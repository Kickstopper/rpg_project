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
using DG.Tweening;
using UnityEngine.SceneManagement;

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
        public GameObject roomNamePanel;
        public TextMeshProUGUI roomNameText;
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
        private bool monsterExist;
        private int _maxSpawnCount = 0;
        private float _spawnDelay = 0f;
        private float _currentSpawnTimer = 0f;

        // 서브 시스템
        private RaycastRenderEngine _renderer;
        private DungeonPlayer _player;
        
        // 상태
        private TileAnimState[,] _tileAnimStates;
        private MapData _currentMap;
        private DungeonTheme theme;

        // 바닥/천장 애니메이션 전용 상태 변수
        private TileAnimState _floorAnimState;
        private TileAnimState _ceilAnimState;
        private int _currentFloorTexIdx;
        private int _currentCeilTexIdx;

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
            _inputLocked = true;

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

            // 씬이 시작되자마자 화면을 검게 가림
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1f;
                fadeOverlay.blocksRaycasts = true;
            }

            // 맵 초기화
            LoadMapData();
            
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            GameStateManager.Instance.ChangeState(GameState.Exploration);

            if (theme != null && theme.useWakeUpEffect)
                StartCoroutine(WakeUpFadeInRoutine());
            else
                StartCoroutine(InitialFadeInRoutine());
        }

        // 씬 진입 시 최초 페이드 인
        private IEnumerator InitialFadeInRoutine()
        {
            if (fadeOverlay == null) yield break;

            _inputLocked = true;
            yield return new WaitForSeconds(0.1f);

            yield return fadeOverlay.DOFade(0f, 1f).WaitForCompletion();

            fadeOverlay.blocksRaycasts = false;
            _inputLocked = false;

            CheckCurrentTileEvent();
        }

        // 잠에서 깨어나는 눈 깜빡임 연출
        private IEnumerator WakeUpFadeInRoutine()
        {
            if (fadeOverlay == null) yield break;

            _inputLocked = true;
            fadeOverlay.alpha = 1f;
            fadeOverlay.blocksRaycasts = true;

            yield return new WaitForSeconds(1f);

            Sequence seq = DOTween.Sequence();
            
            // 첫 번째 깜빡임
            seq.Append(fadeOverlay.DOFade(0.4f, 2f).SetEase(Ease.InOutSine));
            seq.Append(fadeOverlay.DOFade(1f, 0.1f).SetEase(Ease.InOutSine));
            seq.AppendInterval(0.2f);
            
            // 두 번째 깜빡임
            seq.Append(fadeOverlay.DOFade(0.1f, 0.2f).SetEase(Ease.InOutSine));
            seq.Append(fadeOverlay.DOFade(1f, 0.1f).SetEase(Ease.InOutSine));
            seq.AppendInterval(0.3f);
            
            // 완전히 눈을 뜸
            seq.Append(fadeOverlay.DOFade(0f, 1f).SetEase(Ease.InOutSine));

            // 시퀀스가 모두 끝날 때까지 대기
            yield return seq.WaitForCompletion();

            fadeOverlay.blocksRaycasts = false;
            _inputLocked = false;

            CheckCurrentTileEvent();
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

            if (!_inputLocked && theme.moduleEnable && (Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown()))
            {
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
                inputCooldown = 0.2f;
                return;
            }
            
            if (!_inputLocked && Input.GetKeyDown(KeyCode.M))
            {
                if (!theme.moduleEnable || !ModuleManager.Instance.IsMounted(ModuleFeature.AutoMapper)) return;
                autoMapContainer.SetActive(!autoMapContainer.activeSelf);
                return;
            }

            if (!_inputLocked) HandleInput(); // 그리드 입력

            UpdateWallAnimations();
            
            // 심볼 몬스터 로직은 랜덤 인카운터 모드가 아닐 때만 실행
            if (monsterExist && _maxSpawnCount > 0 && theme.encounterMode != EncounterMode.Random)
            {
                // 몬스터 심볼의 이동과 스폰
                UpdateEnemyAI();
                UpdateEnemySprites();
                UpdateEnemySpawner();
                UpdateEncounterSensor(); // 위험도 센서 실시간 갱신
            }

            _renderer.RenderFrame(_player, renderSettings);
            UpdateBackgroundUV();
        }

        // 모드 전환 메서드
        // private void ToggleMovementMode()
        // {
        //     if (_player.IsGridMove)
        //     {
        //         _player.IsGridMove = false;
        //         Debug.Log("Switched to Free Move");
        //     }
        //     else
        //     {
        //         // 그리드 상태로 스냅핑
        //         _player.SnapToGrid();
        //         _player.IsGridMove = true;
                
        //         // 스냅 후 미니맵/아이콘 동기화
        //         if (miniMap) miniMap.SnapToGrid(_player.LogicX, _player.LogicY, _player.DirectionIdx);
        //         if (compassUI) compassUI.SetDirection(_player.DirectionIdx);
        //         UpdateMapDiscovery(_player.LogicX, _player.LogicY);
                
        //         Debug.Log("Switched to Grid Move");
        //     }
        // }

        // 자유 이동 입력 처리
        // private void HandleFreeMoveInput()
        // {
        //     if (_inputLocked) return;

        //     float moveSpeed = Time.deltaTime * 3.0f; // 속도 조절
        //     float rotSpeed = Time.deltaTime * 2.0f;

        //     // 점프
        //     //if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));
        //     // 스캔
        //     if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());

        //     // 이동
        //     if (Input.GetKey(KeyCode.W)) _player.MoveFree(moveSpeed);
        //     if (Input.GetKey(KeyCode.S)) _player.MoveFree(-moveSpeed);

        //     if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) 
        //     {
        //         _player.RotateFree(rotSpeed); // 왼쪽 회전
        //     }
        //     if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) 
        //     {
        //         _player.RotateFree(-rotSpeed); // 오른쪽 회전
        //     }

        //     // Free Move 시 미니맵 갱신 (즉시 갱신)
        //     if (miniMap) miniMap.SetFreeDirection(_player.DirX, _player.DirY);
        //     autoMapRenderer.UpdatePlayerIconFree(_player.PosX, _player.PosY, _player.DirX, _player.DirY);
        // }

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
            // if (Input.GetKey(KeyCode.LeftShift) && !_player.IsMoving)
            // {
            //     if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            //     {
            //         StartCoroutine(TransitionLookState(LookState.Up));
            //         return;
            //     }
            //     if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            //     {
            //         StartCoroutine(TransitionLookState(LookState.Down));
            //         return;
            //     }
            // }

            // 상호작용 (올려보기, 내려보기, 보물상자 열기 등)
            if (!_player.IsMoving && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                UI_Action();
                return;
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
            
            if (targetState == LookState.None) HideSystemMessage();
            else if (targetState == LookState.Up)
            {
                EntranceData entrance = _currentMap.GetEntranceAt(_player.LogicX, _player.LogicY);
                if (entrance != null) ShowSystemMessage("올라갈 수 있을 것 같다. 올라가시겠습니까?");
            }
            else if (targetState == LookState.Down)
            {
                Vector2Int fwd = _player.GetForwardVector();
                EntranceData entrance = _currentMap.GetEntranceAt(_player.LogicX + fwd.x, _player.LogicY + fwd.y);
                if (entrance != null) ShowSystemMessage("바닥이 보인다. 뛰어내리시겠습니까?");
            }

            float endPitch = (targetState == LookState.Up) ? -100f : (targetState == LookState.Down) ? 100f : 0f;
            float endOffset = (targetState == LookState.None || targetState == LookState.Up) ? this.backwardOffset : 0f;

            Sequence seq = DOTween.Sequence();
            
            seq.Join(DOTween.To(() => _player.Pitch, x => _player.Pitch = x, endPitch, 0.3f).SetEase(Ease.InOutQuad));
            seq.Join(DOTween.To(() => _player.BackwardOffset, x => _player.BackwardOffset = x, endOffset, 0.3f)
                .SetEase(Ease.InOutQuad)
                .OnUpdate(() => {
                    Vector2 updatedPos = _player.GetOffsetPosition(_player.LogicX, _player.LogicY, _player.DirectionIdx);
                    _player.SetDirectPosition(updatedPos.x, updatedPos.y, _player.DirectionIdx);
                }));

            yield return seq.WaitForCompletion();

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

            _player.BackwardOffset = this.backwardOffset;
            _currentLookState = LookState.None;

            if (entrance.isWorldMap)
            {
                if (DungeonEventManager.Instance)
                    DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);

                WorldManager.Instance.SetCurrentRegionTheme(entrance.destinationID);
                WorldManager.Instance.isLoadGame = true;
                
                var data = WorldManager.Instance.currentRegionTheme;
                WorldManager.Instance.loadedPosition = data.startPosition;
                
                SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                GameStateManager.Instance.ChangeState(GameState.Exploration);
                
                yield break;
            }
            else
            {
                // 기존의 일반 던전 레벨 전환 로직
                DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
                DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
                
                // 새로운 맵 로드
                LoadMapData(entrance);
                
                yield return new WaitForSeconds(0.1f);

                SoundManager.Instance.PlaySFX(SfxID.Fall);
                // 착지 흔들림 코루틴
                StartCoroutine(LandingImpactRoutine(10f));

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

            // 맵을 로드하기 전에 오프셋과 시점 상태를 먼저 원상 복구
            _player.BackwardOffset = this.backwardOffset; 
            _currentLookState = LookState.None;

            if (entrance.isWorldMap)
            {
                if (DungeonEventManager.Instance)
                    DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);

                WorldManager.Instance.SetCurrentRegionTheme(entrance.destinationID);
                WorldManager.Instance.isLoadGame = true;
                
                var data = WorldManager.Instance.currentRegionTheme;
                WorldManager.Instance.loadedPosition = data.startPosition;
                
                // 월드맵 씬 로드 및 상태 변경
                SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                GameStateManager.Instance.ChangeState(GameState.Exploration);
                
                // 씬이 파괴되므로 코루틴을 즉시 종료
                yield break; 
            }
            else
            {
                // 기존의 일반 던전 레벨 전환 로직
                DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
                DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
                
                // 새로운 맵 로드
                LoadMapData(entrance);
                
                yield return new WaitForSeconds(0.1f);

                SoundManager.Instance.PlaySFX(SfxID.Fall);
                // 착지 흔들림 코루틴
                StartCoroutine(LandingImpactRoutine(150f));

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

        private IEnumerator OpenDoorAndMoveRoutine(CellData doorCell, int tx, int ty, Vector2Int moveVec, DoorAnimConfig doorConfig)
        {
            _inputLocked = true;
            
            SoundManager.Instance.PlaySFX(SfxID.Slide_Door); 

            // 문이 열리는 애니메이션
            if (doorConfig.openFrameTexIds != null && doorConfig.openFrameTexIds.Length > 0)
            {
                for (int i = 0; i < doorConfig.openFrameTexIds.Length; i++)
                {
                    // 해당 셀의 4면 중 벽이 있는 면의 텍스처를 프레임 텍스처로 덮어씌움
                    for (int face = 0; face < 4; face++)
                    {
                        if (doorCell.wallTextureIDs[face] != -1) 
                            doorCell.wallTextureIDs[face] = doorConfig.openFrameTexIds[i];
                    }
                    
                    yield return new WaitForSeconds(doorConfig.animSpeed); 
                }
            }

            // // 문이 열렸으면 통과할 수 있게 벽 속성을 제거
            // for (int face = 0; face < 4; face++)
            // {
            //     doorCell.wallTextureIDs[face] = -1; // 텍스처 삭제
            // }
            // doorCell.value = 0; // 타일을 막힌 벽(1)에서 빈 공간(0)으로 변경

            // 전진 실행
            float duration = _player.IsRunning ? moveDuration / 2f : moveDuration;
            if (miniMap) miniMap.TranslateToNewPosition(tx, ty, duration);
            
            yield return StartCoroutine(_player.MoveGridRoutine(tx, ty, duration, null));

            _inputLocked = false;
        }

        // 문을 열고 맵을 전환하거나 상점으로 진입하는 연출
        private IEnumerator OpenDoorAndTransitionRoutine(CellData doorCell, EntranceData entrance, Vector2Int moveDir, DoorAnimConfig doorConfig)
        {
            _inputLocked = true;
            
            SoundManager.Instance.PlaySFX(SfxID.Slide_Door); 
            
            // 원래 문이 배치되어 있던 면과 타일의 원래 value를 저장
            bool[] originalDoorFaces = new bool[4];
            int originalValue = doorCell.value;
            for (int face = 0; face < 4; face++)
            {
                if (doorCell.wallTextureIDs[face] == doorConfig.closedTexId)
                {
                    originalDoorFaces[face] = true;
                }
            }

            // 문 열림 애니메이션 재생 (문이 있던 면만 교체)
            if (doorConfig.openFrameTexIds != null && doorConfig.openFrameTexIds.Length > 0)
            {
                for (int i = 0; i < doorConfig.openFrameTexIds.Length; i++)
                {
                    for (int face = 0; face < 4; face++)
                    {
                        if (originalDoorFaces[face]) 
                            doorCell.wallTextureIDs[face] = doorConfig.openFrameTexIds[i];
                    }
                    yield return new WaitForSeconds(doorConfig.animSpeed); 
                }
            }

            yield return new WaitForSeconds(0.1f);

            // // 통과를 위해 물리적 벽 속성 제거
            // for (int face = 0; face < 4; face++)
            // {
            //     if (originalDoorFaces[face])
            //         doorCell.wallTextureIDs[face] = -1;
            // }
            // doorCell.value = 0;

            // 코루틴이 파괴되기 전 시점에 실행할 복구 함수
            Action restoreDoorAction = () => {
                // 문을 다시 원래의 닫힘(closedTexId) 상태와 벽 속성으로 복구
                doorCell.value = originalValue;  // 원래 벽 속성 복구
                for (int face = 0; face < 4; face++)
                {
                    if (originalDoorFaces[face])
                        doorCell.wallTextureIDs[face] = doorConfig.closedTexId; // 원래 문 텍스처 복구
                }
            };

            yield return StartCoroutine(TransitionToOtherPlace(entrance, moveDir, restoreDoorAction));
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
            MapObject blockingObj = _staticObjects.Find(o => Mathf.FloorToInt(o.x) == tx && Mathf.FloorToInt(o.y) == ty && o.isActive && o.isSolid);
            
            if (blockingObj != null)
            {
                // 부딪히는 사운드와 애니메이션 재생 후 이동 막음
                StartCoroutine(_player.BumpRoutine(moveVec));
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
                // 앞을 막고 있는 타일의 문 여부를 체크
                CellData currentCell = _currentMap.GetCell(_player.LogicX, _player.LogicY);
                CellData targetCell = _currentMap.GetCell(tx, ty);

                int targetEnterFace = -1;
                int currentExitFace = -1;

                if (moveVec.x > 0)      { targetEnterFace = 3; currentExitFace = 1; }
                else if (moveVec.x < 0) { targetEnterFace = 1; currentExitFace = 3; }
                else if (moveVec.y > 0) { targetEnterFace = 2; currentExitFace = 0; }
                else if (moveVec.y < 0) { targetEnterFace = 0; currentExitFace = 2; }

                bool isBlockedByWall = false;
                CellData hitCell = null;
                int hitTexID = -1; 

                // 내벽 및 외벽 충돌 검사
                if (currentCell != null && currentExitFace != -1 && currentCell.HasWall() && currentCell.wallTextureIDs[currentExitFace] != -1)
                {
                    isBlockedByWall = true; hitCell = currentCell; hitTexID = currentCell.wallTextureIDs[currentExitFace];
                }
                else if (targetCell != null && targetEnterFace != -1 && targetCell.HasWall() && targetCell.wallTextureIDs[targetEnterFace] != -1)
                {
                    isBlockedByWall = true; hitCell = targetCell; hitTexID = targetCell.wallTextureIDs[targetEnterFace];
                }
                else if (targetCell == null)
                {
                    isBlockedByWall = true;
                }

                // 부딪힌 벽이 테마의 문으로 등록되어 있는지 확인
                DoorAnimConfig doorConfig = null;
                if (isBlockedByWall && hitTexID != -1)
                {
                    doorConfig = theme?.doorAnimations?.Find(d => d.closedTexId == hitTexID);
                }

                // 플레이어가 정면을 보고 이동 중인지 체크
                Direction inputDir = VectorToDirection(moveVec);
                Direction facingDir = (Direction)_player.DirectionIdx;
                bool isMovingForward = (inputDir == facingDir);

                EntranceData validEntrance = CheckForEntrance(_player.LogicX, _player.LogicY, tx, ty, moveVec);
                if (validEntrance != null)
                {
                    if (doorConfig != null && hitCell != null)
                    {
                        // 문을 열고 전환 코루틴 실행
                        StartCoroutine(OpenDoorAndTransitionRoutine(hitCell, validEntrance, moveVec, doorConfig));
                    }
                    else
                    {
                        // 문이 아닐 경우 즉시 전환
                        StartCoroutine(TransitionToOtherPlace(validEntrance, moveVec));
                    }
                }
                else
                {
                    if (isBlockedByWall)
                    {
                        // 문은 플레이어가 정면으로 전진 중일 때만 열리도록 제한
                        if (doorConfig != null && hitCell != null && isMovingForward)
                        {
                            // 문을 열고 한 칸 전진
                            StartCoroutine(OpenDoorAndMoveRoutine(hitCell, tx, ty, moveVec, doorConfig));
                        }
                        else
                        {
                            StartCoroutine(_player.BumpRoutine(moveVec));
                            SoundManager.Instance.PlaySFX(SfxID.Bump_Wall);
                        }
                    }
                }
            }
        }

        private IEnumerator SymbolEncounterRoutine(MapEnemy enemy, Vector2Int moveVec, EncounterType encType)
        {
            _inputLocked = true;

            // 적에게 부딪히는 연출 (벽 충돌과 같음)
            SoundManager.Instance.PlaySFX(SfxID.Bump_Wall); 
            yield return StartCoroutine(_player.BumpRoutine(moveVec));

            if (_currentLookState != LookState.None)
            {
                yield return StartCoroutine(TransitionLookState(LookState.None));
            }
            
            // 부딪히면 해당 적 심볼 삭제
            enemy.isAlive = false;
            _activeEnemies.Remove(enemy);
            UpdateSpriteData(); // 화면에서 스프라이트 제거
            yield return null;

            // 전투 개시 명령
            GameStateManager.Instance.StartEncounter(encounterSystem.MonsterCandidate, theme.fogColor, encType); 

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

        private void ShowRoomName(string message)
        {
            if (roomNamePanel != null) roomNamePanel.SetActive(true);
            if (roomNameText != null) roomNameText.text = message;
        }

        private void HideRoomName()
        {
            if (roomNamePanel != null) roomNamePanel.SetActive(false);
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

        // 특정 절대 방향(Direction enum)으로 회전
        public void UI_TurnToDirection(Direction targetDirection)
        {
            if (_inputLocked || _player.IsMoving) return;
            
            StartCoroutine(TurnToDirectionRoutine((int)targetDirection));
        }

        // 특정 절대 방향으로 회전
        public void UI_TurnToDirection(int targetDir)
        {
            if (_inputLocked || _player.IsMoving) return;
            
            // 입력값이 0~3 사이를 유지하도록
            targetDir = ((targetDir % 4) + 4) % 4; 
            
            StartCoroutine(TurnToDirectionRoutine(targetDir));
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

            // 현재 서있는 칸에 고정 오브젝트가 있는지 확인
            MapObject targetObj = _staticObjects.Find(o => 
                Mathf.FloorToInt(o.x) == _player.LogicX && 
                Mathf.FloorToInt(o.y) == _player.LogicY && 
                o.isActive);
            
            // 만약 현재의 칸에 없다면 앞 칸을 확인
            // if (targetObj == null)
            // {
            //     targetObj = _staticObjects.Find(o => 
            //         Mathf.FloorToInt(o.x) == frontX && 
            //         Mathf.FloorToInt(o.y) == frontY && 
            //         o.isActive);
            // }
            
            if (targetObj != null)
            {
                // 닫힌 보물상자 (Object ID: 0) 상호작용
                if (targetObj.texIdx == 0)
                {
                    // 상태 및 텍스처 변경 (열린 상자의 ID인 1로 변경)
                    targetObj.texIdx = 1;
                    
                    // 열린 상자는 충돌하지 않음
                    // targetObj.isSolid = false;

                    // 변경된 텍스처를 렌더러에 즉시 반영
                    UpdateSpriteData();

                    // SoundManager.Instance.PlaySFX(SfxID.Open_Chest);
                    ShowSystemMessage("낡은 보물상자를 열었다.");

                    // TODO: 획득한 아이템이 있을 경우, 인벤토리에 저장한다
                    // InventoryManager.Instance.AddMoney(100);
                }
                else if (targetObj.texIdx == 1)
                {
                    ShowSystemMessage("안은 텅 비어있다...");
                }
                else
                {
                    ShowSystemMessage("아무 반응이 없다.");
                }

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
            Direction facingDir = (Direction)_player.DirectionIdx;

            // 방 안쪽 벽에 있는 입구인지 체크
            EntranceData currentEntrance = _currentMap.GetEntranceAt(currentX, currentY);
            if (currentEntrance != null && currentEntrance.isWallEntrance)
            {
                // triggerDirection 일치 여부, 플레이어가 정면으로 전진 중인지 체크
                if (currentEntrance.triggerDirection == inputDir && inputDir == facingDir)
                    return currentEntrance;
            }

            // 진입 시 방 바깥쪽 벽에 있는 입구인지 체크 (맵 범위를 벗어나지 않았을 때만 체크함)
            if (targetX >= 0 && targetX < _currentMap.width && targetY >= 0 && targetY < _currentMap.height)
            {
                EntranceData targetEntrance = _currentMap.GetEntranceAt(targetX, targetY);
                if (targetEntrance != null && targetEntrance.isWallEntrance)
                {
                    if (targetEntrance.triggerDirection == inputDir && inputDir == facingDir)
                        return targetEntrance;
                }
            }

            return null;
        }

        // 현재 바라보는 방향에 상점 입구가 있는지 확인하고 UI를 갱신
        private void CheckFrontForShop()
        {
            if (_player == null || _currentMap == null) return;

            // 탐험 상태가 아닐 때는 무조건 숨김
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Exploration)
            {
                HideRoomName();
                return;
            }

            Vector2Int forwardVec = _player.GetForwardVector();
            int frontX = _player.LogicX + forwardVec.x;
            int frontY = _player.LogicY + forwardVec.y;

            // 정면에 입구가 있는지 확인
            EntranceData frontEntrance = CheckForEntrance(_player.LogicX, _player.LogicY, frontX, frontY, forwardVec);

            // 상점 입구가 맞다면 텍스트 표시, 아니면 숨김
            if (frontEntrance != null && frontEntrance.type == EntranceType.Shop)
            {
                var shopData = ShopManager.Instance.GetShopData(frontEntrance.destinationID);
                if (shopData != null) ShowRoomName(shopData.displayName);
            }
            else
            {
                HideRoomName();
            }
        }

        // 레벨 전환 및 상점 진입 코루틴
        private IEnumerator TransitionToOtherPlace(EntranceData entrance, Vector2Int moveDir, Action onFadeOutComplete = null)
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
            
            // 화면이 완전히 암전된 직후, 씬이나 맵이 변경되기 전에 콜백 실행
            onFadeOutComplete?.Invoke();

            if (entrance.type == EntranceType.Map)
            {
                if (DungeonEventManager.Instance) {}
                    DungeonEventManager.Instance.SetCurrentMapID(entrance.destinationID);
                
                if (entrance.isWorldMap)
                {
                    WorldManager.Instance.SetCurrentRegionTheme(entrance.destinationID);
                    WorldManager.Instance.isLoadGame = true;
                    
                    var data = WorldManager.Instance.currentRegionTheme;
                    WorldManager.Instance.loadedPosition = data.startPosition;
                    SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                    GameStateManager.Instance.ChangeState(GameState.Exploration);
                }
                else if (DungeonManager.Instance)
                {
                    DungeonManager.Instance.LoadDungeonFromJson(entrance.destinationID);
                    LoadMapData(entrance); 
                } 
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

            // 회전 후 정면에 상점이 있는지 확인
            CheckFrontForShop();
        }

        // 특정 절대 방향(0: 북, 1: 동, 2: 남, 3: 서)으로 회전
        private IEnumerator TurnToDirectionRoutine(int targetDir)
        {
            int currentDir = _player.DirectionIdx;

            // 이미 목표 방향을 바라보고 있다면 즉시 종료
            if (currentDir == targetDir) yield break;

            // 최단 회전 방향 계산 -1: 왼쪽, 1: 오른쪽, 2: 뒤로 돌기
            int dirStep = targetDir - currentDir;

            // 회전 최적화: 시계 반대 방향(-3)은 시계 방향(+1)과 같고, 시계 방향(+3)은 시계 반대 방향(-1)과 같음
            if (dirStep == -3) dirStep = 1;
            else if (dirStep == 3) dirStep = -1;

            if (Mathf.Abs(dirStep) == 2)
            {
                // 오른쪽으로 90도씩 두 번 회전
                yield return StartCoroutine(TurnRoutine(1));
                yield return StartCoroutine(TurnRoutine(1));
            }
            else
            {
                yield return StartCoroutine(TurnRoutine(dirStep));
            }
        }

        // ================= Map & Game Logic =================
        private void LoadMapData(EntranceData entryEntrance = null)
        {
            _currentMap = DungeonManager.Instance.CurrentDungeonData;
            theme = DungeonManager.Instance.GetDungeonTheme(_currentMap.themeName);
            
            SoundManager.Instance.PlayBGM(theme.bgmID);

            UpdateRenderSettings(theme);

            if (backgroundImage != null) backgroundImage.texture = theme.background;
            
            // 시스템 초기화
            _renderer.LoadAssets(theme, 64, 64, null);
            // 테마에 설정된 인카운터 모드로 초기화
            encounterSystem.Initialize(theme.monsterList, theme.encounterMode);

            int finalStartX = _currentMap.startX;
            int finalStartY = _currentMap.startY;
            Direction finalStartDir = _currentMap.startDirection;

            if (entryEntrance != null)
            {
                // 케이스 1: 다른 던전 문을 통해 명시적으로 들어온 경우
                finalStartDir = entryEntrance.targetDirection;
                finalStartX = entryEntrance.targetX;
                finalStartY = entryEntrance.targetY;
            }
            else
            {
                // 케이스 2: 월드맵 등에서 매개변수 없이(null) 새로 씬이 켜진 경우
                // DungeonMapStateManager에 이 맵에 대한 세이브/마지막 위치 정보가 있는지 확인합니다.
                if (DungeonMapStateManager.Instance != null)
                {
                    // 만약 해당 데이터 매니저에 마지막 위치를 기억하는 전역 기능이 있다면 가져옵니다.
                    // 예시: DungeonMapStateManager에 마지막 좌표를 반환하는 메서드가 있다고 가정할 때
                    // var lastPos = DungeonMapStateManager.Instance.GetLastPosition(_currentMap.mapID);
                    // if (lastPos != null) { finalStartX = lastPos.x; finalStartY = lastPos.y; finalStartDir = (int)lastPos.dir; }
                }
                
                // 만약 월드맵에서 던전으로 들어올 때 전용 시작 좌표를 DungeonManager에 세팅해 주었다면 그것을 사용합니다.
                // 예시: DungeonManager.Instance.reservedSpawnX 등이 구현되어 있다면 적용
            }

            if (DungeonEventManager.Instance)
                DungeonEventManager.Instance.SetCurrentMapID(_currentMap.mapID);
            
            // 최종 결정된 좌표로 플레이어를 완벽하게 배치합니다.
            _player.SetMapData(_currentMap, finalStartX, finalStartY, finalStartDir);

            RefreshAppVisible();
            
            // 벽 애니메이션 초기화
            InitializeWallAnims(theme);
            _renderer.SetMapData(_currentMap, theme, _tileAnimStates);
            
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            _maxSpawnCount = theme.maxSpawnCount;
            _spawnDelay = theme.spawnDelay;
            _currentSpawnTimer = 0f;
            _activeEnemies.Clear();

            SpawnStaticObjects(theme);
            
            monsterExist = theme.monsterList != null && theme.monsterList.Count > 0;
            // 심볼 인카운터 모드일 때만 몬스터 스폰
            if (monsterExist && _maxSpawnCount > 0 && theme.encounterMode != EncounterMode.Random) 
            {
                SpawnSymbolEnemies(_maxSpawnCount);
            }
            else
            {
                // 몬스터가 없어도 고정 오브젝트를 렌더러로 보내야 하므로
                UpdateSpriteData();
            }
        }

        private void UpdateRenderSettings(DungeonTheme theme)
        {
            renderSettings.useGridLighting = theme.useGridLighting;
            renderSettings.lightingIntensity = theme.lightingIntensity;
            renderSettings.fogColor = theme.fogColor;

            // 생물체 효과
            renderSettings.useOrganicEffect = theme.useOrganicEffect;
            renderSettings.organicFreqX = theme.organicFreqX;
            renderSettings.organicSpeed = theme.organicSpeed;
            renderSettings.organicBreath = theme.organicBreath;
            renderSettings.organicAmplitude = theme.organicAmplitude;

            // 실린더 효과
            renderSettings.useCylinderEffect = theme.useCylinderEffect;
            renderSettings.cylinderStrength = theme.cylinderStrength;

            // 멜트 효과
            renderSettings.useMeltEffect = theme.useMeltEffect;
            renderSettings.meltEdgeBump = theme.meltEdgeBump;
            renderSettings.meltEdgeSpeed = theme.meltEdgeSpeed;

            // 벽 왜곡 효과
            renderSettings.useWallDistortion = theme.useWallDistortion;
            renderSettings.distortionAmp = theme.distortionAmp;
            renderSettings.distortionFreq = theme.distortionFreq;

            // 먼지 효과
            renderSettings.useDustEffect = theme.useDustEffect;
            renderSettings.dustParticleCount = theme.dustParticleCount;
            renderSettings.dustSwayAmplitude = theme.dustSwayAmplitude;
            renderSettings.dustMovesUp = theme.dustMovesUp;
            renderSettings.useDustTwinkle = theme.useDustTwinkle;
            renderSettings.dustTwinkleSpeed = theme.dustTwinkleSpeed;
            renderSettings.dustColor = theme.dustColor;
        }

        // 모듈 UI의 표시 여부 결정
        private void RefreshAppVisible()
        {
            if (miniMap != null)
            {
                miniMap.Initialize(_currentMap);
                miniMap.gameObject.SetActive(theme.moduleEnable && ModuleManager.Instance.IsMounted(ModuleFeature.LocalRadar));
                
                if (miniMap.gameObject.activeSelf)
                    miniMap.SnapToGrid(_player.LogicX, _player.LogicY, _player.DirectionIdx);
            }
            if (compassUI != null)
            {
                compassUI.SetDirection(_player.DirectionIdx);
                compassUI.gameObject.SetActive(theme.moduleEnable && ModuleManager.Instance.IsMounted(ModuleFeature.GyroCompass));   
            }
            if (autoMapContainer != null)
            {
                autoMapContainer.SetActive(false);
                autoMapRenderer.DrawFullMap(_currentMap, DungeonManager.Instance.CurrentDungeonState);
            }
            if (encounterSystem != null)
            {
                encounterSystem.SetVisible(theme.moduleEnable && ModuleManager.Instance.IsMounted(ModuleFeature.MobSensor));
            }
            if (weatherUI != null)
            {
                weatherUI.gameObject.SetActive(theme.moduleEnable && ModuleManager.Instance.IsMounted(ModuleFeature.WeatherWidget));
            }
        }

        // 플레이어 주변의 몬스터 거리를 감지하여 위험도 UI에 반영
        private void UpdateEncounterSensor()
        {
            if (_activeEnemies.Count == 0)
            {
                encounterSystem.UpdateSymbolDanger(0f);
                return;
            }

            float minDistance = float.MaxValue;
            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.isAlive) continue;
                float dist = Vector2.Distance(new Vector2(enemy.x, enemy.y), new Vector2(_player.LogicX, _player.LogicY));
                if (dist < minDistance)
                {
                    minDistance = dist;
                }
            }

            // 센서 최대 감지 거리 (8칸 안에 몬스터가 들어오면 반응 시작)
            float maxSensorRange = 8.0f; 
            
            // 거리가 0에 가까울수록 ratio는 1에 가까워짐
            float ratio = 1.0f - (minDistance / maxSensorRange);
            
            encounterSystem.UpdateSymbolDanger(ratio);
        }

        private void OnPlayerStep()
        {
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            // 테마 설정에 따라 랜덤 인카운터가 활성화된 경우에만 걸음 수 연산
            if (theme != null && theme.encounterMode == EncounterMode.Random)
            {
                encounterSystem.OnStepTaken();
            }
            
            // 이벤트가 있는지 체크
            CheckCurrentTileEvent();
            // 상점이 있는지 확인
            CheckFrontForShop();
        }

        // 현재 서 있는 칸의 이벤트를 확인하고 발동
        private void CheckCurrentTileEvent()
        {
            if (DungeonEventManager.Instance == null) return;

            (string eventID, int forceDir) = DungeonEventManager.Instance.CheckEvent(_player.LogicX, _player.LogicY);
            if (!string.IsNullOrEmpty(eventID))
                StartCoroutine(ShowDialog(eventID, forceDir));
        }

        IEnumerator ShowDialog(string eventID, int forceDir)
        {
            if (forceDir != -1)
            {
                yield return TurnToDirectionRoutine(forceDir);
            } 
            GameStateManager.Instance.StartEventDialogue(eventID);
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

            // 바닥/천장 초기화
            _floorAnimState = null;
            _ceilAnimState = null;
            _currentFloorTexIdx = theme.floorTexIdx;
            _currentCeilTexIdx = theme.ceilingTexIdx;

            _tileAnimStates = new TileAnimState[_currentMap.width, _currentMap.height];

            Dictionary<int, WallAnimConfig> animDict = new Dictionary<int, WallAnimConfig>();
            foreach (var cfg in theme.wallAnimations)
            {
                // 배열에 텍스처가 없으면 건너뜀
                if (cfg.frameTexIDs == null || cfg.frameTexIDs.Length == 0) continue;

                // 기준 텍스처는 항상 0번 인덱스로 사용
                int baseTex = cfg.frameTexIDs[0];

                if (!animDict.ContainsKey(baseTex)) animDict.Add(baseTex, cfg);

                // 바닥/천장 초기화 로직도 baseTex로 비교
                if (baseTex == theme.floorTexIdx)
                {
                    _floorAnimState = new TileAnimState { isAnimating = true, config = cfg, currentFrame = 0, timer = UnityEngine.Random.Range(cfg.minInterval, cfg.maxInterval) };
                }
                if (baseTex == theme.ceilingTexIdx)
                {
                    _ceilAnimState = new TileAnimState { isAnimating = true, config = cfg, currentFrame = 0, timer = UnityEngine.Random.Range(cfg.minInterval, cfg.maxInterval) };
                }
            }
                

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
            float dt = Time.deltaTime;
            bool globalTexChanged = false;

            // 전역 바닥 애니메이션 업데이트
            if (_floorAnimState != null && _floorAnimState.isAnimating)
            {
                _floorAnimState.timer -= dt;
                if (_floorAnimState.timer <= 0)
                {
                    // 프레임 1 증가, 배열 길이를 넘어가면 0으로 순환
                    _floorAnimState.currentFrame = (_floorAnimState.currentFrame + 1) % _floorAnimState.config.frameTexIDs.Length;
                    _floorAnimState.timer = UnityEngine.Random.Range(_floorAnimState.config.minInterval, _floorAnimState.config.maxInterval);
                    _currentFloorTexIdx = _floorAnimState.config.frameTexIDs[_floorAnimState.currentFrame];
                    globalTexChanged = true;
                }
            }

            // 전역 천장 애니메이션 업데이트
            if (_ceilAnimState != null && _ceilAnimState.isAnimating)
            {
                _ceilAnimState.timer -= dt;
                if (_ceilAnimState.timer <= 0)
                {
                    _ceilAnimState.currentFrame = (_ceilAnimState.currentFrame + 1) % _ceilAnimState.config.frameTexIDs.Length;
                    _ceilAnimState.timer = UnityEngine.Random.Range(_ceilAnimState.config.minInterval, _ceilAnimState.config.maxInterval);
                    _currentCeilTexIdx = _ceilAnimState.config.frameTexIDs[_ceilAnimState.currentFrame];
                    globalTexChanged = true;
                }
            }

            // 텍스처 스왑이 일어났다면 렌더러에 즉시 반영
            if (globalTexChanged)
            {
                _renderer.UpdateFloorCeilingTex(_currentFloorTexIdx, _currentCeilTexIdx);
            }

            if (_tileAnimStates == null) return;

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
                            // 프레임 순환
                            st.currentFrame = (st.currentFrame + 1) % st.config.frameTexIDs.Length;
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
            _renderer.SetScanState(true, 0f);

            // 속도 기반으로 도달 시간 계산
            float expandTime = renderSettings.maxScanDistance / renderSettings.scanSpeed;
            float returnTime = renderSettings.maxScanDistance / (renderSettings.scanSpeed * renderSettings.returnSpeedMultiplier);

            Sequence seq = DOTween.Sequence();
            
            // 스캔 퍼짐
            seq.Append(DOTween.To(() => radius, x => { 
                radius = x; 
                _renderer.SetScanState(true, radius); 
            }, renderSettings.maxScanDistance, expandTime).SetEase(Ease.Linear));
            
            seq.AppendInterval(renderSettings.scanWaitTime);
            
            // 돌아옴
            seq.Append(DOTween.To(() => radius, x => { 
                radius = x; 
                _renderer.SetScanState(true, radius); 
            }, 0f, returnTime).SetEase(Ease.Linear));

            yield return seq.WaitForCompletion();

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

        // 맵 데이터에 배치된 고정 오브젝트들을 씬에 스폰
        private void SpawnStaticObjects(DungeonTheme theme)
        {
            _staticObjects.Clear();

            // 테마에서 오브젝트의 충돌 여부(isObstacle)를 찾기 위한 딕셔너리
            Dictionary<int, bool> objectSolidMap = new Dictionary<int, bool>();
            if (theme.objectSprites != null)
            {
                foreach (var objData in theme.objectSprites)
                {
                    objectSolidMap[objData.objectID] = objData.isObstacle;
                }
            }

            for (int x = 0; x < _currentMap.width; x++)
            {
                for (int y = 0; y < _currentMap.height; y++)
                {
                    CellData cell = _currentMap.GetCell(x, y);
                    if (cell == null) continue;

                    // 중앙 오브젝트 스폰
                    if (cell.centerObjectID != -1)
                        AddStaticObject(x + 0.5f, y + 0.5f, cell.centerObjectID, objectSolidMap);

                    // 벽면 오브젝트 스폰 (벽 쪽으로 오프셋을 줌)
                    float offset = 0.49f;
                    if (cell.faceObjectIDs[0] != -1) AddStaticObject(x + 0.5f, y + 0.5f + offset, cell.faceObjectIDs[0], objectSolidMap); // North
                    if (cell.faceObjectIDs[1] != -1) AddStaticObject(x + 0.5f + offset, y + 0.5f, cell.faceObjectIDs[1], objectSolidMap); // East
                    if (cell.faceObjectIDs[2] != -1) AddStaticObject(x + 0.5f, y + 0.5f - offset, cell.faceObjectIDs[2], objectSolidMap); // South
                    if (cell.faceObjectIDs[3] != -1) AddStaticObject(x + 0.5f - offset, y + 0.5f, cell.faceObjectIDs[3], objectSolidMap); // West
                }
            }
        }

        private void AddStaticObject(float x, float y, int id, Dictionary<int, bool> solidMap)
        {
            bool isSolid = solidMap.ContainsKey(id) ? solidMap[id] : false;
            _staticObjects.Add(new MapObject {
                x = x, y = y, texIdx = id, isSolid = isSolid, isActive = true,
                objectId = $"Obj_{id}_{x}_{y}"
            });
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

            // 맵 범위 체크
            if (tx < 0 || tx >= _currentMap.width || ty < 0 || ty >= _currentMap.height) return false;

            // 방향에 따른 벽면 충돌 체크
            int targetEnterFace = -1;
            int currentExitFace = -1;

            if (dir.x > 0)      { targetEnterFace = 3; currentExitFace = 1; } // East 이동 (내동쪽(1)으로 나가서 상대서쪽(3)으로 진입)
            else if (dir.x < 0) { targetEnterFace = 1; currentExitFace = 3; } // West 이동
            else if (dir.y > 0) { targetEnterFace = 2; currentExitFace = 0; } // North 이동
            else if (dir.y < 0) { targetEnterFace = 0; currentExitFace = 2; } // South 이동

            // 현재 칸에서 해당 방향으로 나갈 수 있는지 내벽 검사
            CellData currentCell = _currentMap.GetCell(ex, ey);
            if (currentCell != null && currentCell.HasWall() && currentExitFace != -1)
            {
                int texID = currentCell.wallTextureIDs[currentExitFace];
                if (texID != -1) return false;
            }

            // 목표 칸이 void인지 먼저 검사
            CellData targetCell = _currentMap.GetCell(tx, ty);
            if (targetCell == null || targetCell.value == -1) return false;

            // 목표 칸으로 해당 방향을 통해 들어갈 수 있는지 외벽 검사
            if (targetCell.HasWall() && targetEnterFace != -1)
            {
                int texID = targetCell.wallTextureIDs[targetEnterFace];
                if (texID != -1) return false;
            }

            // 플레이어 위치일 경우, 겹치기 체크를 무시하고 돌진
            if (tx == _player.LogicX && ty == _player.LogicY) return true;

            // 다른 살아있는 몬스터와 겹치기 방지
            foreach (var other in _activeEnemies)
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
            // 플레이어가 대화 중이거나 이동 중이거나 시스템 메시지 패널이 켜있을 때는 적들이 움직이지 않고 대기
            if (_inputLocked || _player.IsMoving || (systemMessagePanel != null && systemMessagePanel.activeSelf)) 
            {
                return;
            }

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

            foreach (var enemy in _activeEnemies)
            {
                if (enemy.isAlive)
                {
                    spriteList.Add(new SpriteInfo { 
                        x = enemy.x, 
                        y = enemy.y, 
                        texIdx = enemy.currentTexIdx,
                        isEnemy = true
                    });
                }
            }

            foreach (var obj in _staticObjects)
            {
                if (obj.isActive)
                {
                    spriteList.Add(new SpriteInfo { 
                        x = obj.x, 
                        y = obj.y, 
                        texIdx = obj.texIdx, // DungeonTheme의 ObjectSpriteData.objectID와 일치
                        isEnemy = false
                    });
                }
            }

            // 통합된 리스트를 렌더러에 전달
            _renderer.UpdateSprites(spriteList.ToArray());
        }

        private void OnGameStateChanged(GameState newState)
        {
            _canRender = (newState == GameState.Exploration);
            if (!_canRender)
            {
                // 탐험 상태가 아니면 텍스트를 숨김
                HideSystemMessage();
                HideRoomName();
                return;
            }
            
            SoundManager.Instance.PlayBGM(DungeonManager.Instance.GetDungeonTheme(_currentMap.themeName).bgmID);
            RefreshAppVisible();

            // 탐험 상태로 돌아왔을 때 정면 체크 
            CheckFrontForShop();
        }
    }
}