using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Manager;

namespace UI
{
    public enum InteractType { Coin, Monster, TrafficCar }

    public class Pseudo3DMinigameManager : MonoBehaviour
    {
        class InteractableObj
        {
            public InteractType type;
            public RectTransform rect;
            public Image img;
            public float currentZ;
            public float roadX; 
            public bool isHit;
        }

        [Header("References")]
        public Pseudo3DRoad roadScroller;
        public RectTransform container; // 전체 화면 진동을 위한 최상위 컨테이너
        public RectTransform itemContainer; // 코인과 몬스터가 생성될 전용 레이어
        
        [Header("Car Sprites")]
        public Image playerCarImage;                // 자동차 UI의 Image 컴포넌트
        public Sprite carCenterSprite;              // 직진 이미지
        public Sprite carLeftSprite;                // 좌측 커브 이미지
        public Sprite carRightSprite;               // 우측 커브 이미지

        [Tooltip("스프라이트가 교체되기 위한 최소 원심력 임계값")]
        public float turnSpriteThreshold = 0.1f;

        [Tooltip("마찰음이 발생하기 시작하는 원심력 임계값")]
        public float skidForceThreshold = 0.15f;    
        private bool _isSkidding = false; // 매 프레임 재생 방지용 상태 플래그

        [Header("Player Car")]
        public RectTransform playerCar;
        public float carMoveSpeed = 3.0f;
        public float maxCarXOffset = 0.4f;
        public float centrifugalMultiplier = 1.5f; // 원심력 강도 (값이 클수록 커브에서 더 강하게 밀려남)
        
        [Tooltip("카메라 좌우 패닝을 담당할 최상단 컨테이너")]
        public RectTransform cameraRoot; 
        public float cameraFollowSpeed = 2.5f; // 카메라가 쫓아가는 속도 (낮을수록 시차가 큼)
        public float maxCameraPan = 100f;      // 카메라 전체가 좌우로 움직이는 최대 픽셀 거리

        [Header("Prefabs")]
        public GameObject coinPrefab;
        public GameObject monsterPrefab;
        public GameObject trafficCarPrefab;

        [Header("Spawn Settings")]
        public float approachSpeed = 10.0f;
        public float spawnZ = 10.0f; // 소실점
        public float cullZ = 0.2f;
        public float hitZ = 0.8f; 
        public float hitTolerance = 0.3f; 

        [Tooltip("앞차의 상대 속도 (1.0이면 정지 사물과 같고, 0.5면 플레이어 속도의 절반으로 달리는 효과)")]
        public float trafficCarRelativeSpeed = 0.6f;
        [Tooltip("앞차의 절대 주행 속도 (이 값이 approachSpeed보다 작아야 유저가 추월 가능)")]
        public float trafficCarAbsoluteSpeed = 5.0f;
        private float _baseApproachSpeed; // 감속 후 원래 속도로 복구하기 위해 기본값을 저장할 변수

        [Header("Visuals")]
        public float baseScale = 2.0f;
        public float horizonY = 1.0f;
        public float yOffset = 0f;

        private List<InteractableObj> _activeObjects = new List<InteractableObj>();
        private Queue<InteractableObj> _coinPool = new Queue<InteractableObj>();
        private Queue<InteractableObj> _monsterPool = new Queue<InteractableObj>();
        private Queue<InteractableObj> _trafficCarPool = new Queue<InteractableObj>();
        private RawImage _roadImage;

        private float _playerRoadX = 0f;
        private float _cameraRoadX = 0f; // 현재 카메라의 논리적 위치
        private float _spawnTimer = 0f;
        private int _pendingCoins = 0;
        private float _pendingCoinX = 0f;
        private float _coinSpawnDelay = 0f;

        public bool isSpawningActive = true;

        public void StopSpawning()
        {
            isSpawningActive = false;
        }

