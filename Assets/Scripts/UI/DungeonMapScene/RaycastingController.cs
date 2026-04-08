using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using UI.DungeonMapScene;
using UI;
using TMPro;

namespace Controller
{
    public class RaycastingController : MonoBehaviour
    {
        [Header("Settings")]
        public UI.DungeonMapScene.RenderSettings renderSettings;
        [Range(0.0f, 0.499f)] public float backwardOffset = 0.499f;
        public float fovScale = 1f;
        
        [Header("Game References")]
        public RawImage screenImage;
        public RawImage backgroundImage;
        public CompassUI compassUI;
        public WeatherUI weatherUI;
        public GridMap miniMap;
        public AutoMapRenderer autoMapRenderer;
        public GameObject autoMapContainer;
        public CanvasGroup fadeOverlay;
        public GameObject systemMessagePanel;
        public TextMeshProUGUI systemMessageText;

        [Header("Input")]
        public float doubleTapThreshold = 0.3f;
        public float moveDuration = 0.2f;
        public float turnDuration = 0.2f;
        
        [Header("Encounter System")]
        public EncounterSystem encounterSystem;

        // 시점 상태 관리
        public enum LookState { None, Up, Down }
        private LookState _currentLookState = LookState.None;
        private bool _isLookTransitioning = false; // 시점이 부드럽게 변하는 애니메이션 중인지 여부

        // 몬스터 리스폰 관련 변수
        private int _maxSpawnCount = 0;
        private float _spawnDelay = 0f;
        private float _currentSpawnTimer = 0f;

        // 서브 시스템
        private RaycastRenderEngine _renderer;
        private DungeonPlayer _player;
        
        // 상태
        private TileAnimState[,] _tileAnimStates;
        private MapData _currentMap;

        public class MapEnemy
        {
            public float x, y;
            public float targetX, targetY;
            public int direction; 
            
            public int baseTexIdx;    
            public int currentTexIdx; 
            
            public bool isAlive = true;
            public bool isMoving = false; 

            public float animTimer = 0f;
            public int animFrame = 0; 
            
            // AI 관련 변수
            public float moveTimer = 0f;      // 행동 쿨타임
            public float moveInterval = 1.5f; // 몇 초마다 1칸씩 움직일지
            public float aggroRange = 5.0f;   // 플레이어 추적 시작 거리. 칸 수
            public float moveSpeed = 2.5f;    // 화면에서 시각적으로 움직이는 속도
        }

        public class MapObject
        {
            public float x, y;
            public int texIdx;          // 표시할 텍스처 인덱스
            public bool isSolid = true; // 통과 가능 여부
            public bool isActive = true;// 상호작용 가능/표시 여부
            
            public string objectId;     // 상호작용 이벤트 처리를 위한 ID
        }
        private List<MapEnemy> _activeEnemies = new List<MapEnemy>(); // 심볼 인카운터 에너미 리스트
        private List<MapObject> _staticObjects = new List<MapObject>(); // 고정 오브젝트 리스트

        private bool _canRender = true;
        private bool _inputLocked = false;
        private float inputCooldown = 0f;
        private float _lastWPressTime = -100f;
        private bool _isScanning = false;
        private KeyCode _lastMoveKey = KeyCode.None; 
        
        [HideInInspector]
        public bool isUIHoldingMovement = false; // 가상 컨트롤러에서 누르고 있는지 여부
        
        void Awake()
        {
            _renderer = new RaycastRenderEngine();
            // illusion ID 리스트는 필요 시 Inspector나 DungeonManager에서 가져옴
            _player = new DungeonPlayer(this, fovScale, backwardOffset, new List<int>()); 
            
            _player.OnMoveStepTaken += OnPlayerStep;
        }

        void Start()
        {
            HideSystemMessage();

            _renderer.Initialize(renderSettings.screenWidth, renderSettings.screenHeight);
            
            Material mat;
            if (renderSettings.screenMaterial != null)
            {
                mat = new Material(renderSettings.screenMaterial);
            }
            else
            {
                // 할당 안 했을 경우 비상용 기본값
                mat = new Material(Shader.Find("UI/Default")); 
            }
            mat.mainTexture = _renderer.ScreenTexture;
            screenImage.material = mat;
            screenImage.rectTransform.localScale = renderSettings.screenScale;

            // 맵 초기화
            LoadMapData();
            
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            GameStateManager.Instance.ChangeState(GameState.Exploration);
        }

        void OnDestroy()
        {
            if(GameStateManager.Instance) 
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        void Update()
        {
            if (!_canRender) return;

            if (inputCooldown > 0)
            {
                inputCooldown -= Time.deltaTime;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
                inputCooldown = 0.2f;
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.O)) 
            {
                ToggleMovementMode();
                return;
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!ModuleManager.Instance.IsMounted(ModuleFeature.AutoMapper)) return;
                autoMapContainer.SetActive(!autoMapContainer.activeSelf);
                return;
            }

            if (Input.GetKeyDown(KeyCode.P)) 
            {
                GameSettingManager.Instance.useAnaglyph = !GameSettingManager.Instance.useAnaglyph;
                Debug.Log($"Anaglyph: {GameSettingManager.Instance.useAnaglyph}");
                return;
            }

            // 입력 처리 분기
            if (_player.IsGridMove)
            {
                HandleInput(); // 그리드 입력
            }
            else
            {
                HandleFreeMoveInput(); // 자유 이동 입력
            }

            UpdateWallAnimations();
            
            // 몬스터 심볼의 이동과 스폰
            UpdateEnemyAI();
            UpdateEnemySprites();
            UpdateEnemySpawner();

            _renderer.RenderFrame(_player, renderSettings);
            UpdateBackgroundUV();
        }

        // 모드 전환 메서드
        private void ToggleMovementMode()
        {
            if (_player.IsGridMove)
            {
                _player.IsGridMove = false;
                Debug.Log("Switched to Free Move");
            }
            else
            {
                // 그리드 상태로 스냅핑
                _player.SnapToGrid();
                _player.IsGridMove = true;
                
                // 스냅 후 미니맵/아이콘 동기화
                if (miniMap) miniMap.SnapToGrid(_player.LogicX, _player.LogicY, _player.DirectionIdx);
                if (compassUI) compassUI.SetDirection(_player.DirectionIdx);
                UpdateMapDiscovery(_player.LogicX, _player.LogicY);
                
                Debug.Log("Switched to Grid Move");
            }
        }

        // 자유 이동 입력 처리
        private void HandleFreeMoveInput()
        {
            if (_inputLocked) return;

            float moveSpeed = Time.deltaTime * 3.0f; // 속도 조절
            float rotSpeed = Time.deltaTime * 2.0f;

            // 점프
            //if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));
            // 스캔
            if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());

