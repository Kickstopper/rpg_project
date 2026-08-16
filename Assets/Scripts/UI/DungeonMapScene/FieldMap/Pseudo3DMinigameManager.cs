using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Manager;

namespace UI
{
    public enum InteractType { Coin, Monster }

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
        
        [Header("Player Car")]
        public RectTransform playerCar;
        public float carMoveSpeed = 3.0f;
        public float maxCarXOffset = 0.4f;
        
        [Tooltip("카메라 좌우 패닝을 담당할 최상단 컨테이너")]
        public RectTransform cameraRoot; 
        public float cameraFollowSpeed = 2.5f; // 카메라가 쫓아가는 속도 (낮을수록 시차가 큼)
        public float maxCameraPan = 100f;      // 카메라 전체가 좌우로 움직이는 최대 픽셀 거리

        [Header("Prefabs")]
        public GameObject coinPrefab;
        public GameObject monsterPrefab;

        [Header("Spawn Settings")]
        public float approachSpeed = 10.0f;
        public float spawnZ = 10.0f;
        public float cullZ = 0.2f;
        public float hitZ = 0.8f; 
        public float hitTolerance = 0.3f; 

        [Header("Visuals")]
        public float baseScale = 2.0f;
        public float horizonY = 1.0f;
        public float yOffset = 0f;

        private List<InteractableObj> _activeObjects = new List<InteractableObj>();
        private Queue<InteractableObj> _coinPool = new Queue<InteractableObj>();
        private Queue<InteractableObj> _monsterPool = new Queue<InteractableObj>();
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
            if (roadScroller != null) _roadImage = roadScroller.GetComponent<RawImage>();
            
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

            HandlePlayerInput();

            float moveAmount = approachSpeed * Time.deltaTime;
            HandleSpawning(moveAmount);

            float currentCurve = currentMat.GetFloat("_CurveAmount");
            float currentHill = currentMat.GetFloat("_HillAmount");
            float width = container.rect.width;
            float height = container.rect.height;

            for (int i = _activeObjects.Count - 1; i >= 0; i--)
            {
                InteractableObj obj = _activeObjects[i];
                
                // 몬스터가 충돌하지 않은 상태일 때만 플레이어를 향해 다가옴
                if (!obj.isHit)
                {
                    obj.currentZ -= moveAmount;
                }

                if (!obj.isHit && obj.currentZ <= hitZ && obj.currentZ > cullZ)
                {
                    if (Mathf.Abs(_playerRoadX - obj.roadX) <= hitTolerance)
                    {
                        ProcessHit(obj);
                    }
                }

                if (obj.currentZ <= cullZ)
                {
                    ReturnToPool(obj);
                    _activeObjects.RemoveAt(i);
                    continue; // 삭제된 후에는 렌더링을 건너뜀
                }

                UpdateObjectVisuals(obj, currentCurve, currentHill, width, height);
            }
        }

        private void HandlePlayerInput()
        {
            // 플레이어 조작 입력
            float horizontal = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) horizontal = -1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) horizontal = 1f;

            // 플레이어의 논리적 위치 이동 (-1.0 ~ 1.0)
            _playerRoadX = Mathf.Clamp(_playerRoadX + (horizontal * carMoveSpeed * Time.deltaTime), -1f, 1f);

            // 카메라 추적 로직
            // 카메라(_cameraRoadX)가 플레이어(_playerRoadX)의 위치를 부드럽게 쫓아감
            _cameraRoadX = Mathf.Lerp(_cameraRoadX, _playerRoadX, Time.deltaTime * cameraFollowSpeed);

            // 카메라 월드 패닝
            if (cameraRoot != null)
            {
                cameraRoot.anchoredPosition = new Vector2(-_cameraRoadX * maxCameraPan, cameraRoot.anchoredPosition.y);
            }

            // 자동차 UI 렌더링
            if (playerCar != null)
            {
                // 차체의 물리적 위치
                float targetX = _playerRoadX * (container.rect.width * maxCarXOffset);
                playerCar.anchoredPosition = new Vector2(targetX, playerCar.anchoredPosition.y);
                
                // 차체 기울임
                float targetTilt = horizontal * -4f; 
                playerCar.localRotation = Quaternion.Lerp(playerCar.localRotation, Quaternion.Euler(0, 0, targetTilt), Time.deltaTime * 10f);
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
                    
                    if (Random.value > 0.3f) 
                    {
                        _pendingCoins = Random.Range(3, 6);
                        _pendingCoinX = randomRoadX;
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
            if (!obj.isHit) 
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
        }

        #region Object Pooling
        private InteractableObj GetFromPool(InteractType type)
        {
            Queue<InteractableObj> pool = type == InteractType.Coin ? _coinPool : _monsterPool;
            if (pool.Count > 0)
            {
                InteractableObj obj = pool.Dequeue();
                obj.rect.gameObject.SetActive(true);
                return obj;
            }
            else
            {
                GameObject prefab = type == InteractType.Coin ? coinPrefab : monsterPrefab;
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
            else _monsterPool.Enqueue(obj);
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