        private void OnEnable()
        {
            _baseApproachSpeed = approachSpeed;

            if (roadScroller != null) _roadImage = roadScroller.GetComponent<RawImage>();
            _isSkidding = false;
            if (ManagerRoot.Sound != null) ManagerRoot.Sound.StopSFX(Data.SfxID.Car_Skid);

            // 논리적 위치 초기화
            _playerRoadX = 0f;
            _cameraRoadX = 0f; 
            _pendingCoins = 0;
            
            // 스폰 상태 초기화
            isSpawningActive = true; 

            // 물리적 UI 위치 초기화
            if (cameraRoot != null)
            {
                cameraRoot.anchoredPosition = new Vector2(0, cameraRoot.anchoredPosition.y);
            }
            if (playerCar != null)
            {
                playerCar.anchoredPosition = new Vector2(0, playerCar.anchoredPosition.y);
                playerCar.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (roadScroller == null || !roadScroller.isMoving || _roadImage == null) return;
            Material currentMat = _roadImage.material;
            if (currentMat == null) return;

            // 쉐이더에서 현재 도로가 꺾인 정도(커브 값)를 먼저 읽어옴
            float currentCurve = currentMat.GetFloat("_CurveAmount");
            float currentHill = currentMat.GetFloat("_HillAmount");

            // 플레이어 조작 함수에 커브 값을 전달하여 원심력을 계산
            HandlePlayerInput(currentCurve);

            float moveAmount = approachSpeed * Time.deltaTime;
            HandleSpawning(moveAmount);

            float width = container.rect.width;
            float height = container.rect.height;

            for (int i = _activeObjects.Count - 1; i >= 0; i--)
            {
                InteractableObj obj = _activeObjects[i];
                
                // 절대 속도 기반 물리적 위치 이동
                if (obj.type == InteractType.TrafficCar)
                {
                    float relativeSpeed = approachSpeed - trafficCarAbsoluteSpeed;
                    obj.currentZ -= relativeSpeed * Time.deltaTime;

                    // 차가 멀어지면 다시 충돌할 수 있도록 isHit 해제. 충돌 거리(hitZ)보다 0.5f 이상 멀어지면 다음 충돌을 허용
                    if (obj.isHit && obj.currentZ > hitZ + 0.5f)
                    {
                        obj.isHit = false;
                        obj.img.color = Color.white; // 점멸 중이었다면 색상도 즉시 원상복구
                    }
                }
                else if (!obj.isHit)
                {
                    obj.currentZ -= moveAmount;
                }

                // 살아있는 경우에만 충돌 판정
                if (!obj.isHit && obj.currentZ <= hitZ && obj.currentZ > cullZ)
                {
                    if (Mathf.Abs(_playerRoadX - obj.roadX) <= hitTolerance)
                    {
                        ProcessHit(obj);
                    }
                }

                // Culling 판정. 플레이어가 차를 지나쳤거나(cullZ), 감속하여 앞차가 지평선 너머로 멀어진 경우 삭제
                if (obj.currentZ <= cullZ || obj.currentZ > spawnZ + 2f)
                {
                    ReturnToPool(obj);
                    _activeObjects.RemoveAt(i);
                    continue; 
                }

                // 시각 업데이트
                UpdateObjectVisuals(obj, currentCurve, currentHill, width, height);
            }
        }

        private void HandlePlayerInput(float currentCurve)
        {
            // 플레이어 조작 입력
            float horizontal = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) horizontal = -1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) horizontal = 1f;

            // 원심력 계산
            float centrifugalForce = -currentCurve * centrifugalMultiplier;

            // 최종 이동 속도
            float finalMoveSpeed = (horizontal * carMoveSpeed) + centrifugalForce;

            // 플레이어의 논리적 위치 이동 
            _playerRoadX = Mathf.Clamp(_playerRoadX + (finalMoveSpeed * Time.deltaTime), -1f, 1f);

            // 카메라 추적 로직 
            _cameraRoadX = Mathf.Lerp(_cameraRoadX, _playerRoadX, Time.deltaTime * cameraFollowSpeed);

            // 카메라 월드 패닝 
            if (cameraRoot != null)
            {
                cameraRoot.anchoredPosition = new Vector2(-_cameraRoadX * maxCameraPan, cameraRoot.anchoredPosition.y);
            }

            // 자동차 UI 물리적 이동 및 기울임
            if (playerCar != null)
            {
                float targetX = _playerRoadX * (container.rect.width * maxCarXOffset);
                playerCar.anchoredPosition = new Vector2(targetX, playerCar.anchoredPosition.y);
                
                float targetTilt = (horizontal * -1.5f) + (currentCurve * 2f); 
                playerCar.localRotation = Quaternion.Lerp(playerCar.localRotation, Quaternion.Euler(0, 0, targetTilt), Time.deltaTime * 10f);
            }

