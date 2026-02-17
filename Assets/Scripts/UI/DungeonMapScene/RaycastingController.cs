using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using UI.DungeonMapScene;
using UI;

namespace Controller
{
    public class RaycastingController : MonoBehaviour
    {
        // ================= Components =================
        [Header("Settings")]
        public UI.DungeonMapScene.RenderSettings renderSettings;
        [Range(0.0f, 0.499f)] public float backwardOffset = 0.499f;
        public float fovScale = 1f;
        
        [Header("Game References")]
        public RawImage screenImage;
        public RawImage backgroundImage;
        public CompassUI compassUI;
        public GridMap miniMap;
        public AutoMapRenderer autoMapRenderer;
        public DialogueUI dialogueUI;
        public GameObject autoMapContainer;
        public CanvasGroup fadeOverlay;

        [Header("Input")]
        public float doubleTapThreshold = 0.3f;
        public float moveDuration = 0.2f;
        public float turnDuration = 0.2f;
        
        [Header("Encounter System")]
        public EncounterSystem encounterSystem;

        // ================= Sub-Systems =================
        private RaycastRenderEngine _renderer;
        private DungeonPlayer _player;
        
        // ================= State =================
        private TileAnimState[,] _tileAnimStates;
        private MapData _currentMap;
        private bool _canRender = true;
        private bool _inputLocked = false;
        private float _lastWPressTime = -100f;
        private bool _isScanning = false;
        
        // ================= Unity Lifecycle =================
        void Awake()
        {
            _renderer = new RaycastRenderEngine();
            // illusion ID 리스트는 필요 시 Inspector나 LevelManager에서 가져옴
            _player = new DungeonPlayer(this, fovScale, backwardOffset, new List<int>()); 
            
            _player.OnMoveStepTaken += OnPlayerStep;
        }

        void Start()
        {
            _renderer.Initialize(renderSettings.screenWidth, renderSettings.screenHeight);
            
            // Screen Material Setup
            Material mat;
            if (renderSettings.screenMaterial != null)
            {
                mat = new Material(renderSettings.screenMaterial); // 원본 보존을 위해 복제 인스턴스 사용
            }
            else
            {
                // 할당 안 했을 경우 비상용 기본값
                mat = new Material(Shader.Find("UI/Default")); 
            }
            mat.mainTexture = _renderer.ScreenTexture;
            screenImage.material = mat;
            screenImage.rectTransform.localScale = renderSettings.screenScale;

            // Load Initial Map
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
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
                return;  
            }
            
            if (Input.GetKeyDown(KeyCode.O)) 
            {
                ToggleMovementMode();
                return;
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!AppManager.Instance.IsInstalled(AppFeature.AutoMapper)) return;
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
            _renderer.RenderFrame(_player, renderSettings);
            UpdateBackgroundUV();
        }