            // 이동
            if (Input.GetKey(KeyCode.W)) _player.MoveFree(moveSpeed);
            if (Input.GetKey(KeyCode.S)) _player.MoveFree(-moveSpeed);

            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) 
            {
                _player.RotateFree(rotSpeed); // 왼쪽 회전
            }
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) 
            {
                _player.RotateFree(-rotSpeed); // 오른쪽 회전
            }

            // Free Move 시 미니맵 갱신 (즉시 갱신)
            if (miniMap) miniMap.SetFreeDirection(_player.DirX, _player.DirY);
            autoMapRenderer.UpdatePlayerIconFree(_player.PosX, _player.PosY, _player.DirX, _player.DirY);
        }

        // ================= Input & Logic =================
        private void HandleInput()
        {
            // 시점 전환 애니메이션 중이면 다른 입력 무시
            if (_isLookTransitioning) return;

            // 올려보기/내려보기 상태 유지 중일 때의 처리
            if (_currentLookState != LookState.None)
            {
                if (Input.anyKeyDown)
                {
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    {
                       if (_currentLookState == LookState.Up)
                        {
                            CellData currentCell = _currentMap.GetCell(_player.LogicX, _player.LogicY);
                            
                            // 현재 칸의 천장이 void인지 확인
                            if (currentCell != null && currentCell.value == 1)
                            {
                                EntranceData ceilingEntrance = _currentMap.GetEntranceAt(_player.LogicX, _player.LogicY);
                                
                                if (ceilingEntrance != null)
                                {
                                    StartCoroutine(JumpUpRoutine(ceilingEntrance));
                                    return;
                                }
                            }
                        }
                        else if (_currentLookState == LookState.Down)
                        {
                            Vector2Int fwd = _player.GetForwardVector();
                            int tx = _player.LogicX + fwd.x;
                            int ty = _player.LogicY + fwd.y;

                            CellData targetCell = _currentMap.GetCell(tx, ty);
                            
                            // 앞 칸의 바닥이 void인지 확인
                            if (targetCell != null && targetCell.value == -1)
                            {
                                EntranceData holeEntrance = _currentMap.GetEntranceAt(tx, ty);
                                
                                if (holeEntrance != null)
                                {
                                    StartCoroutine(JumpDownRoutine(holeEntrance, fwd));
                                    return;
                                }
                            }
                        }
                    }

                    // 확인 키가 아니거나 구멍이 아니거나 EntranceData가 없으면 원래 상태로 복귀
                    if (!_isLookTransitioning) 
                        StartCoroutine(TransitionLookState(LookState.None));
                }
                return;
            }

            if (_inputLocked) return;
            
            // 올려보기 및 내려보기
            if (Input.GetKey(KeyCode.LeftShift) && !_player.IsMoving)
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    StartCoroutine(TransitionLookState(LookState.Up));
                    return;
                }
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    StartCoroutine(TransitionLookState(LookState.Down));
                    return;
                }
            }
            
            if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!ModuleManager.Instance.IsMounted(ModuleFeature.AutoMapper)) return;
                autoMapContainer.SetActive(!autoMapContainer.activeSelf);
            } 
            if (Input.GetKeyDown(KeyCode.P))
            {
                GameSettingManager.Instance.useAnaglyph = !GameSettingManager.Instance.useAnaglyph;
                Debug.Log($"Anaglyph Mode: {GameSettingManager.Instance.useAnaglyph}");
            }

            // 상하좌우 키 중 하나라도 눌리면 더블 탭 체크
            bool anyMoveKeyDown = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) ||
                                  Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
                                  Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                                  Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow); 
            
            // 이동 관련 키 배열 (W, S, A, D, 방향키 4개)
            KeyCode[] moveKeys = { 
                KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, 
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow 
            };

            foreach (KeyCode key in moveKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    // 같은 키를 두 번 눌렀는지, 시간 간격도 Threshold 이내인지 체크
                    if (key == _lastMoveKey && (Time.time - _lastWPressTime < doubleTapThreshold))
                    {
                        _player.SetRunning(true);
                    }
                    else
                    {
                        _lastMoveKey = key;
                    }
                    
                    _lastWPressTime = Time.time;
                    break;
                }
            }

            // 키를 모두 떼면 달리기 해제
            if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S) &&
                !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D) && 
                !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow) &&
                !Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.RightArrow))
            {
                // 가상 컨트롤러에서 누르고 있는 중이 아닐 때만 달리기 해제
                if (!isUIHoldingMovement)
                    _player.SetRunning(false);
            }

            if (!_player.IsMoving)
            {
                //if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));

                // 이동 입력
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) TryMove(1);
                else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) TryMove(-1);
                
                // 좌우 수평 이동
                else if (Input.GetKey(KeyCode.A)) TryStrafe(-1);
                else if (Input.GetKey(KeyCode.D)) TryStrafe(1);
                
                // 회전
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) StartCoroutine(TurnRoutine(-1));
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) StartCoroutine(TurnRoutine(1));
            }
        }

        // 올려보기 내려보기 코루틴
        private IEnumerator TransitionLookState(LookState targetState)
        {
            _isLookTransitioning = true;
            
            if (targetState == LookState.None)
            {
                HideSystemMessage(); // 정면을 보면 메시지 숨김
            }
            else if (targetState == LookState.Up)
            {
                // 천장에 입구가 있는지 확인
                EntranceData entrance = _currentMap.GetEntranceAt(_player.LogicX, _player.LogicY);
                if (entrance != null) ShowSystemMessage("올라갈 수 있을 것 같다. 올라가시겠습니까?");
            }
            else if (targetState == LookState.Down)
            {
                // 바로 앞 바닥에 입구가 있는지 확인
                Vector2Int fwd = _player.GetForwardVector();
                EntranceData entrance = _currentMap.GetEntranceAt(_player.LogicX + fwd.x, _player.LogicY + fwd.y);
                if (entrance != null) ShowSystemMessage("바닥이 보인다. 뛰어내리시겠습니까?");
            }

            float startPitch = _player.Pitch;
            float endPitch = 0f;
            if (targetState == LookState.Up) endPitch = -100f;
            else if (targetState == LookState.Down) endPitch = 100f;

            float startOffset = _player.BackwardOffset;
            float endOffset = (targetState == LookState.None || targetState == LookState.Up) ? this.backwardOffset : 0f;

            float duration = 0.3f;
            float elapsed = 0f;

            // 애니메이션
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                t = t * t * (3f - 2f * t); // SmoothStep을 적용하여 시작과 끝을 더 부드럽게 감속

                _player.Pitch = Mathf.Lerp(startPitch, endPitch, t);
                _player.BackwardOffset = Mathf.Lerp(startOffset, endOffset, t);

                // BackwardOffset이 변하므로 플레이어의 논리좌표 내의 물리적 위치를 재계산
                Vector2 updatedPos = _player.GetOffsetPosition(_player.LogicX, _player.LogicY, _player.DirectionIdx);
                _player.SetDirectPosition(updatedPos.x, updatedPos.y, _player.DirectionIdx);

                yield return null;
            }

            // 최종 값 오차 보정
            _player.Pitch = endPitch;
            _player.BackwardOffset = endOffset;
            
            Vector2 finalPos = _player.GetOffsetPosition(_player.LogicX, _player.LogicY, _player.DirectionIdx);
            _player.SetDirectPosition(finalPos.x, finalPos.y, _player.DirectionIdx);

            _currentLookState = targetState;
            _isLookTransitioning = false;
        }

        private IEnumerator JumpUpRoutine(EntranceData entrance)
        {
            HideSystemMessage();

            _isLookTransitioning = true;
            _inputLocked = true;

            float elapsed = 0f;
            float duration = 0.8f;
            
            // 중앙 정렬을 위해 현재 위치 정보 저장
            float startPosX = _player.PosX;
            float startPosY = _player.PosY;
            float startPitch = _player.Pitch;
            
            // 목표 위치는 현재 그리드의 정중앙
            float centerPosX = _player.LogicX + 0.5f;
            float centerPosY = _player.LogicY + 0.5f;
            float targetPitch = -400f; // 천장을 뚫고 지나가는 느낌을 위해 Pitch를 크게 감소

            if (fadeOverlay != null) fadeOverlay.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeIn = t * t;

                // 그리드 중앙으로 위치 고정하며 수직 상승 연출
                // JumpOffset을 직접 수정할 수 없으므로, 시각적 피드백을 위해 Pitch를 강하게 조절함.
                _player.SetDirectPosition(
                    Mathf.Lerp(startPosX, centerPosX, t),
                    Mathf.Lerp(startPosY, centerPosY, t),
                    _player.DirectionIdx
                );

                // 시점 상승 연출
                _player.Pitch = Mathf.Lerp(startPitch, targetPitch, easeIn);

                // 마지막 0.3초 동안 페이드 아웃
                if (fadeOverlay != null && t > 0.6f)
                {
                    fadeOverlay.alpha = (t - 0.6f) / 0.4f;
                }

                yield return null;
            }

            // 레벨 전환 실행
            DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
            DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
            
            // 새로운 맵 로드
            LoadMapData(entrance);
            
            yield return new WaitForSeconds(0.1f);

            StartCoroutine(LandingImpactRoutine(10f));

            _player.BackwardOffset = this.backwardOffset;
            _currentLookState = LookState.None;

            // 페이드 인 및 상태 초기화
            if (fadeOverlay != null)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < 0.5f)
                {
                    fadeElapsed += Time.deltaTime;
                    fadeOverlay.alpha = 1f - (fadeElapsed / 0.5f);
                    yield return null;
                }
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
            }

            // 상태 복구
            _inputLocked = false;
            _isLookTransitioning = false;
        }

        private IEnumerator JumpDownRoutine(EntranceData entrance, Vector2Int moveDir)
        {
            HideSystemMessage();

            _isLookTransitioning = true;
            _inputLocked = true;

            // 낙하 애니메이션
            float elapsed = 0f;
            float duration = 0.8f;
            
            float startPosX = _player.PosX;
            float startPosY = _player.PosY;
            float startPitch = _player.Pitch;

            // 구멍의 중앙 좌표
            float targetPosX = _player.LogicX + moveDir.x + 0.5f;
            float targetPosY = _player.LogicY + moveDir.y + 0.5f;
            float targetPitch = 300f; // 바닥을 뚫고 지나가는 느낌을 주기 위해 Pitch를 크게 증가

            if (fadeOverlay != null) fadeOverlay.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeIn = t * t; // 가속도 효과

                // 구멍 안쪽으로 이동
                _player.SetDirectPosition(
                    Mathf.Lerp(startPosX, targetPosX, t),
                    Mathf.Lerp(startPosY, targetPosY, t),
                    _player.DirectionIdx
                );

                // 시점 낙하 연출
                _player.Pitch = Mathf.Lerp(startPitch, targetPitch, easeIn);

                // 마지막 0.3초 동안 페이드 아웃
                if (fadeOverlay != null && t > 0.6f)
                {
                    fadeOverlay.alpha = (t - 0.6f) / 0.4f;
                }

                yield return null;
            }

            // 레벨 전환
            DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
            DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
            
            // 새로운 맵 로드
            LoadMapData(entrance);
            
            yield return new WaitForSeconds(0.1f);

            // 착지 흔들림 코루틴
            StartCoroutine(LandingImpactRoutine(150f));

            // 흔들림이 시작되기 전, 상태와 오프셋을 미리 원상 복구
            _player.BackwardOffset = this.backwardOffset; 
            _currentLookState = LookState.None;

            // 페이드 인 및 상태 초기화
            if (fadeOverlay != null)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < 0.5f)
                {
                    fadeElapsed += Time.deltaTime;
                    fadeOverlay.alpha = 1f - (fadeElapsed / 0.5f);
                    yield return null;
                }
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
            }

            _inputLocked = false;
            _isLookTransitioning = false;
        }

        private IEnumerator LandingImpactRoutine(float magnitude, float duration = 0.6f)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float damp = 1f - t; // 시간이 지날수록 진동 폭이 줄어들도록
                _player.Pitch = Mathf.Sin(t * Mathf.PI * 6f) * magnitude * damp; // 사인파로 상하 진동

                yield return null;
            }
            
            // 진동이 끝나면 시점을 정면(0)으로
            _player.Pitch = 0f;
        }
        
        private void PerformMove(Vector2Int moveVec)
        {
            int tx = _player.LogicX + moveVec.x;
            int ty = _player.LogicY + moveVec.y;

            // 심볼 인카운터. 적과 부딪혔는지 검사
            MapEnemy encounteredEnemy = _activeEnemies.Find(e => Mathf.FloorToInt(e.x) == tx && Mathf.FloorToInt(e.y) == ty && e.isAlive);
            if (encounteredEnemy != null)
            {
                EncounterType encType = DetermineEncounterAdvantage(encounteredEnemy, true);
                StartCoroutine(SymbolEncounterRoutine(encounteredEnemy, moveVec, encType));
                return;
            }

            // 통과할 수 없는 고정 오브젝트 충돌 (예: 잠긴 상자, 기둥)
            MapObject blockingObj = _staticObjects.Find(o => Mathf.FloorToInt(o.x) == tx && Mathf.FloorToInt(o.y) == ty && o.isActive);
            if (blockingObj != null && blockingObj.isSolid)
            {
                // 부딪히는 사운드와 애니메이션만 재생하고 이동은 막음
                StartCoroutine(_player.BumpRoutine(moveVec, 0.2f, 0.3f, null));
                SoundManager.Instance.PlaySFX(SfxID.Bump_Wall);
                return; 
            }

            // 이동 가능 여부 체크
            bool walkable = _player.IsWalkable(tx, ty, moveVec.x, moveVec.y);
            
            if (walkable)
            {
                float duration = _player.IsRunning ? moveDuration / 2f : moveDuration;
                if (miniMap) miniMap.TranslateToNewPosition(tx, ty, duration);
                StartCoroutine(_player.MoveGridRoutine(tx, ty, duration, null));
            }
            else
            {
                EntranceData validEntrance = CheckForEntrance(_player.LogicX, _player.LogicY, tx, ty, moveVec);

                if (validEntrance != null)
                {
                    // 입구가 있다면 레벨 전환 시작
                    Debug.Log($"[Entrance] {validEntrance.destinationID}으로 이동합니다.");
                    StartCoroutine(TransitionToOtherPlace(validEntrance, moveVec));
                }
                else
                {
                    CellData targetCell = _currentMap.GetCell(tx, ty);
                    bool isVoidTile = (targetCell != null && targetCell.value == -1);

                    if (!isVoidTile)
                    {
                        // 일반 벽일 경우에만 충돌 애니메이션과 사운드 재생
                        StartCoroutine(_player.BumpRoutine(moveVec, 0.2f, 0.3f, null));
                        SoundManager.Instance.PlaySFX(SfxID.Bump_Wall);
                    }
                    else
                    {
                        // 구멍(void) 앞에서는 아무런 피드백 없이 이동만 무시
                        Debug.Log("Void tile ahead. Movement blocked silently.");
                    }
                }
            }
        }

        private IEnumerator SymbolEncounterRoutine(MapEnemy enemy, Vector2Int moveVec, EncounterType encType)
        {
            _inputLocked = true;

            // 적에게 부딪히는 연출 (벽 충돌과 같음)
            SoundManager.Instance.PlaySFX(SfxID.Bump_Wall); 
            yield return StartCoroutine(_player.BumpRoutine(moveVec, 0.2f, 0.3f, null));
            
            // 부딪히면 해당 적 심볼 삭제
            enemy.isAlive = false;
            _activeEnemies.Remove(enemy);
            UpdateSpriteData(); // 화면에서 스프라이트 제거
            yield return null;

            // 전투 개시 명령
            GameStateManager.Instance.StartEncounter(encounterSystem.MonsterCandidate, encType); 

            // 전투가 끝나고 탐험 상태로 돌아올 때까지 대기
            yield return new WaitUntil(() => GameStateManager.Instance.CurrentState == GameState.Exploration);

            _inputLocked = false;
        }

        private void ShowSystemMessage(string message)
        {
            if (systemMessagePanel != null) systemMessagePanel.SetActive(true);
            if (systemMessageText != null) systemMessageText.text = message;
        }

        private void HideSystemMessage()
        {
            if (systemMessagePanel != null) systemMessagePanel.SetActive(false);
        }

        public void UI_SetRunning(bool isRunning)
        {
            if (_player != null)
            {
                _player.SetRunning(isRunning);
            }
        }

        // UI Virtual Controller 용
        public void UI_MoveForward()
        {
            if (_inputLocked || _player.IsMoving) return;
            TryMove(1);
        }

        public void UI_MoveBackward()
        {
            if (_inputLocked || _player.IsMoving) return;
            TryMove(-1);
        }

        public void UI_MoveLeft()
        {
            if (_inputLocked || _player.IsMoving) return;
            TryStrafe(-1);
        }

        public void UI_MoveRight()
        {
            if (_inputLocked || _player.IsMoving) return;
            TryStrafe(1);
        }

        public void UI_TurnLeft()
        {
            if (_inputLocked || _player.IsMoving) return;
            StartCoroutine(TurnRoutine(-1));
        }

        public void UI_TurnRight()
        {
            if (_inputLocked || _player.IsMoving) return;
            StartCoroutine(TurnRoutine(1));
        }

        public void UI_Action()
        {
            // 입력이 잠겨있거나, 이동 중이거나, 시점이 움직이는 중이면 무시
            if (_inputLocked || _player.IsMoving || _isLookTransitioning) return;

            // 이미 올려보기 / 내려보기 상태일 때의 동작 (점프 및 화면 전환)
            if (_currentLookState == LookState.Up)
            {
                CellData currentCell = _currentMap.GetCell(_player.LogicX, _player.LogicY);
                if (currentCell != null && currentCell.value == 1)
                {
                    EntranceData ceilingEntrance = _currentMap.GetEntranceAt(_player.LogicX, _player.LogicY);
                    if (ceilingEntrance != null)
                    {
                        StartCoroutine(JumpUpRoutine(ceilingEntrance));
                        return;
                    }
                }
                
                // 설정된 입구 데이터가 없다면 원래의 상태로 돌아옴
                StartCoroutine(TransitionLookState(LookState.None));
                return;
            }
            else if (_currentLookState == LookState.Down)
            {
                Vector2Int fwd = _player.GetForwardVector();
                int tx = _player.LogicX + fwd.x;
                int ty = _player.LogicY + fwd.y;

                CellData targetCell = _currentMap.GetCell(tx, ty);
                if (targetCell != null && targetCell.value == -1)
                {
                    EntranceData holeEntrance = _currentMap.GetEntranceAt(tx, ty);
                    if (holeEntrance != null)
                    {
                        StartCoroutine(JumpDownRoutine(holeEntrance, fwd));
                        return;
                    }
                }

                // 설정된 입구 데이터가 없다면 원래의 상태로 돌아옴
                StartCoroutine(TransitionLookState(LookState.None));
                return;
            }

            // 정면을 보고 있을 때, 현재 서 있는 칸의 천장이 void(1)인지 확인
            CellData myCell = _currentMap.GetCell(_player.LogicX, _player.LogicY);
            if (myCell != null && myCell.value == 1)
            {
                StartCoroutine(TransitionLookState(LookState.Up));
                return;
            }

            // 정면(LookState.None)을 보고 있을 때의 탐색 동작
            Vector2Int forward = _player.GetForwardVector();
            int frontX = _player.LogicX + forward.x;
            int frontY = _player.LogicY + forward.y;

            // 앞 칸에 고정 오브젝트(보물상자 등)가 있는지 확인
            MapObject targetObj = _staticObjects.Find(o => Mathf.FloorToInt(o.x) == frontX && Mathf.FloorToInt(o.y) == frontY && o.isActive);
            
            if (targetObj != null)
            {
                Debug.Log($"오브젝트 상호작용 발생: {targetObj.objectId}");
                
                // TODO
                // 이벤트 매니저를 통해 대화창이나 보상 획득 UI 띄우기
                // targetObj.texIdx를 열린 상자 텍스처로 변경
                // targetObj.isSolid = false 로 변경하여 지나갈 수 있게 만들기
                // UpdateSpriteData() 호출하여 화면 갱신
                
                return;
            }

            // 바닥 구멍 체크
            CellData frontCell = _currentMap.GetCell(frontX, frontY);
            
            if (frontCell != null && frontCell.value == -1)
            {
                StartCoroutine(TransitionLookState(LookState.Down));
                return;
            }

            Debug.Log("ACTION 버튼 클릭됨");
            // TODO: 추후 상호작용(문 열기, NPC 대화, 전방의 아이템 조사 등) 로직 연결
        }

        // 입구 데이터 확인 메서드
        private EntranceData CheckForEntrance(int currentX, int currentY, int targetX, int targetY, Vector2Int moveDir)
        {
            if (_currentMap == null) return null;

            Direction inputDir = VectorToDirection(moveDir);

            // 방 안쪽 벽에 있는 입구인지 체크
            EntranceData currentEntrance = _currentMap.GetEntranceAt(currentX, currentY);
            if (currentEntrance != null && currentEntrance.isWallEntrance && currentEntrance.triggerDirection == inputDir)
            {
                return currentEntrance;
            }

            // 진입 시 방 바깥쪽 벽에 있는 입구인지 체크 (맵 범위를 벗어나지 않았을 때만 체크함)
            if (targetX >= 0 && targetX < _currentMap.width && targetY >= 0 && targetY < _currentMap.height)
            {
                EntranceData targetEntrance = _currentMap.GetEntranceAt(targetX, targetY);
                if (targetEntrance != null && targetEntrance.isWallEntrance && targetEntrance.triggerDirection == inputDir)
                {
                    return targetEntrance;
                }
            }

            return null;
        }

        // 레벨 전환 및 상점 진입 코루틴
        private IEnumerator TransitionToOtherPlace(EntranceData entrance, Vector2Int moveDir)
        {
            _inputLocked = true; // 입력 잠금

            // 애니메이션으로 논리 좌표가 바뀌기 전의 타일 좌표
            int preEntranceLogicX = _player.LogicX;
            int preEntranceLogicY = _player.LogicY;

            // 애니메이션 스타또!
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                float duration = 0.5f; 
                
                float startX = _player.PosX;
                float startY = _player.PosY;

                int targetGridX = _player.LogicX + moveDir.x;
                int targetGridY = _player.LogicY + moveDir.y;
                
                Vector2 targetPos = _player.GetOffsetPosition(targetGridX, targetGridY, _player.DirectionIdx);

                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = true;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    
                    fadeOverlay.alpha = t;

                    // 이때 논리 좌표 변경됨
                    _player.SetDirectPosition(
                        Mathf.Lerp(startX, targetPos.x, t),
                        Mathf.Lerp(startY, targetPos.y, t),
                        _player.DirectionIdx
                    );

                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            yield return new WaitForSeconds(0.2f);

            if (entrance.type == EntranceType.Map)
            {
                if (DungeonEventManager.Instance) {}
                    DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
                if (DungeonManager.Instance) 
                    DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
                LoadMapData(entrance); 
                yield return null; 

                if (fadeOverlay != null)
                {
                    float elapsed = 0f;
                    float duration = 0.5f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                        yield return null;
                    }
                    fadeOverlay.alpha = 0f;
                    fadeOverlay.blocksRaycasts = false;
                }
            }
            else if (entrance.type == EntranceType.Shop)
            {
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.ShowShop(entrance.destinationID);
                }

                // 상점이 열려있는 동안 코루틴 대기
                yield return new WaitUntil(() => GameStateManager.Instance.CurrentState != GameState.Shop);

                // 상점을 나설 때 180도 회전
                int reverseDir = (_player.DirectionIdx + 2) % 4; 
                
                // 뒤집힌 방향을 기준으로 원래 위치의 오프셋을 다시 계산
                Vector2 originalPos = _player.GetOffsetPosition(preEntranceLogicX, preEntranceLogicY, reverseDir);
                _player.SetDirectPosition(originalPos.x, originalPos.y, reverseDir);

                // 회전한 방향에 맞춰 나침반과 미니맵도 즉시 동기화
                if (compassUI) compassUI.SetDirection(reverseDir);
                if (miniMap) miniMap.SnapToGrid(preEntranceLogicX, preEntranceLogicY, reverseDir);
                
                UpdateMapDiscovery(preEntranceLogicX, preEntranceLogicY);

                // 페이드인
                if (fadeOverlay != null)
                {
                    float elapsedFade = 0f;
                    float fadeDuration = 0.5f;
                    
                    while (elapsedFade < fadeDuration)
                    {
                        elapsedFade += Time.deltaTime;
                        fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsedFade / fadeDuration);
                        yield return null;
                    }
                    fadeOverlay.alpha = 0f;
                    fadeOverlay.blocksRaycasts = false;
                }
            }

            _inputLocked = false;
        }

        private Direction VectorToDirection(Vector2Int dirVec)
        {
            if (dirVec.y == 1) return Direction.North;
            if (dirVec.x == 1) return Direction.East;
            if (dirVec.y == -1) return Direction.South;
            if (dirVec.x == -1) return Direction.West;
            return Direction.North;
        }

        private void TryMove(int forwardSign)
        {
            Vector2Int fwd = _player.GetForwardVector() * forwardSign;
            PerformMove(fwd);
        }

        private void TryStrafe(int rightSign)
        {
            Vector2Int rightVec = _player.GetRightVector() * rightSign;
            PerformMove(rightVec);
        }

        private IEnumerator TurnRoutine(int dirStep)
        {
            // 현재 방향과 이동할 다음 방향을 미리 계산
            int currentDir = _player.DirectionIdx;
            // (a % n + n) % n 은 음수 나머지 처리를 위한 공식.
            int nextDir = ((currentDir + dirStep) % 4 + 4) % 4;

            if (compassUI)
                compassUI.AnimateTurn(currentDir, nextDir, dirStep, turnDuration);
            
            if (miniMap)
                miniMap.SetDirection(nextDir, turnDuration);

            yield return StartCoroutine(_player.RotateGridRoutine(dirStep, turnDuration, null));
            
            // 맵 갱신
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
        }

        // ================= Map & Game Logic =================
        private void LoadMapData(EntranceData entryEntrance = null)
        {
            _currentMap = DungeonManager.Instance.CurrentDungeonData;
            DungeonTheme theme = DungeonManager.Instance.GetDungeonTheme(_currentMap.themeName);
            
            SoundManager.Instance.PlayBGM(theme.bgmID);

            renderSettings.useGridLighting = theme.useGridLighting;
            renderSettings.lightingIntensity = theme.lightingIntensity;
            renderSettings.fogColor = theme.fogColor;
            
            if (backgroundImage != null) backgroundImage.texture = theme.background;
            
            // 시스템 초기화
            _renderer.LoadAssets(theme.texture, theme.spriteTextures, 64, 64, null);
            encounterSystem.Initialize(theme.monsterList);

            // 플레이어 위치 초기화
            if (entryEntrance != null)
            {
                _currentMap.startDirection = entryEntrance.targetDirection;
                _currentMap.startX = entryEntrance.targetX;
                _currentMap.startY = entryEntrance.targetY;
            }

            if (DungeonEventManager.Instance)
                DungeonEventManager.Instance.SetCurrentMapID(_currentMap.mapID);
            
            _player.SetMapData(_currentMap, _currentMap.startX, _currentMap.startY, _currentMap.startDirection);

            
            RefreshAppVisible();
            
            // 벽 애니메이션 초기화
            InitializeWallAnims(theme);
            _renderer.SetMapData(_currentMap, theme, _tileAnimStates);
            
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            _maxSpawnCount = theme.maxSpawnCount;
            _spawnDelay = theme.spawnDelay;
            _currentSpawnTimer = 0f;
            _activeEnemies.Clear();

            SpawnSymbolEnemies(theme.maxSpawnCount);
        }

        private void RefreshAppVisible()
        {
            if (miniMap != null)
            {
                miniMap.Initialize(_currentMap);
                miniMap.gameObject.SetActive(ModuleManager.Instance.IsMounted(ModuleFeature.LocalRadar));
            }
            if (compassUI != null)
            {
                compassUI.SetDirection(_player.DirectionIdx);
                compassUI.gameObject.SetActive(ModuleManager.Instance.IsMounted(ModuleFeature.GyroCompass));   
            }
            if (autoMapContainer != null)
            {
                autoMapContainer.SetActive(false);
                autoMapRenderer.DrawFullMap(_currentMap, DungeonManager.Instance.CurrentDungeonState);
            }
            if (weatherUI != null)
            {
                weatherUI.gameObject.SetActive(ModuleManager.Instance.IsMounted(ModuleFeature.WeatherWidget));
            }
        }

        private void OnPlayerStep()
        {
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            // 랜덤 인카운터 계산
            //encounterSystem.OnStepTaken();
            
            // 이벤트가 있는지 체크
            string eventID = DungeonEventManager.Instance.CheckEvent(_player.LogicX, _player.LogicY);
            if (!string.IsNullOrEmpty(eventID))
            {
                GameStateManager.Instance.StartEventDialogue(eventID);
            }
        }

        private void UpdateMapDiscovery(int x, int y)
        {
            DungeonManager.Instance.CurrentDungeonState.MarkVisited(x, y);
            autoMapRenderer.RevealCell(x, y);
            autoMapRenderer.UpdatePlayerIcon(x, y, (Direction)_player.DirectionIdx);
            DungeonMapStateManager.Instance.UpdatePlayerPosition(x, y, (Direction)_player.DirectionIdx, _currentMap.mapID);
        }

        private void InitializeWallAnims(DungeonTheme theme)
        {
            if (theme == null || theme.wallAnimations == null) return;
            _tileAnimStates = new TileAnimState[_currentMap.width, _currentMap.height];

            Dictionary<int, WallAnimConfig> animDict = new Dictionary<int, WallAnimConfig>();
            foreach (var cfg in theme.wallAnimations)
                if (!animDict.ContainsKey(cfg.baseTexId)) animDict.Add(cfg.baseTexId, cfg);

            if (animDict.Count == 0) return;

            for (int x = 0; x < _currentMap.width; x++)
            {
                for (int y = 0; y < _currentMap.height; y++)
                {
                    CellData cell = _currentMap.GetCell(x, y);
                    if (cell != null && cell.HasWall())
                    {
                        foreach (int texID in cell.wallTextureIDs)
                        {
                            if (animDict.ContainsKey(texID))
                            {
                                _tileAnimStates[x, y] = new TileAnimState
                                {
                                    isAnimating = true,
                                    config = animDict[texID],
                                    showAlt = false,
                                    timer = UnityEngine.Random.Range(animDict[texID].minInterval, animDict[texID].maxInterval)
                                };
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void UpdateWallAnimations()
        {
            if (_tileAnimStates == null) return;
            float dt = Time.deltaTime;

            for (int x = 0; x < _tileAnimStates.GetLength(0); x++)
            {
                for (int y = 0; y < _tileAnimStates.GetLength(1); y++)
                {
                    TileAnimState st = _tileAnimStates[x, y];
                    if (st != null && st.isAnimating)
                    {
                        st.timer -= dt;
                        if (st.timer <= 0)
                        {
                            st.showAlt = !st.showAlt;
                            st.timer = UnityEngine.Random.Range(st.config.minInterval, st.config.maxInterval);
                        }
                    }
                }
            }
        }

        private IEnumerator ScanRoutine()
        {
            if (_isScanning) yield break;
            _isScanning = true;
            
            float radius = 0f;
            // 스캔 퍼짐
            while (radius < renderSettings.maxScanDistance)
            {
                radius += Time.deltaTime * renderSettings.scanSpeed;
                _renderer.SetScanState(true, radius);
                yield return null;
            }
            radius = renderSettings.maxScanDistance;
            
            // 유지
            yield return new WaitForSeconds(renderSettings.scanWaitTime);
            
            while (radius > 0f)
            {
                radius -= Time.deltaTime * renderSettings.scanSpeed * renderSettings.returnSpeedMultiplier;
                _renderer.SetScanState(true, radius);
                yield return null;
            }

            _isScanning = false;
            _renderer.SetScanState(false, 0f);
        }

        private void UpdateBackgroundUV()
        {
            if (!backgroundImage) return;
            float angle = Mathf.Atan2(_player.DirY, _player.DirX) * Mathf.Rad2Deg;
            Rect uv = backgroundImage.uvRect;
            uv.x = -angle / 360f;
            backgroundImage.uvRect = uv;
        }

        // 맵의 빈 공간에 적 심볼 생성
        private void SpawnSymbolEnemies(int count)
        {
            int spawned = 0;
            int maxAttempts = count * 50; 
            
            // 플레이어로부터 최소 몇 칸 떨어져서 스폰될지 결정
            float safeDistance = 3.0f; 

            while(spawned < count && maxAttempts > 0)
            {
                maxAttempts--;
                int rx = UnityEngine.Random.Range(1, _currentMap.width - 1);
                int ry = UnityEngine.Random.Range(1, _currentMap.height - 1);
                
                CellData cell = _currentMap.GetCell(rx, ry);
                
                // 생성하려는 좌표와 현재 플레이어 위치 사이의 거리를 계산
                float distToPlayer = Vector2.Distance(new Vector2(rx, ry), new Vector2(_player.LogicX, _player.LogicY));
                
                // 거리가 safeDistance 이상일 때만 스폰을 허용
                if (cell != null && !cell.HasWall() && cell.value != -1 && distToPlayer >= safeDistance)
                {
                    _activeEnemies.Add(new MapEnemy { 
                        x = rx + 0.5f, 
                        y = ry + 0.5f, 
                        targetX = rx + 0.5f, 
                        targetY = ry + 0.5f,
                        direction = UnityEngine.Random.Range(0, 4),
                        baseTexIdx = 0,
                        currentTexIdx = 0,
                        moveInterval = UnityEngine.Random.Range(1.2f, 1.8f) 
                    }); 
                    spawned++;
                }
            }
            UpdateSpriteData();
        }

        private void UpdateEnemySprites()
        {
            float animSpeed = 0.3f; // 1프레임당 지속 시간
            bool needsRenderUpdate = false;

            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.isAlive) continue;

                // 걷기 애니메이션 타이머
                if (enemy.isMoving)
                {
                    enemy.animTimer += Time.deltaTime;
                    if (enemy.animTimer >= animSpeed)
                    {
                        enemy.animTimer -= animSpeed;
                        enemy.animFrame = (enemy.animFrame + 1) % 2;
                        needsRenderUpdate = true;
                    }
                }

                // 플레이어가 적을 바라보는 각도 계산
                float dx = _player.PosX - enemy.x;
                float dy = _player.PosY - enemy.y;
                float angleToPlayer = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                // 적이 현재 바라보고 있는 절대 각도 (0:N, 1:E, 2:S, 3:W)
                float facingAngle = 0f;
                if (enemy.direction == 0) facingAngle = 90f;  // North
                if (enemy.direction == 1) facingAngle = 0f;   // East
                if (enemy.direction == 2) facingAngle = -90f; // South
                if (enemy.direction == 3) facingAngle = 180f; // West

                // 두 각도의 차이를 계산하여 보이는 면 결정
                float diff = Mathf.DeltaAngle(angleToPlayer, facingAngle);

                int viewSide = 0; // 0: 정면, 1: 뒷면, 2: 우측면, 3: 좌측면

                if (diff >= -45f && diff <= 45f) viewSide = 0; // 정면
                else if (diff > 45f && diff <= 135f) viewSide = 3; // 우측면
                else if (diff < -45f && diff >= -135f) viewSide = 2; // 좌측면
                else viewSide = 1; // 뒷면

                // 최종 텍스처 인덱스 도출 (방향 오프셋, 애니메이션 프레임)
                int offset = (viewSide * 2) + enemy.animFrame;
                int newTexIdx = enemy.baseTexIdx + offset;

                // 텍스처가 바뀌었을 때만 렌더러 갱신 요청
                if (enemy.currentTexIdx != newTexIdx)
                {
                    enemy.currentTexIdx = newTexIdx;
                    needsRenderUpdate = true;
                }
            }

            // 변경 사항이 생겼을 때만 그래픽 데이터 갱신
            if (needsRenderUpdate) UpdateSpriteData();
        }

        // 몬스터 충돌 시 서로를 바라보는 방향을 바탕으로 선공권을 계산
        private EncounterType DetermineEncounterAdvantage(MapEnemy enemy, bool playerInitiated)
        {
            int px = _player.LogicX;
            int py = _player.LogicY;
            int ex = Mathf.FloorToInt(enemy.x);
            int ey = Mathf.FloorToInt(enemy.y);

            // 서로를 향하는 방향 벡터 도출 (0:N, 1:E, 2:S, 3:W)
            int dirToEnemy = (int)VectorToDirection(new Vector2Int(ex - px, ey - py));
            int dirToPlayer = (int)VectorToDirection(new Vector2Int(px - ex, py - ey));

            // 플레이어는 적을 보고 있는가? / 적은 플레이어를 보고 있는가?
            bool playerFacesEnemy = (_player.DirectionIdx == dirToEnemy);
            bool enemyFacesPlayer = (enemy.direction == dirToPlayer);

            if (playerFacesEnemy && enemyFacesPlayer) 
                return EncounterType.Normal; // 서로 정면 충돌

            if (playerFacesEnemy && !enemyFacesPlayer) 
                return EncounterType.Preemptive; // 플레이어가 적의 옆/뒤를 덮침

            if (!playerFacesEnemy && enemyFacesPlayer) 
                return EncounterType.Ambush; // 적이 플레이어의 옆/뒤를 덮침

            // 둘 다 서로를 안 볼 때 (뒷걸음질로 서로 부딪힌 경우) 먼저 부딪힌 쪽이 유리하게 판정
            return EncounterType.Normal;
        }

        // 몬스터의 이동 처리
        private void ProcessEnemyTurn(MapEnemy enemy)
        {
            int ex = Mathf.FloorToInt(enemy.targetX);
            int ey = Mathf.FloorToInt(enemy.targetY);
            int px = _player.LogicX;
            int py = _player.LogicY;

            float dist = Vector2.Distance(new Vector2(ex, ey), new Vector2(px, py));
            Vector2Int moveDir = Vector2Int.zero;

            // 거리에 따라 추적할지 배회할지 결정
            if (dist <= enemy.aggroRange && dist > 0)
                moveDir = GetChaseDirection(ex, ey, px, py);
            else
                moveDir = GetRandomWanderDirection(ex, ey);

            // 이동할 방향이 정해졌다면 목표 좌표 갱신
            if (moveDir != Vector2Int.zero)
            {
                int nextX = ex + moveDir.x;
                int nextY = ey + moveDir.y;

                // 몬스터가 플레이어의 자리로 이동하려 한다면 기습 발동
                if (nextX == px && nextY == py)
                {
                    enemy.direction = (int)VectorToDirection(moveDir);

                    EncounterType encType = DetermineEncounterAdvantage(enemy, false);
                    StartCoroutine(SymbolEncounterRoutine(enemy, -moveDir, encType));
                    return;
                }

                enemy.targetX = nextX + 0.5f;
                enemy.targetY = nextY + 0.5f;
                enemy.direction = (int)VectorToDirection(moveDir);
            }
        }

        // 플레이어 쪽으로 다가가는 경로 계산
        private Vector2Int GetChaseDirection(int ex, int ey, int px, int py)
        {
            int dx = px - ex;
            int dy = py - ey;

            List<Vector2Int> preferredDirs = new List<Vector2Int>();
            
            // X축 거리와 Y축 거리 중 더 먼 쪽을 먼저 좁히려고 시도
            if (Mathf.Abs(dx) > Mathf.Abs(dy))
            {
                if (dx != 0) preferredDirs.Add(new Vector2Int((int)Mathf.Sign(dx), 0));
                if (dy != 0) preferredDirs.Add(new Vector2Int(0, (int)Mathf.Sign(dy)));
            }
            else
            {
                if (dy != 0) preferredDirs.Add(new Vector2Int(0, (int)Mathf.Sign(dy)));
                if (dx != 0) preferredDirs.Add(new Vector2Int((int)Mathf.Sign(dx), 0));
            }

            foreach (var dir in preferredDirs)
            {
                if (CanEnemyMove(ex, ey, dir)) return dir;
            }
            // 모든 길이 막혔다면 제자리
            return Vector2Int.zero;
        }

        // 4방향 중 무작위 한 곳으로 이동 시도
        private Vector2Int GetRandomWanderDirection(int ex, int ey)
        {
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            int startIdx = UnityEngine.Random.Range(0, 4);
            
            for (int i = 0; i < 4; i++)
            {
                Vector2Int dir = dirs[(startIdx + i) % 4];
                if (CanEnemyMove(ex, ey, dir)) return dir;
            }
            return Vector2Int.zero;
        }

        // 몬스터가 해당 칸으로 들어갈 수 있는지 검사
        private bool CanEnemyMove(int ex, int ey, Vector2Int dir)
        {
            int tx = ex + dir.x;
            int ty = ey + dir.y;

            // 플레이어 자리는 기습을 위해 진입 가능으로 판단
            if (tx == _player.LogicX && ty == _player.LogicY) return true;

            // 맵 밖 방지
            if (tx < 0 || tx >= _currentMap.width || ty < 0 || ty >= _currentMap.height) return false;

            // 벽과 구멍 방지 (몬스터는 기본적으로 일루전 월을 통과하지 않음)
            CellData targetCell = _currentMap.GetCell(tx, ty);
            if (targetCell == null || targetCell.value == -1) return false;
            if (targetCell.HasWall()) return false; 

            // 다른 몬스터와 겹치기 방지
            foreach(var other in _activeEnemies)
            {
                if (other.isAlive && Mathf.FloorToInt(other.targetX) == tx && Mathf.FloorToInt(other.targetY) == ty)
                    return false;
            }

            return true;
        }

        // 몬스터 자동 리스폰 로직
        private void UpdateEnemySpawner()
        {
            // 스폰 딜레이가 0 이하거나, 이미 최대치만큼 적이 있다면 타이머 정지
            if (_spawnDelay <= 0f || _activeEnemies.Count >= _maxSpawnCount)
            {
                _currentSpawnTimer = 0f;
                return;
            }

            // 플레이어가 이동/탐험 중일 때만 시간이 흐름
            if (!_inputLocked)
            {
                _currentSpawnTimer += Time.deltaTime;
                
                // 설정된 딜레이 시간이 지나면
                if (_currentSpawnTimer >= _spawnDelay)
                {
                    _currentSpawnTimer -= _spawnDelay;
                    SpawnSymbolEnemies(1);
                }
            }
        }

        private void UpdateEnemyAI()
        {
            // 플레이어가 대화 중이거나 이동 중일 때는 적들도 움직이지 않고 대기
            if (_inputLocked || _player.IsMoving) return;

            float dt = Time.deltaTime;

            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.isAlive) continue;

                // 부드러운 이동
                if (Mathf.Abs(enemy.x - enemy.targetX) > 0.01f || Mathf.Abs(enemy.y - enemy.targetY) > 0.01f)
                {
                    enemy.x = Mathf.MoveTowards(enemy.x, enemy.targetX, enemy.moveSpeed * dt);
                    enemy.y = Mathf.MoveTowards(enemy.y, enemy.targetY, enemy.moveSpeed * dt);
                    enemy.isMoving = true;
                }
                else
                {
                    enemy.x = enemy.targetX;
                    enemy.y = enemy.targetY;
                    enemy.isMoving = false;
                    enemy.animFrame = 0; // 멈추면 대기 자세
                }

                // AI 행동 쿨타임 계산
                enemy.moveTimer -= dt;
                if (enemy.moveTimer <= 0f)
                {
                    // 타이머 리셋. 약간의 랜덤 엇박자를 주어 로봇처럼 동시에 움직이지 않게 함
                    enemy.moveTimer = enemy.moveInterval + UnityEngine.Random.Range(-0.2f, 0.2f);
                    ProcessEnemyTurn(enemy);
                }
            }
        }

        private void UpdateSpriteData()
        {
            List<SpriteInfo> spriteList = new List<SpriteInfo>();

            // 에너미 데이터 추가
            foreach (var enemy in _activeEnemies)
            {
                if (enemy.isAlive)
                {
                    spriteList.Add(new SpriteInfo { 
                        x = enemy.x, 
                        y = enemy.y, 
                        texIdx = enemy.currentTexIdx 
                    });
                }
            }

            // 고정 오브젝트 데이터 추가
            foreach (var obj in _staticObjects)
            {
                if (obj.isActive)
                {
                    spriteList.Add(new SpriteInfo { 
                        x = obj.x, 
                        y = obj.y, 
                        texIdx = obj.texIdx 
                    });
                }
            }

            // 통합된 리스트를 렌더러에 전달
            _renderer.UpdateSprites(spriteList.ToArray());
        }

        private void OnGameStateChanged(GameState newState)
        {
            _canRender = (newState == GameState.Exploration);
            if (!_canRender) return;
            
            SoundManager.Instance.PlayBGM(DungeonManager.Instance.GetDungeonTheme(_currentMap.themeName).bgmID);
            RefreshAppVisible();
        }
    }
}