            // 조건부 스프라이트 교체 로직
            if (playerCarImage != null)
            {
                // 원심력의 크기가 설정한 임계값 이상인지 확인
                if (Mathf.Abs(centrifugalForce) >= turnSpriteThreshold)
                {
                    if (horizontal < 0f) 
                        playerCarImage.sprite = carLeftSprite;  // 강한 원심력 중 왼쪽으로 이동 시도
                    else if (horizontal > 0f) 
                        playerCarImage.sprite = carRightSprite; // 강한 원심력 중 오른쪽으로 이동 시도
                    else 
                        playerCarImage.sprite = carCenterSprite; // 키보드에서 손을 뗀 상태
                }
                else
                {
                    // 직진 구간이거나 원심력이 매우 약할 때는 항상 중앙 이미지 유지
                    playerCarImage.sprite = carCenterSprite;
                }
            }

            // 타이어 마찰음 재생 로직. 원심력이 임계값을 넘고 && 플레이어가 해당 방향으로 조향하여 저항이 발생할 때
            bool isDrifting = Mathf.Abs(centrifugalForce) >= skidForceThreshold && horizontal != 0f;

            if (isDrifting && !_isSkidding)
            {
                _isSkidding = true;
                if (!ManagerRoot.Sound.IsSfxPlaying(Data.SfxID.Car_Skid))
                    ManagerRoot.Sound.PlaySFX(Data.SfxID.Car_Skid, 1.0f, 1.0f, true); 
            }
            else if (!isDrifting && _isSkidding)
            {
                // 코너링이 끝나거나 키보드에서 손을 뗐을 때 1번만 실행됨
                _isSkidding = false;
                if (ManagerRoot.Sound.IsSfxPlaying(Data.SfxID.Car_Skid))
                    ManagerRoot.Sound.StopSFX(Data.SfxID.Car_Skid);
            }
        }

        private void HandleSpawning(float moveAmount)
        {
            if (!isSpawningActive) return;

            if (_pendingCoins > 0)
            {
                _coinSpawnDelay -= moveAmount;
                if (_coinSpawnDelay <= 0)
                {
                    SpawnObject(InteractType.Coin, _pendingCoinX);
                    _pendingCoins--;
                    _coinSpawnDelay = 1.5f; 
                }
            }
            else
            {
                _spawnTimer -= moveAmount;
                if (_spawnTimer <= 0)
                {
                    float randomRoadX = Random.Range(-0.8f, 0.8f);
                    float randVal = Random.value;
                    
                    if (randVal > 0.8f) 
                    {
                        _pendingCoins = Random.Range(3, 6);
                        _pendingCoinX = randomRoadX;
                    }
                    else if (randVal > 0.7f)
                    {
                        float laneX = Random.value > 0.5f ? 0.4f : -0.4f; 
                        SpawnObject(InteractType.TrafficCar, laneX);
                    }
                    else 
                    {
                        SpawnObject(InteractType.Monster, randomRoadX);
                    }
                    
                    _spawnTimer = Random.Range(5f, 15f); 
                }
            }
        }

        private void SpawnObject(InteractType type, float roadX)
        {
            InteractableObj obj = GetFromPool(type);
            obj.roadX = roadX;
            obj.currentZ = spawnZ;
            obj.isHit = false;
            
            obj.rect.DOKill(); 
            obj.img.DOKill(); 
            
            // 몬스터의 컬러와 로테이션을 복구 
            obj.img.color = Color.white;
            obj.rect.localRotation = Quaternion.identity; 
            
            obj.rect.SetAsFirstSibling(); 
            
            _activeObjects.Add(obj);
        }

