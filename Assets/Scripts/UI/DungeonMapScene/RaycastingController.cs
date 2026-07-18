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
using Helper;

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

            // 이 심볼과 부딪혔을 때 싸우게 될 실제 몬스터 ID 목록
            public List<string> encounterGroup; 
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
        private Dictionary<string, int> _monsterBaseTexMap = new Dictionary<string, int>(); // 몬스터 ID별로 텍스처 인덱스(baseTexIdx) 시작점을 저장

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
            
            ManagerRoot.GameState.OnStateChanged += OnGameStateChanged;
            ManagerRoot.GameState.ChangeState(GameState.Exploration);

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
            if(ManagerRoot.GameState) 
                ManagerRoot.GameState.OnStateChanged -= OnGameStateChanged;
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
                ManagerRoot.GameState.ChangeState(GameState.PlayerMenu);
                inputCooldown = 0.05f;
                return;
            }
            
            if (!_inputLocked && Input.GetKeyDown(KeyCode.M))
            {
                if (!theme.moduleEnable || !ManagerRoot.Module.IsMounted(ModuleFeature.AutoMapper)) return;
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
            
            // 상호작용 (올려보기, 내려보기, 보물상자 열기 등)
            if (!_player.IsMoving && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                UI_Action();
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!ManagerRoot.Module.IsMounted(ModuleFeature.AutoMapper)) return;
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
                if (ManagerRoot.DungeonEvent)
                    ManagerRoot.DungeonEvent.SetCurrentMapID(entrance.destinationID);

                ManagerRoot.World.SetCurrentRegionTheme(entrance.destinationID);
                ManagerRoot.World.isLoadGame = true;
                
                var data = ManagerRoot.World.currentRegionTheme;
                ManagerRoot.World.loadedPosition = data.startPosition;
                
                SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                ManagerRoot.GameState.ChangeState(GameState.Exploration);
                
                yield break;
            }
            else
            {
                // 기존의 일반 던전 레벨 전환 로직
                ManagerRoot.DungeonEvent.SetCurrentMapID(entrance.destinationID);
                ManagerRoot.Dungeon.LoadDungeonFromJson(entrance.destinationID);
                
                // 새로운 맵 로드
                LoadMapData(entrance);
                
                yield return new WaitForSeconds(0.1f);
                if (theme.isUnderwater)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.Spash);
                }
                else 
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.Fall);
                }
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
                if (ManagerRoot.DungeonEvent)
                    ManagerRoot.DungeonEvent.SetCurrentMapID(entrance.destinationID);

                ManagerRoot.World.SetCurrentRegionTheme(entrance.destinationID);
                ManagerRoot.World.isLoadGame = true;
                
                var data = ManagerRoot.World.currentRegionTheme;
                ManagerRoot.World.loadedPosition = data.startPosition;
                
                // 월드맵 씬 로드 및 상태 변경
                SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                ManagerRoot.GameState.ChangeState(GameState.Exploration);
                
                // 씬이 파괴되므로 코루틴을 즉시 종료
                yield break; 
            }
            else
            {
                // 기존의 일반 던전 레벨 전환 로직
                ManagerRoot.DungeonEvent.SetCurrentMapID(entrance.destinationID);
                ManagerRoot.Dungeon.LoadDungeonFromJson(entrance.destinationID);
                
                // 새로운 맵 로드
                LoadMapData(entrance);
                
                yield return new WaitForSeconds(0.1f);

                if (theme.isUnderwater)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.Spash);
                }
                else 
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.Fall);
                }

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
            
            ManagerRoot.Sound.PlaySFX(SfxID.Slide_Door); 

            // 원래 문이 배치되어 있던 면과 타일의 원래 value를 저장
            bool[] originalDoorFaces = new bool[4];
            int originalValue = doorCell.value;

            for (int face = 0; face < 4; face++)
            {
                // 해당 면이 현재 열어야 할 문의 텍스처(closedTexId)와 일치하는지 확인
                if (doorCell.wallTextureIDs[face] == doorConfig.closedTexId)
                {
                    originalDoorFaces[face] = true;
                }
            }

            // 문이 열리는 애니메이션
            if (doorConfig.openFrameTexIds != null && doorConfig.openFrameTexIds.Length > 0)
            {
                for (int i = 0; i < doorConfig.openFrameTexIds.Length; i++)
                {
                    for (int face = 0; face < 4; face++)
                    {
                        // 아무 벽이나 덮어씌우지 않고, 문의 원래 면이었던 곳만 애니메이션 프레임으로 덮어씌움
                        if (originalDoorFaces[face]) 
                            doorCell.wallTextureIDs[face] = doorConfig.openFrameTexIds[i];
                    }
                    
                    yield return new WaitForSeconds(doorConfig.animSpeed); 
                }
            }

            // 전진 실행
            float duration = _player.IsRunning ? moveDuration / 2f : moveDuration;
            if (miniMap) miniMap.TranslateToNewPosition(tx, ty, duration);
            
            yield return StartCoroutine(_player.MoveGridRoutine(tx, ty, duration, null));

            // 문을 완전히 지나간 직후, 타일 속성과 텍스처를 닫힌 문으로 복원
            doorCell.value = originalValue; 
            for (int face = 0; face < 4; face++)
            {
                if (originalDoorFaces[face])
                    doorCell.wallTextureIDs[face] = doorConfig.closedTexId;
            }

            _inputLocked = false;
        }

        // 문을 열고 맵을 전환하거나 상점으로 진입하는 연출
        private IEnumerator OpenDoorAndTransitionRoutine(CellData doorCell, EntranceData entrance, Vector2Int moveDir, DoorAnimConfig doorConfig)
        {
            _inputLocked = true;
            
            ManagerRoot.Sound.PlaySFX(SfxID.Slide_Door); 
            
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

            // 심볼 인카운터 검사
            MapEnemy encounteredEnemy = _activeEnemies.Find(e => Mathf.FloorToInt(e.x) == tx && Mathf.FloorToInt(e.y) == ty && e.isAlive);
            if (encounteredEnemy != null)
            {
                EncounterType encType = DetermineEncounterAdvantage(encounteredEnemy, true);
                StartCoroutine(SymbolEncounterRoutine(encounteredEnemy, moveVec, encType));
                return;
            }

            // 고정 오브젝트 충돌 검사
            MapObject blockingObj = _staticObjects.Find(o => Mathf.FloorToInt(o.x) == tx && Mathf.FloorToInt(o.y) == ty && o.isActive && o.isSolid);
            if (blockingObj != null)
            {
                StartCoroutine(_player.BumpRoutine(moveVec));
                ManagerRoot.Sound.PlaySFX(SfxID.Bump_Wall);
                return; 
            }

            // DungeonPlayer의 순수 물리 판정 (텍스처가 있으면 일단 막음)
            bool walkable = _player.IsWalkable(tx, ty, moveVec.x, moveVec.y);

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
            if (currentCell != null && currentExitFace != -1 && currentCell.wallTextureIDs[currentExitFace] != -1)
            {
                isBlockedByWall = true; hitCell = currentCell; hitTexID = currentCell.wallTextureIDs[currentExitFace];
            }
            else if (targetCell != null && targetEnterFace != -1 && targetCell.wallTextureIDs[targetEnterFace] != -1)
            {
                isBlockedByWall = true; hitCell = targetCell; hitTexID = targetCell.wallTextureIDs[targetEnterFace];
            }
            else if (targetCell == null || targetCell.value == -1)
            {
                isBlockedByWall = true;
            }

            // 부딪힌 텍스처가 문(Door)인지 확인
            DoorAnimConfig doorConfig = null;
            if (hitTexID != -1)
            {
                doorConfig = theme?.doorAnimations?.Find(d => d.closedTexId == hitTexID);
            }

            // 최종 이동 및 충돌 연출 처리
            if (walkable)
            {
                float duration = _player.IsRunning ? moveDuration / 2f : moveDuration;
                if (miniMap) miniMap.TranslateToNewPosition(tx, ty, duration);
                StartCoroutine(_player.MoveGridRoutine(tx, ty, duration, null));
            }
            else
            {
                Direction inputDir = VectorToDirection(moveVec);
                Direction facingDir = (Direction)_player.DirectionIdx;
                bool isMovingForward = (inputDir == facingDir);

                EntranceData validEntrance = CheckForEntrance(_player.LogicX, _player.LogicY, tx, ty, moveVec);
                
                if (validEntrance != null)
                {
                    if (doorConfig != null && hitCell != null)
                        StartCoroutine(OpenDoorAndTransitionRoutine(hitCell, validEntrance, moveVec, doorConfig));
                    else
                        StartCoroutine(TransitionToOtherPlace(validEntrance, moveVec));
                }
                else
                {
                    if (doorConfig != null && hitCell != null && isMovingForward)
                    {
                        StartCoroutine(OpenDoorAndMoveRoutine(hitCell, tx, ty, moveVec, doorConfig));
                    }
                    else
                    {
                        // 낭떠러지(-1), 진짜 꽉 막힌 벽(1), 혹은 맵의 끝을 만났을 경우 무조건 Bump!
                        StartCoroutine(_player.BumpRoutine(moveVec));
                        ManagerRoot.Sound.PlaySFX(SfxID.Bump_Wall);
                    }
                }
            }
        }

        private IEnumerator SymbolEncounterRoutine(MapEnemy enemy, Vector2Int moveVec, EncounterType encType)
        {
            _inputLocked = true;

            // 적에게 부딪히는 연출 (벽 충돌과 같음)
            ManagerRoot.Sound.PlaySFX(SfxID.Bump_Wall); 
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

            // 화면을 캡처
            Sprite bgSprite = CaptureCurrentDungeonView();
            
            // 스폰 시점에 미리 결정해둔 몬스터 리스트로 전투 개시
            ManagerRoot.GameState.StartEncounter(enemy.encounterGroup, theme.fogColor, encType, bgSprite);
            
            // 전투가 끝나고 탐험 상태로 돌아올 때까지 대기
            yield return new WaitUntil(() => ManagerRoot.GameState.CurrentState == GameState.Exploration);

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
            
            if (targetObj != null)
            {
                // 닫힌 보물상자 (Object ID: 0) 상호작용
                if (targetObj.texIdx == 0)
                {
                    // 상태 및 텍스처 변경 (열린 상자의 ID인 1로 변경)
                    targetObj.texIdx = 1;

                    // 변경된 텍스처를 렌더러에 즉시 반영
                    UpdateSpriteData();

                    ShowSystemMessage("낡은 보물상자를 열었다.");
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
                if (currentEntrance.triggerDirection == inputDir && inputDir == facingDir)
                    return currentEntrance;
            }

            // 진입 시 방 바깥쪽 벽에 있는 입구인지 체크
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

        // 현재 바라보는 방향의 입구에 이름이 있는지 확인하고 UI를 갱신
        private void CheckFrontForEntranceName()
        {
            if (_player == null || _currentMap == null) return;

            if (ManagerRoot.GameState != null && ManagerRoot.GameState.CurrentState != GameState.Exploration)
            {
                HideRoomName();
                return;
            }

            Vector2Int forwardVec = _player.GetForwardVector();
            int frontX = _player.LogicX + forwardVec.x;
            int frontY = _player.LogicY + forwardVec.y;

            EntranceData frontEntrance = CheckForEntrance(_player.LogicX, _player.LogicY, frontX, frontY, forwardVec);

            if (frontEntrance != null)
            {
                if (frontEntrance.type == EntranceType.Shop)
                {
                    var shopData = ShopManager.Instance.GetShopData(frontEntrance.destinationID);
                    if (shopData != null) ShowRoomName(shopData.displayName);
                }
                else if (frontEntrance.type == EntranceType.Terminal)
                {
                    ShowRoomName("TERMINAL"); // 일단 무조건 TERMINAL
                }
                else HideRoomName();
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
                if (ManagerRoot.DungeonEvent)
                    ManagerRoot.DungeonEvent.SetCurrentMapID(entrance.destinationID);
                
                if (entrance.isWorldMap)
                {
                    ManagerRoot.World.SetCurrentRegionTheme(entrance.destinationID);
                    ManagerRoot.World.isLoadGame = true;
                    
                    var data = ManagerRoot.World.currentRegionTheme;
                    ManagerRoot.World.loadedPosition = data.startPosition;
                    SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                    ManagerRoot.GameState.ChangeState(GameState.Exploration);
                }
                else if (ManagerRoot.Dungeon)
                {
                    ManagerRoot.Dungeon.LoadDungeonFromJson(entrance.destinationID);
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
                if (ManagerRoot.GameState != null)
                {
                    ManagerRoot.GameState.ShowShop(entrance.destinationID);
                }

                yield return new WaitUntil(() => ManagerRoot.GameState.CurrentState != GameState.Shop);

                int reverseDir = (_player.DirectionIdx + 2) % 4; 
                
                Vector2 originalPos = _player.GetOffsetPosition(preEntranceLogicX, preEntranceLogicY, reverseDir);
                _player.SetDirectPosition(originalPos.x, originalPos.y, reverseDir);

                if (compassUI) compassUI.SetDirection(reverseDir);
                if (miniMap) miniMap.SnapToGrid(preEntranceLogicX, preEntranceLogicY, reverseDir);
                
                UpdateMapDiscovery(preEntranceLogicX, preEntranceLogicY);

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
            else if (entrance.type == EntranceType.Terminal)
            {
                if (ManagerRoot.GameState != null)
                    ManagerRoot.GameState.ChangeState(GameState.Terminal);

                string currentTerminalID = entrance.destinationID;
                
                int exitLogicX = preEntranceLogicX;
                int exitLogicY = preEntranceLogicY;
                int exitDir = (_player.DirectionIdx + 2) % 4; 

                while (true)
                {
                    TerminalManager.Instance.UnlockTerminal(currentTerminalID);
                    TerminalUIManager.Instance.OpenTerminal(currentTerminalID);

                    yield return new WaitUntil(() => TerminalUIManager.Instance.IsSelectionComplete);

                    if (TerminalUIManager.Instance.IsCanceled)
                    {
                        Vector2 originalPos = _player.GetOffsetPosition(exitLogicX, exitLogicY, exitDir);
                        _player.SetDirectPosition(originalPos.x, originalPos.y, exitDir);

                        if (compassUI) compassUI.SetDirection(exitDir);
                        if (miniMap) miniMap.SnapToGrid(exitLogicX, exitLogicY, exitDir);
                        UpdateMapDiscovery(exitLogicX, exitLogicY);

                        _renderer.RenderFrame(_player, renderSettings);

                        if (ManagerRoot.GameState != null)
                            ManagerRoot.GameState.ChangeState(GameState.Exploration);

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
                        break; 
                    }
                    else
                    {
                        TerminalData dest = TerminalUIManager.Instance.SelectedTerminal;

                        EntranceData dynamicDest = new EntranceData
                        {
                            destinationID = dest.mapID,
                            targetX = dest.targetX,
                            targetY = dest.targetY,
                            targetDirection = dest.targetDir
                        };

                        if (ManagerRoot.Dungeon)
                        {
                            ManagerRoot.Dungeon.LoadDungeonFromJson(dynamicDest.destinationID);
                            LoadMapData(dynamicDest); 
                            
                            ManagerRoot.Sound.StopBGM(false);
                        }
                        
                        yield return new WaitForSeconds(0.2f); // 렉 스파이크 대기

                        currentTerminalID = dest.terminalID;
                        exitLogicX = dest.targetX;
                        exitLogicY = dest.targetY;
                        exitDir = (int)dest.targetDir;
                    }
                }
            }
            else if (entrance.type == EntranceType.Elevator)
            {
                ManagerRoot.GameState.ChangeState(GameState.Elevator);

                ElevatorData elvData = ManagerRoot.Dungeon.GetElevatorData(entrance.destinationID);
                
                if (elvData != null)
                {
                    ElevatorUIManager.Instance.OpenElevator(elvData, _currentMap.mapID);

                    yield return new WaitUntil(() => ElevatorUIManager.Instance.IsSelectionComplete);
                    yield return new WaitUntil(() => ElevatorUIManager.Instance.IsAnimationFinished);

                    FloorData destFloor = ElevatorUIManager.Instance.SelectedFloor;

                    if (destFloor.mapID == _currentMap.mapID)
                    {
                        int exitDir = (_player.DirectionIdx + 2) % 4; 
                        _player.SetMapData(_currentMap, preEntranceLogicX, preEntranceLogicY, (Direction)exitDir);
                        
                        if (miniMap) miniMap.SnapToGrid(preEntranceLogicX, preEntranceLogicY, exitDir);
                        if (compassUI) compassUI.SetDirection(exitDir);

                        yield return StartCoroutine(HandleElevatorExitSequence(elvData));

                        ManagerRoot.GameState.ChangeState(GameState.Exploration);
                        _inputLocked = false;
                        yield break; 
                    }
                    
                    EntranceData dynamicDest = new EntranceData
                    {
                        destinationID = destFloor.mapID,
                        targetX = destFloor.mapX,
                        targetY = destFloor.mapY,
                        targetDirection = destFloor.targetDirection
                    };

                    if (ManagerRoot.Dungeon)
                    {
                        ManagerRoot.Dungeon.LoadDungeonFromJson(dynamicDest.destinationID);
                        LoadMapData(dynamicDest); 
                    }
                    
                    yield return new WaitForSeconds(0.2f); 

                    yield return StartCoroutine(HandleElevatorExitSequence(elvData));
                }

                ManagerRoot.GameState.ChangeState(GameState.Exploration);
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
            int currentDir = _player.DirectionIdx;
            int nextDir = ((currentDir + dirStep) % 4 + 4) % 4;

            if (compassUI)
                compassUI.AnimateTurn(currentDir, nextDir, dirStep, turnDuration);
            
            if (miniMap)
                miniMap.SetDirection(nextDir, turnDuration);

            yield return StartCoroutine(_player.RotateGridRoutine(dirStep, turnDuration, null));
            
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            CheckFrontForEntranceName();
        }

        private IEnumerator TurnToDirectionRoutine(int targetDir)
        {
            int currentDir = _player.DirectionIdx;

            if (currentDir == targetDir) yield break;

            int dirStep = targetDir - currentDir;

            if (dirStep == -3) dirStep = 1;
            else if (dirStep == 3) dirStep = -1;

            if (Mathf.Abs(dirStep) == 2)
            {
                yield return StartCoroutine(TurnRoutine(1));
                yield return StartCoroutine(TurnRoutine(1));
            }
            else
            {
                yield return StartCoroutine(TurnRoutine(dirStep));
            }
        }

        private IEnumerator HandleElevatorExitSequence(ElevatorData elvData)
        {
            Vector2Int forward = _player.GetForwardVector();
            int backX = _player.LogicX - forward.x;
            int backY = _player.LogicY - forward.y;

            CellData corridorCell = _currentMap.GetCell(_player.LogicX, _player.LogicY);
            CellData doorCell = _currentMap.GetCell(backX, backY);

            int corridorBackFace = -1;
            int doorFrontFace = -1;

            if (forward.x > 0)      { corridorBackFace = 3; doorFrontFace = 1; }
            else if (forward.x < 0) { corridorBackFace = 1; doorFrontFace = 3; }
            else if (forward.y > 0) { corridorBackFace = 2; doorFrontFace = 0; }
            else if (forward.y < 0) { corridorBackFace = 0; doorFrontFace = 2; }

            int origDoorVal = 0;
            int origDoorTex = -1;
            int origCorridorTex = -1;

            if (doorCell != null && doorFrontFace != -1)
            {
                origDoorTex = doorCell.wallTextureIDs[doorFrontFace];
            }

            Vector2 targetPos = _player.GetOffsetPosition(_player.LogicX, _player.LogicY, _player.DirectionIdx);
            Vector2 startPos = new Vector2(targetPos.x - (forward.x * 0.8f), targetPos.y - (forward.y * 0.8f));
            
            _player.SetDirectPosition(startPos.x, startPos.y, _player.DirectionIdx);

            _renderer.RenderFrame(_player, renderSettings);

            if (ManagerRoot.GameState.explorationCanvas != null)
                ManagerRoot.GameState.explorationCanvas.SetActive(true);

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
            }

            yield return StartCoroutine(ElevatorUIManager.Instance.OpenDoorsRoutine(elvData.doorType));

            DoorAnimConfig doorConfig = null;
            if (origDoorTex != -1 && theme != null && theme.doorAnimations != null)
            {
                doorConfig = theme.doorAnimations.Find(d => d.closedTexId == origDoorTex);
            }

            if (doorConfig != null && doorConfig.openFrameTexIds != null && doorConfig.openFrameTexIds.Length > 0 && doorCell != null)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.Slide_Door); 
                
                for (int i = 0; i < doorConfig.openFrameTexIds.Length; i++)
                {
                    doorCell.wallTextureIDs[doorFrontFace] = doorConfig.openFrameTexIds[i];
                    _renderer.RenderFrame(_player, renderSettings); 
                    yield return new WaitForSeconds(doorConfig.animSpeed);
                }
            }

            if (doorCell != null)
            {
                origDoorVal = doorCell.value;
                doorCell.value = 0; 
                if (doorFrontFace != -1) doorCell.wallTextureIDs[doorFrontFace] = -1; 
            }
            if (corridorCell != null && corridorBackFace != -1)
            {
                origCorridorTex = corridorCell.wallTextureIDs[corridorBackFace];
                corridorCell.wallTextureIDs[corridorBackFace] = -1;
            }

            _renderer.RenderFrame(_player, renderSettings);

            float stepOutTime = 0.6f;
            StartCoroutine(ElevatorUIManager.Instance.StepOutZoomRoutine(stepOutTime)); 
            
            yield return StartCoroutine(StepOutOfElevatorRoutine(stepOutTime, startPos, targetPos));

            if (doorCell != null)
            {
                doorCell.value = origDoorVal;
                if (doorFrontFace != -1) doorCell.wallTextureIDs[doorFrontFace] = origDoorTex;
            }
            if (corridorCell != null && corridorBackFace != -1)
            {
                corridorCell.wallTextureIDs[corridorBackFace] = origCorridorTex;
            }

            ElevatorUIManager.Instance.CloseElevator();
        }

        private IEnumerator StepOutOfElevatorRoutine(float duration, Vector2 startPos, Vector2 targetPos)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float easeT = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                _player.SetDirectPosition(
                    Mathf.Lerp(startPos.x, targetPos.x, easeT),
                    Mathf.Lerp(startPos.y, targetPos.y, easeT),
                    _player.DirectionIdx
                );

                _renderer.RenderFrame(_player, renderSettings);
                yield return null;
            }

            _player.SetDirectPosition(targetPos.x, targetPos.y, _player.DirectionIdx);
            _renderer.RenderFrame(_player, renderSettings);
        }

        // ================= Map & Game Logic =================
        private void LoadMapData(EntranceData entryEntrance = null)
        {
            _currentMap = ManagerRoot.Dungeon.CurrentDungeonData;
            theme = ManagerRoot.Dungeon.GetDungeonTheme(_currentMap.themeName);
            
            ManagerRoot.Sound.PlayBGM(theme.bgmID);

            UpdateRenderSettings(theme);

            if (backgroundImage != null) backgroundImage.texture = theme.background;
            
            // 이 맵에 등장할 수 있는 모든 몬스터의 애니메이션 프레임 사전 캐싱
            _monsterBaseTexMap.Clear();
            List<Sprite> dynamicEnemySprites = new List<Sprite>();
            
            if (theme.monsterList != null)
            {
                foreach (string monID in theme.monsterList)
                {
                    var entry = DatabaseManager.Instance.monsterDB.GetEntry(monID);
                    if (entry != null && !_monsterBaseTexMap.ContainsKey(monID))
                    {
                        // 현재 카운트를 이 몬스터의 시작 인덱스로 저장
                        _monsterBaseTexMap[monID] = dynamicEnemySprites.Count;
                        
                        // 0: 정면(Down), 1: 뒷면(Up), 2: 좌측(Left), 3: 우측(Right)
                        AddSpriteFrames(dynamicEnemySprites, entry.downImgs);
                        AddSpriteFrames(dynamicEnemySprites, entry.upImgs);
                        AddSpriteFrames(dynamicEnemySprites, entry.leftImgs);
                        AddSpriteFrames(dynamicEnemySprites, entry.rightImgs);
                    }
                }
            }

            _renderer.LoadAssets(theme, dynamicEnemySprites.ToArray(), 64, 64, null);

            // 테마에 설정된 인카운터 모드로 초기화
            encounterSystem.Initialize(theme.monsterList, theme.maxEnemyCount, theme.encounterMode);

            int finalStartX = _currentMap.startX;
            int finalStartY = _currentMap.startY;
            Direction finalStartDir = _currentMap.startDirection;

            if (entryEntrance != null)
            {
                finalStartDir = entryEntrance.targetDirection;
                finalStartX = entryEntrance.targetX;
                finalStartY = entryEntrance.targetY;
            }

            if (ManagerRoot.DungeonEvent)
                ManagerRoot.DungeonEvent.SetCurrentMapID(_currentMap.mapID);
            
            _player.SetMapData(_currentMap, finalStartX, finalStartY, finalStartDir);

            RefreshAppVisible();
            
            InitializeWallAnims(theme);
            _renderer.SetMapData(_currentMap, theme, _tileAnimStates);
            
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            _maxSpawnCount = theme.maxSpawnCount;
            _spawnDelay = theme.spawnDelay;
            _currentSpawnTimer = 0f;
            _activeEnemies.Clear();

            SpawnStaticObjects(theme);
            
            monsterExist = theme.monsterList != null && theme.monsterList.Count > 0;
            if (monsterExist && _maxSpawnCount > 0 && theme.encounterMode != EncounterMode.Random) 
            {
                SpawnSymbolEnemies(_maxSpawnCount);
            }
            else
            {
                UpdateSpriteData();
            }
        }

        private void UpdateRenderSettings(DungeonTheme theme)
        {
            if (theme != null && theme.passableWallTexIDs != null)
            {
                _player.SetIllusionTextures(theme.passableWallTexIDs);
            }

            if (screenImage != null && screenImage.material != null)
            {
                if (theme.isUnderwater)
                {
                    screenImage.material.SetFloat("_WaveAmount", 0.01f); 
                    screenImage.material.SetFloat("_WaveSpeed", 1.0f);
                    screenImage.material.SetFloat("_WaveFrequency", 10.0f);
                }
                else
                {
                    screenImage.material.SetFloat("_WaveAmount", 0.0f);
                }
            }
            renderSettings.useGridLighting = theme.useGridLighting;
            renderSettings.lightingIntensity = theme.lightingIntensity;
            renderSettings.fogColor = theme.fogColor;

            renderSettings.useOrganicEffect = theme.useOrganicEffect;
            renderSettings.organicFreqX = theme.organicFreqX;
            renderSettings.organicSpeed = theme.organicSpeed;
            renderSettings.organicBreath = theme.organicBreath;
            renderSettings.organicAmplitude = theme.organicAmplitude;

            renderSettings.useCylinderEffect = theme.useCylinderEffect;
            renderSettings.cylinderStrength = theme.cylinderStrength;

            renderSettings.useMeltEffect = theme.useMeltEffect;
            renderSettings.meltEdgeBump = theme.meltEdgeBump;
            renderSettings.meltEdgeSpeed = theme.meltEdgeSpeed;

            renderSettings.useWallDistortion = theme.useWallDistortion;
            renderSettings.distortionAmp = theme.distortionAmp;
            renderSettings.distortionFreq = theme.distortionFreq;

            renderSettings.useDustEffect = theme.useDustEffect;
            renderSettings.dustParticleCount = theme.dustParticleCount;
            renderSettings.dustSwayAmplitude = theme.dustSwayAmplitude;
            renderSettings.dustMovesUp = theme.dustMovesUp;
            renderSettings.useDustTwinkle = theme.useDustTwinkle;
            renderSettings.dustTwinkleSpeed = theme.dustTwinkleSpeed;
            renderSettings.dustColor = theme.dustColor;
        }

        private void RefreshAppVisible()
        {
            if (miniMap != null)
            {
                miniMap.Initialize(_currentMap, theme.passableWallTexIDs);
                miniMap.gameObject.SetActive(theme.moduleEnable && ManagerRoot.Module.IsMounted(ModuleFeature.LocalRadar));
                
                if (miniMap.gameObject.activeSelf)
                    miniMap.SnapToGrid(_player.LogicX, _player.LogicY, _player.DirectionIdx);
            }
            if (compassUI != null)
            {
                compassUI.SetDirection(_player.DirectionIdx);
                compassUI.gameObject.SetActive(theme.moduleEnable && ManagerRoot.Module.IsMounted(ModuleFeature.GyroCompass));   
            }
            if (autoMapContainer != null)
            {
                autoMapContainer.SetActive(false);
                autoMapRenderer.DrawFullMap(_currentMap, ManagerRoot.Dungeon.CurrentDungeonState);
            }
            if (encounterSystem != null)
            {
                encounterSystem.SetVisible(theme.moduleEnable && ManagerRoot.Module.IsMounted(ModuleFeature.MobSensor));
            }
            if (weatherUI != null)
            {
                weatherUI.gameObject.SetActive(theme.moduleEnable && ManagerRoot.Module.IsMounted(ModuleFeature.WeatherWidget));
            }
        }

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

            float maxSensorRange = 8.0f; 
            float ratio = 1.0f - (minDistance / maxSensorRange);
            
            encounterSystem.UpdateSymbolDanger(ratio);
        }

        private void OnPlayerStep()
        {
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            
            if (theme != null && theme.encounterMode == EncounterMode.Random)
            {
                encounterSystem.OnStepTaken();
            }
            
            CheckCurrentTileEvent();
            CheckFrontForEntranceName();
        }

        private void CheckCurrentTileEvent()
        {
            if (ManagerRoot.DungeonEvent == null) return;

            (string eventID, int forceDir) = ManagerRoot.DungeonEvent.CheckEvent(_player.LogicX, _player.LogicY);
            if (!string.IsNullOrEmpty(eventID))
            {
                _inputLocked = true;
                _player.SetRunning(false);
                StartCoroutine(ShowDialog(eventID, forceDir));
            }
        }

        IEnumerator ShowDialog(string eventID, int forceDir)
        {
            if (forceDir != -1)
            {
                yield return TurnToDirectionRoutine(forceDir);
            } 
            ManagerRoot.GameState.StartEventDialogue(eventID);

            yield return new WaitUntil(() => ManagerRoot.GameState.CurrentState == GameState.Exploration);

            _inputLocked = false;
        }

        private void UpdateMapDiscovery(int x, int y)
        {
            ManagerRoot.Dungeon.CurrentDungeonState.MarkVisited(x, y);
            autoMapRenderer.RevealCell(x, y);
            autoMapRenderer.UpdatePlayerIcon(x, y, (Direction)_player.DirectionIdx);
            ManagerRoot.DungeonMapState.UpdatePlayerPosition(x, y, (Direction)_player.DirectionIdx, _currentMap.mapID);
        }

        private void InitializeWallAnims(DungeonTheme theme)
        {
            if (theme == null || theme.wallAnimations == null) return;

            _floorAnimState = null;
            _ceilAnimState = null;
            _currentFloorTexIdx = theme.floorTexIdx;
            _currentCeilTexIdx = theme.ceilingTexIdx;

            _tileAnimStates = new TileAnimState[_currentMap.width, _currentMap.height];

            Dictionary<int, WallAnimConfig> animDict = new Dictionary<int, WallAnimConfig>();
            foreach (var cfg in theme.wallAnimations)
            {
                if (cfg.frameTexIDs == null || cfg.frameTexIDs.Length == 0) continue;

                int baseTex = cfg.frameTexIDs[0];

                if (!animDict.ContainsKey(baseTex)) animDict.Add(baseTex, cfg);

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

            if (_floorAnimState != null && _floorAnimState.isAnimating)
            {
                _floorAnimState.timer -= dt;
                if (_floorAnimState.timer <= 0)
                {
                    _floorAnimState.currentFrame = (_floorAnimState.currentFrame + 1) % _floorAnimState.config.frameTexIDs.Length;
                    _floorAnimState.timer = UnityEngine.Random.Range(_floorAnimState.config.minInterval, _floorAnimState.config.maxInterval);
                    _currentFloorTexIdx = _floorAnimState.config.frameTexIDs[_floorAnimState.currentFrame];
                    globalTexChanged = true;
                }
            }

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

            float expandTime = renderSettings.maxScanDistance / renderSettings.scanSpeed;
            float returnTime = renderSettings.maxScanDistance / (renderSettings.scanSpeed * renderSettings.returnSpeedMultiplier);

            Sequence seq = DOTween.Sequence();
            
            seq.Append(DOTween.To(() => radius, x => { 
                radius = x; 
                _renderer.SetScanState(true, radius); 
            }, renderSettings.maxScanDistance, expandTime).SetEase(Ease.Linear));
            
            seq.AppendInterval(renderSettings.scanWaitTime);
            
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

        private void SpawnStaticObjects(DungeonTheme theme)
        {
            _staticObjects.Clear();

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

                    if (cell.centerObjectID != -1)
                        AddStaticObject(x + 0.5f, y + 0.5f, cell.centerObjectID, objectSolidMap);

                    float offset = 0.49f;
                    if (cell.faceObjectIDs[0] != -1) AddStaticObject(x + 0.5f, y + 0.5f + offset, cell.faceObjectIDs[0], objectSolidMap); 
                    if (cell.faceObjectIDs[1] != -1) AddStaticObject(x + 0.5f + offset, y + 0.5f, cell.faceObjectIDs[1], objectSolidMap); 
                    if (cell.faceObjectIDs[2] != -1) AddStaticObject(x + 0.5f, y + 0.5f - offset, cell.faceObjectIDs[2], objectSolidMap); 
                    if (cell.faceObjectIDs[3] != -1) AddStaticObject(x + 0.5f - offset, y + 0.5f, cell.faceObjectIDs[3], objectSolidMap); 
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

        private void SpawnSymbolEnemies(int count)
        {
            int spawned = 0;
            int maxAttempts = count * 50; 
            float safeDistance = 3.0f; 

            while(spawned < count && maxAttempts > 0)
            {
                maxAttempts--;
                int rx = UnityEngine.Random.Range(1, _currentMap.width - 1);
                int ry = UnityEngine.Random.Range(1, _currentMap.height - 1);
                
                CellData cell = _currentMap.GetCell(rx, ry);
                float distToPlayer = Vector2.Distance(new Vector2(rx, ry), new Vector2(_player.LogicX, _player.LogicY));
                
                // 4면이 모두 꽉 막힌 벽(통과 불가)인지 검사합니다.
                bool isEnclosed = true;
                if (cell != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int tex = cell.wallTextureIDs[i];
                        // 벽이 없거나(-1), 통과 가능한 환영의 벽이라면 갇힌 공간이 아님
                        if (tex == -1 || (theme.passableWallTexIDs != null && theme.passableWallTexIDs.Contains(tex)))
                        {
                            isEnclosed = false;
                            break;
                        }
                    }
                }

                if (cell != null && !isEnclosed && cell.value != -1 && distToPlayer >= safeDistance)
                {
                    List<string> generatedGroup = new List<string>();
                    var candidates = encounterSystem.MonsterCandidate;
                    if (candidates != null && candidates.Count > 0)
                    {
                        int numMonsters = BattleCalculator.DetermineSpawnCount(theme.maxEnemyCount);
                        for (int i = 0; i < numMonsters; i++)
                        {
                            generatedGroup.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
                        }
                    }

                    // 가장 레벨이 높은 몬스터를 대표 심볼로 지정
                    string repMonsterID = GetHighestLevelMonster(generatedGroup);
                    
                    int baseTex = 0;
                    if (!string.IsNullOrEmpty(repMonsterID) && _monsterBaseTexMap.ContainsKey(repMonsterID))
                    {
                        baseTex = _monsterBaseTexMap[repMonsterID];
                    }

                    _activeEnemies.Add(new MapEnemy { 
                        x = rx + 0.5f, 
                        y = ry + 0.5f, 
                        targetX = rx + 0.5f, 
                        targetY = ry + 0.5f,
                        direction = UnityEngine.Random.Range(0, 4),
                        baseTexIdx = baseTex,
                        currentTexIdx = baseTex,
                        moveInterval = UnityEngine.Random.Range(1.2f, 1.8f),
                        encounterGroup = generatedGroup // 💡 결정된 정확한 파티 저장
                    });
                    spawned++;
                }
            }
            UpdateSpriteData();
        }

        private void UpdateEnemySprites()
        {
            bool needsRenderUpdate = false;

            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.isAlive) continue;

                if (enemy.isMoving)
                {
                    // 1칸 이동(1.0f 거리) 시 3프레임 애니메이션을 2회(6번 전환) 반복하도록 속도 계산
                    // 이동에 걸리는 총 시간 = 1.0f / enemy.moveSpeed
                    float dynamicAnimSpeed = (1.0f / enemy.moveSpeed) / 6.0f;

                    enemy.animTimer += Time.deltaTime;
                    if (enemy.animTimer >= dynamicAnimSpeed)
                    {
                        enemy.animTimer -= dynamicAnimSpeed;
                        enemy.animFrame = (enemy.animFrame + 1) % 3;
                        needsRenderUpdate = true;
                    }
                }

                float dx = _player.PosX - enemy.x;
                float dy = _player.PosY - enemy.y;
                float angleToPlayer = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                float facingAngle = 0f;
                if (enemy.direction == 0) facingAngle = 90f;  
                if (enemy.direction == 1) facingAngle = 0f;   
                if (enemy.direction == 2) facingAngle = -90f; 
                if (enemy.direction == 3) facingAngle = 180f; 

                float diff = Mathf.DeltaAngle(angleToPlayer, facingAngle);

                int viewSide = 0; 
                if (diff >= -45f && diff <= 45f) viewSide = 0; 
                else if (diff > 45f && diff <= 135f) viewSide = 3; 
                else if (diff < -45f && diff >= -135f) viewSide = 2; 
                else viewSide = 1; 

                int offset = (viewSide * 3) + enemy.animFrame;
                int newTexIdx = enemy.baseTexIdx + offset;

                if (enemy.currentTexIdx != newTexIdx)
                {
                    enemy.currentTexIdx = newTexIdx;
                    needsRenderUpdate = true;
                }
            }

            if (needsRenderUpdate) UpdateSpriteData();
        }

        private EncounterType DetermineEncounterAdvantage(MapEnemy enemy, bool playerInitiated)
        {
            int px = _player.LogicX;
            int py = _player.LogicY;
            int ex = Mathf.FloorToInt(enemy.x);
            int ey = Mathf.FloorToInt(enemy.y);

            int dirToEnemy = (int)VectorToDirection(new Vector2Int(ex - px, ey - py));
            int dirToPlayer = (int)VectorToDirection(new Vector2Int(px - ex, py - ey));

            bool playerFacesEnemy = (_player.DirectionIdx == dirToEnemy);
            bool enemyFacesPlayer = (enemy.direction == dirToPlayer);

            if (playerFacesEnemy && enemyFacesPlayer) 
                return EncounterType.Normal; 

            if (playerFacesEnemy && !enemyFacesPlayer) 
                return EncounterType.Preemptive; 

            if (!playerFacesEnemy && enemyFacesPlayer) 
                return EncounterType.Ambush; 

            return EncounterType.Normal;
        }

        private void ProcessEnemyTurn(MapEnemy enemy)
        {
            int ex = Mathf.FloorToInt(enemy.targetX);
            int ey = Mathf.FloorToInt(enemy.targetY);
            int px = _player.LogicX;
            int py = _player.LogicY;

            float dist = Vector2.Distance(new Vector2(ex, ey), new Vector2(px, py));
            Vector2Int moveDir = Vector2Int.zero;

            if (dist <= enemy.aggroRange && dist > 0)
                moveDir = GetChaseDirection(ex, ey, px, py);
            else
                moveDir = GetRandomWanderDirection(ex, ey);

            if (moveDir != Vector2Int.zero)
            {
                int nextX = ex + moveDir.x;
                int nextY = ey + moveDir.y;

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

        private Vector2Int GetChaseDirection(int ex, int ey, int px, int py)
        {
            int dx = px - ex;
            int dy = py - ey;

            List<Vector2Int> preferredDirs = new List<Vector2Int>();
            
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
            return Vector2Int.zero;
        }

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

        private bool CanEnemyMove(int ex, int ey, Vector2Int dir)
        {
            int tx = ex + dir.x;
            int ty = ey + dir.y;

            if (tx < 0 || tx >= _currentMap.width || ty < 0 || ty >= _currentMap.height) return false;

            int targetEnterFace = -1;
            int currentExitFace = -1;

            if (dir.x > 0)      { targetEnterFace = 3; currentExitFace = 1; } 
            else if (dir.x < 0) { targetEnterFace = 1; currentExitFace = 3; } 
            else if (dir.y > 0) { targetEnterFace = 2; currentExitFace = 0; } 
            else if (dir.y < 0) { targetEnterFace = 0; currentExitFace = 2; } 

            // 현재 칸에서 해당 방향으로 나갈 수 있는지 내벽 검사 (passableWall 예외 처리)
            CellData currentCell = _currentMap.GetCell(ex, ey);
            if (currentCell != null && currentExitFace != -1)
            {
                int texID = currentCell.wallTextureIDs[currentExitFace];
                if (texID != -1 && (theme.passableWallTexIDs == null || !theme.passableWallTexIDs.Contains(texID))) 
                    return false;
            }

            // 목표 칸이 void(-1)인지 먼저 검사
            CellData targetCell = _currentMap.GetCell(tx, ty);
            if (targetCell == null || targetCell.value == -1) return false;

            // 목표 칸으로 해당 방향을 통해 들어갈 수 있는지 외벽 검사 (passableWall 예외 처리)
            if (targetEnterFace != -1)
            {
                int texID = targetCell.wallTextureIDs[targetEnterFace];
                if (texID != -1 && (theme.passableWallTexIDs == null || !theme.passableWallTexIDs.Contains(texID))) 
                    return false;
            }

            // 플레이어 위치일 경우, 겹치기 체크를 무시하고 돌진
            if (tx == _player.LogicX && ty == _player.LogicY) return true;

            foreach (var other in _activeEnemies)
            {
                if (other.isAlive && Mathf.FloorToInt(other.targetX) == tx && Mathf.FloorToInt(other.targetY) == ty)
                    return false;
            }

            return true;
        }

        private void UpdateEnemySpawner()
        {
            if (_spawnDelay <= 0f || _activeEnemies.Count >= _maxSpawnCount)
            {
                _currentSpawnTimer = 0f;
                return;
            }

            if (!_inputLocked)
            {
                _currentSpawnTimer += Time.deltaTime;
                
                if (_currentSpawnTimer >= _spawnDelay)
                {
                    _currentSpawnTimer -= _spawnDelay;
                    SpawnSymbolEnemies(1);
                }
            }
        }

        private void UpdateEnemyAI()
        {
            if (_inputLocked || _player.IsMoving || (systemMessagePanel != null && systemMessagePanel.activeSelf)) 
            {
                return;
            }

            float dt = Time.deltaTime;

            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.isAlive) continue;

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
                    enemy.animFrame = 1; // 정지 이미지
                }

                enemy.moveTimer -= dt;
                if (enemy.moveTimer <= 0f)
                {
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
                        texIdx = obj.texIdx, 
                        isEnemy = false
                    });
                }
            }

            _renderer.UpdateSprites(spriteList.ToArray());
        }

        private void OnGameStateChanged(GameState newState)
        {
            _canRender = (newState == GameState.Exploration);
            if (!_canRender)
            {
                HideSystemMessage();
                HideRoomName();
                return;
            }
            
            ManagerRoot.Sound.PlayBGM(ManagerRoot.Dungeon.GetDungeonTheme(_currentMap.themeName).bgmID);
            RefreshAppVisible();
            CheckFrontForEntranceName();
        }

        public Sprite CaptureCurrentDungeonView()
        {
            if (screenImage == null || screenImage.material == null || screenImage.material.mainTexture == null) return null;

            Texture sourceTex = screenImage.material.mainTexture;
            Texture2D capturedTex = null;

            if (sourceTex is Texture2D t2d)
            {
                capturedTex = new Texture2D(t2d.width, t2d.height, t2d.format, false);
                Graphics.CopyTexture(t2d, capturedTex);
            }
            else
            {
                return null;
            }

            return Sprite.Create(capturedTex, new Rect(0, 0, capturedTex.width, capturedTex.height), new Vector2(0.5f, 0.5f));
        }

        // 3프레임 이미지를 리스트에 합쳐줌
        private void AddSpriteFrames(List<Sprite> list, Sprite[] frames)
        {
            // 방향당 3프레임 기준으로 리스트에 추가
            if (frames != null && frames.Length > 0)
            {
                list.Add(frames[0]); // 1번 프레임
                list.Add(frames.Length > 1 ? frames[1] : frames[0]); // 2번 프레임 (없으면 1번 복사)
                list.Add(frames.Length > 2 ? frames[2] : (frames.Length > 1 ? frames[1] : frames[0])); // 3번 프레임 (없으면 이전 프레임 복사)
            }
            else
            {
                // 이미지가 아예 없을 경우 빈 공간 3개 추가
                list.Add(null);
                list.Add(null);
                list.Add(null);
            }
        }

        // 스폰된 그룹 중 가장 레벨이 높은 몬스터의 ID를 반환 (동률 시 랜덤)
        private string GetHighestLevelMonster(List<string> group)
        {
            if (group == null || group.Count == 0) return null;

            int highestLevel = -1;
            List<string> candidates = new List<string>();

            foreach (string id in group)
            {
                var entry = ManagerRoot.Database.monsterDB.GetEntry(id);
                if (entry != null)
                {
                    if (entry.stats.level > highestLevel)
                    {
                        highestLevel = entry.stats.level;
                        candidates.Clear();
                        candidates.Add(id);
                    }
                    else if (entry.stats.level == highestLevel)
                    {
                        candidates.Add(id); 
                    }
                }
            }

            if (candidates.Count > 0) return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return group[0];
        }
    }
}