        // 모드 전환 메서드
        private void ToggleMovementMode()
        {
            if (_player.IsGridMove)
            {
                // Grid -> Free
                _player.IsGridMove = false;
                Debug.Log("Switched to Free Move");
            }
            else
            {
                // Free -> Grid (스냅핑 필요)
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

            // 점프 / 스캔
            if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));
            if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());

            // 이동 (W/S)
            if (Input.GetKey(KeyCode.W)) _player.MoveFree(moveSpeed);
            if (Input.GetKey(KeyCode.S)) _player.MoveFree(-moveSpeed);

            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) 
            {
                _player.RotateFree(rotSpeed); // 왼쪽 회전 (+값인가 -값인가는 DungeonPlayer.RotateFree 구현에 따름)
            }
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) 
            {
                _player.RotateFree(-rotSpeed); // 오른쪽 회전
            }

            // Free Move 시 미니맵 갱신 (부드러운 이동 대신 즉시 갱신)
            if (miniMap) miniMap.SetFreeDirection(_player.DirX, _player.DirY);
            autoMapRenderer.UpdatePlayerIconFree(_player.PosX, _player.PosY, _player.DirX, _player.DirY);
        }

        // ================= Input & Logic =================
        private void HandleInput()
        {
            if (_inputLocked) return;

            // 1. Look (Pitch) - 시점 변경
            if (Input.GetKey(KeyCode.LeftShift))
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) _player.Pitch -= 300f * Time.deltaTime;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) _player.Pitch += 300f * Time.deltaTime;
            }
            else
            {
                // Auto Center Look
                if (Mathf.Abs(_player.Pitch) > 1f)
                    _player.Pitch = Mathf.Lerp(_player.Pitch, 0f, Time.deltaTime * 5f);
            }
            _player.Pitch = Mathf.Clamp(_player.Pitch, -150f, 150f);

            // 2. Action - 기타 기능
            if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(ScanRoutine());
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!AppManager.Instance.IsInstalled(AppFeature.AutoMapper)) return;
                autoMapContainer.SetActive(!autoMapContainer.activeSelf);
            } 
            if (Input.GetKeyDown(KeyCode.P))
            {
                GameSettingManager.Instance.useAnaglyph = !GameSettingManager.Instance.useAnaglyph;
                Debug.Log($"Anaglyph Mode: {GameSettingManager.Instance.useAnaglyph}");
            }

            // 3. Running State Check (이동 중이어도 입력 받아야 함 -> 위치 이동)
            // W, S, 위, 아래 키 중 하나라도 눌리면 더블 탭 체크
            bool anyMoveKeyDown = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || 
                                  Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow);
            
            if (anyMoveKeyDown)
            {
                if (Time.time - _lastWPressTime < doubleTapThreshold)
                {
                    _player.SetRunning(true);
                }
                _lastWPressTime = Time.time;
            }

            // 키를 모두 떼면 달리기 해제
            if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S) && 
                !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow))
            {
                _player.SetRunning(false);
            }

            // 4. Movement Execution (이동은 멈춰있을 때만 가능)
            if (!_player.IsMoving)
            {
                if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));

                // 이동 입력 처리
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) TryMove(1);
                else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) TryMove(-1);
                
                // 좌우 수평 이동 (A/D)
                else if (Input.GetKey(KeyCode.A)) TryStrafe(-1);
                else if (Input.GetKey(KeyCode.D)) TryStrafe(1);
                
                // 회전 (Q/E)
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) StartCoroutine(TurnRoutine(-1));
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) StartCoroutine(TurnRoutine(1));
            }
        }

        // 워프 체크 로직 추가
        private void PerformMove(Vector2Int moveVec)
        {
            int tx = _player.LogicX + moveVec.x;
            int ty = _player.LogicY + moveVec.y;

            // 1. 이동 가능 여부 체크
            bool walkable = _player.IsWalkable(tx, ty, moveVec.x, moveVec.y);
            
            if (walkable)
            {
                float duration = _player.IsRunning ? moveDuration / 2f : moveDuration;
                if (miniMap) miniMap.TranslateToNewPosition(tx, ty, duration);
                StartCoroutine(_player.MoveGridRoutine(tx, ty, duration, null));
            }
            else
            {
                WarpData validWarp = CheckForWarp(_player.LogicX, _player.LogicY, tx, ty, moveVec);

                if (validWarp != null)
                {
                    // 워프가 있다면 레벨 전환 시작
                    Debug.Log($"[Warp] {validWarp.targetMapName}으로 이동합니다.");
                    StartCoroutine(TransitionToLevel(validWarp, moveVec));
                }
                else
                {
                    // 워프도 없다면 벽 충돌 처리
                    StartCoroutine(_player.BumpRoutine(moveVec, 0.2f, 0.3f, null));
                    SoundManager.Instance.PlaySFX(SfxID.Bump_Wall);
                }
            }
        }

        // 워프 데이터 확인 메서드
        private WarpData CheckForWarp(int currentX, int currentY, int targetX, int targetY, Vector2Int moveDir)
        {
            if (_currentMap == null) return null;

            Direction inputDir = VectorToDirection(moveDir);

            // 1. 현재 위치(Source) 검사: "방 안쪽 벽에 있는 워프인가?"
            WarpData currentWarp = _currentMap.GetWarpAt(currentX, currentY);
            if (currentWarp != null && currentWarp.isWallWarp && currentWarp.triggerDirection == inputDir)
            {
                return currentWarp;
            }

            // 2. 목표 위치(Target) 검사: "방 바깥쪽 벽(진입 시)에 있는 워프인가?"
            // (맵 범위를 벗어나지 않았을 때만 검사)
            if (targetX >= 0 && targetX < _currentMap.width && targetY >= 0 && targetY < _currentMap.height)
            {
                WarpData targetWarp = _currentMap.GetWarpAt(targetX, targetY);
                if (targetWarp != null && targetWarp.isWallWarp && targetWarp.triggerDirection == inputDir)
                {
                    return targetWarp;
                }
            }

            return null;
        }

        // 레벨 전환 코루틴
        private IEnumerator TransitionToLevel(WarpData warp, Vector2Int moveDir)
        {
            _inputLocked = true; // 입력 잠금

            // -----------------------------------------------------
            // Phase 1: 페이드 아웃 + 플레이어가 벽 쪽으로 걸어가는 연출
            // -----------------------------------------------------
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                float duration = 0.5f; // 페이드 시간
                
                float startX = _player.PosX;
                float startY = _player.PosY;

                // 목표 지점 (벽 안쪽) 계산
                int targetGridX = _player.LogicX + moveDir.x;
                int targetGridY = _player.LogicY + moveDir.y;
                
                // 플레이어의 GetOffsetPosition을 활용해 목표 좌표 계산
                Vector2 targetPos = _player.GetOffsetPosition(targetGridX, targetGridY, _player.DirectionIdx);

                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = true;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    
                    // 화면 어둡게
                    fadeOverlay.alpha = t;

                    // 플레이어 강제 이동 (시각적 연출)
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

            // -----------------------------------------------------
            // Phase 2: 데이터 로드 및 맵 변경
            // -----------------------------------------------------
            // 매니저를 통해 다음 맵 ID 설정 및 로드
            if (DungeonEventManager.Instance) 
                DungeonEventManager.Instance.SetCurrentMapID(warp.targetMapName);
            
            if (LevelManager.Instance) 
                LevelManager.Instance.LoadLevelFromJson(warp.targetMapName);
            
            // 맵 데이터 갱신 및 플레이어 위치 재설정 (Warp 정보 전달)
            LoadMapData(warp); 

            yield return null; 

            // -----------------------------------------------------
            // Phase 3: 페이드 인
            // -----------------------------------------------------
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

            _inputLocked = false; // 입력 잠금 해제
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
            // 1. 현재 방향과 이동할 다음 방향을 미리 계산
            int currentDir = _player.DirectionIdx;
            // (a % n + n) % n 은 음수 나머지 처리를 위한 공식.
            int nextDir = ((currentDir + dirStep) % 4 + 4) % 4;

            // 2. UI에게 "Current에서 Next로 회전하라"고 지시
            if (compassUI) 
            {
                compassUI.AnimateTurn(currentDir, nextDir, dirStep, turnDuration);
            }

            // 3. 실제 플레이어 데이터 회전 (기존 로직 유지)
            yield return StartCoroutine(_player.RotateGridRoutine(dirStep, turnDuration, null));
            
            // 4. 보정 (혹시 모를 오차 방지)
            if (miniMap) miniMap.SetDirection(_player.DirectionIdx, 0.1f);
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
        }

        // ================= Map & Game Logic =================
        private void LoadMapData(WarpData entryWarp = null)
        {
            _currentMap = LevelManager.Instance.CurrentMapData;
            DungeonTheme theme = LevelManager.Instance.GetTheme(_currentMap.themeName);

            if (backgroundImage != null) backgroundImage.texture = theme.background;
            
            // Init Systems
            _renderer.LoadAssets(theme.texture, 64, 64, null); // Dummy Sprite info
            encounterSystem.Initialize(theme);

            // Init Player Position
            if (entryWarp != null)
            {
                _currentMap.startDirection = entryWarp.targetDirection;
                _currentMap.startX = entryWarp.targetX;
                _currentMap.startY = entryWarp.targetY;
                _player.SetDirectPosition(entryWarp.targetX, entryWarp.targetY, (int)entryWarp.targetDirection);
            }
            else
            {
                _player.SetMapData(_currentMap, _currentMap.startX, _currentMap.startY, _currentMap.startDirection);
            }
            
            if (miniMap != null)
            {
                miniMap.Initialize(_currentMap);
                miniMap.gameObject.SetActive(AppManager.Instance.IsInstalled(AppFeature.LocalRadar));
            }
            if (compassUI)
            {
                compassUI.SetDirection(_player.DirectionIdx);
                compassUI.gameObject.SetActive(AppManager.Instance.IsInstalled(AppFeature.GyroCompass));   
            }
            if (autoMapContainer != null)
            {
                autoMapContainer.SetActive(false);
                autoMapRenderer.DrawFullMap(_currentMap, LevelManager.Instance.CurrentMapState);
            }
            
            // Init Wall Animations
            InitializeWallAnims(theme);
            _renderer.SetMapData(_currentMap, theme, _tileAnimStates);
            
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
        }

        private void OnPlayerStep()
        {
            UpdateMapDiscovery(_player.LogicX, _player.LogicY);
            encounterSystem.OnStepTaken();
            
            // Check Event (Dialogue)
            string eventID = DungeonEventManager.Instance.CheckEvent(_player.LogicX, _player.LogicY);
            if (!string.IsNullOrEmpty(eventID))
            {
                _inputLocked = true;
                dialogueUI.OnDialogueFinished -= OnDialogueEnd;
                dialogueUI.OnDialogueFinished += OnDialogueEnd;
                dialogueUI.StartDialogue(eventID);
            }
        }

        private void OnDialogueEnd()
        {
            _inputLocked = false;
        }

        private void UpdateMapDiscovery(int x, int y)
        {
            LevelManager.Instance.CurrentMapState.MarkVisited(x, y);
            autoMapRenderer.RevealCell(x, y);
            autoMapRenderer.UpdatePlayerIcon(x, y, (Direction)_player.DirectionIdx);
            MapManager.Instance.UpdatePlayerPosition(x, y, (Direction)_player.DirectionIdx, _currentMap.mapID);
        }

        private void InitializeWallAnims(DungeonTheme theme)
        {
            if (theme == null || theme.wallAnimations == null) return;
            _tileAnimStates = new TileAnimState[_currentMap.width, _currentMap.height];

            // 딕셔너리로 변환하여 검색 속도 향상
            Dictionary<int, WallAnimConfig> animDict = new Dictionary<int, WallAnimConfig>();
            foreach (var cfg in theme.wallAnimations)
                if (!animDict.ContainsKey(cfg.baseTexId)) animDict.Add(cfg.baseTexId, cfg);

            if (animDict.Count == 0) return;

            // 전체 맵 순회
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
            // Expand
            while (radius < renderSettings.maxScanDistance)
            {
                radius += Time.deltaTime * renderSettings.scanSpeed;
                _renderer.SetScanState(true, radius);
                yield return null;
            }
            radius = renderSettings.maxScanDistance;
            
            // Wait
            yield return new WaitForSeconds(renderSettings.scanWaitTime);
            
            // Contract
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

        private void OnGameStateChanged(GameState newState)
        {
            _canRender = (newState == GameState.Exploration);
            if (_canRender) SoundManager.Instance.PlayBGM(LevelManager.Instance.GetTheme(_currentMap.themeName).bgmID);
        }
    }
}