        private void UpdateObjectVisuals(InteractableObj obj, float curve, float hill, float width, float height)
        {
            float depth = 1f / obj.currentZ;
            float adjustedY = horizonY * (1f - depth);

            float yNorm;
            if (Mathf.Abs(hill) < 0.001f) yNorm = adjustedY;
            else
            {
                float discriminant = 1f - 4f * hill * adjustedY;
                if (discriminant < 0f) yNorm = 2.0f;
                else yNorm = (1f - Mathf.Sqrt(discriminant)) / (2f * hill);
            }

            float curveOffset = (adjustedY * adjustedY) * curve;
            float finalX = (curveOffset * width) + (obj.roadX * width * 0.5f * depth);
            
            obj.rect.anchoredPosition = new Vector2(finalX, (yNorm * height) + yOffset);

            float scale = depth * baseScale;
            if (!obj.isHit || obj.type == InteractType.TrafficCar) 
            {
                obj.rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void ProcessHit(InteractableObj obj)
        {
            obj.isHit = true;
            
            obj.rect.DOKill();
            obj.img.DOKill();

            if (obj.type == InteractType.Coin)
            {
                ManagerRoot.Sound.PlaySFX(Data.SfxID.Coin);
                // 획득 즉시 Z값을 화면 밖(cullZ보다 작게)으로 밀어내어 곧바로 사라지게 만듦
                obj.currentZ = cullZ - 1f; 
                // obj.rect.DOAnchorPosY(obj.rect.anchoredPosition.y + 100f, 0.3f);
                // obj.rect.DOScale(0f, 0.3f).SetEase(Ease.InBack);
                // obj.img.DOFade(0f, 0.3f);
            }
            else if (obj.type == InteractType.Monster)
            {
                ManagerRoot.Sound.PlaySFX((Random.value > 0.5f) ? Data.SfxID.Splatter_1 : Data.SfxID.Splatter_2);
                
                container.DOShakeAnchorPos(0.3f, 40f, 50);

                DOTween.To(() => obj.currentZ, x => obj.currentZ = x, obj.currentZ + 30f, 0.4f)
                       .SetEase(Ease.OutExpo);
                
                obj.img.DOColor(Color.red, 0.4f);
                obj.rect.DORotate(new Vector3(0, 0, Random.Range(-1080f, 1080f)), 0.4f, RotateMode.FastBeyond360);
                
                obj.rect.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).OnComplete(() => {
                    obj.currentZ = cullZ - 1f; 
                });
            }
            else if (obj.type == InteractType.TrafficCar)
            {
                ManagerRoot.Sound.PlaySFX(Data.SfxID.Car_Crash);
                
                // 충돌 진동 
                container.DOShakeAnchorPos(0.4f, 40f, 40);

                // 데미지 깜빡임
                obj.img.DOColor(Color.red, 0.1f).SetLoops(6, LoopType.Yoyo).OnComplete(() => {
                    obj.img.color = Color.white;
                });

                // 실제 속도 감속 
                DOTween.Kill("SpeedRecovery"); 
                
                // 부딪힌 앞 차가 멀어짐
                DOTween.To(() => approachSpeed, x => approachSpeed = x, _baseApproachSpeed * 0.1f, 0.1f)
                       .SetId("SpeedRecovery")
                       .OnComplete(() => {
                           // 1.5초에 걸쳐 원래 속도로 엔진 가속 복구
                           DOTween.To(() => approachSpeed, x => approachSpeed = x, _baseApproachSpeed, 1.5f)
                                  .SetEase(Ease.InQuad)
                                  .SetId("SpeedRecovery");
                       });

                // 페널티 부여: 수리비 차감
                int repairCost = 500;
                Debug.Log($"일반 차량과 충돌! 수리비 {repairCost} 청구!");
                // TODO: 차감된 금액 표시 로직
                ManagerRoot.Finance.SubMoney(repairCost); 
            }
        }

        #region Object Pooling
        private InteractableObj GetFromPool(InteractType type)
        {
            Queue<InteractableObj> pool;
            GameObject prefab;

            if (type == InteractType.Coin) { pool = _coinPool; prefab = coinPrefab; }
            else if (type == InteractType.Monster) { pool = _monsterPool; prefab = monsterPrefab; }
            else { pool = _trafficCarPool; prefab = trafficCarPrefab; } // TrafficCar 추가

            if (pool.Count > 0)
            {
                InteractableObj obj = pool.Dequeue();
                obj.rect.gameObject.SetActive(true);
                return obj;
            }
            else
            {
                GameObject go = Instantiate(prefab, itemContainer);
                InteractableObj obj = new InteractableObj
                {
                    type = type,
                    rect = go.GetComponent<RectTransform>(),
                    img = go.GetComponent<Image>()
                };
                obj.rect.anchorMin = new Vector2(0.5f, 0f);
                obj.rect.anchorMax = new Vector2(0.5f, 0f);
                obj.rect.pivot = new Vector2(0.5f, 0f);
                return obj;
            }
        }

        private void ReturnToPool(InteractableObj obj)
        {
            obj.rect.gameObject.SetActive(false);
            if (obj.type == InteractType.Coin) _coinPool.Enqueue(obj);
            else if (obj.type == InteractType.Monster) _monsterPool.Enqueue(obj);
            else _trafficCarPool.Enqueue(obj);
        }

        private void OnDisable()
        {
            foreach (var obj in _activeObjects) ReturnToPool(obj);
            _activeObjects.Clear();
            _playerRoadX = 0f;
            if (playerCar != null) 
            {
                playerCar.anchoredPosition = new Vector2(0, playerCar.anchoredPosition.y);
                playerCar.localRotation = Quaternion.identity;
            }
        }
        #endregion
    }
}