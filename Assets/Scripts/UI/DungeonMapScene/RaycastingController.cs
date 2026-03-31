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

        // 서브 시스템
        private RaycastRenderEngine _renderer;
        private DungeonPlayer _player;
        
        // 상태
        private TileAnimState[,] _tileAnimStates;
        private MapData _currentMap;
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

            // 점프 / 스캔
            if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));
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
                        // 아래를 보고 있을 때만 구멍 체크 실행
                        if (_currentLookState == LookState.Down)
                        {
                            Vector2Int fwd = _player.GetForwardVector();
                            int tx = _player.LogicX + fwd.x;
                            int ty = _player.LogicY + fwd.y;

                            CellData targetCell = _currentMap.GetCell(tx, ty);
                            
                            // 앞 타일이 구멍(value == -1)인지 확인
                            if (targetCell != null && targetCell.value == -1)
                            {
                                // 해당 좌표에 설정된 목적지 정보(EntranceData)가 있는지 확인
                                EntranceData holeEntrance = _currentMap.GetEntranceAt(tx, ty);
                                
                                if (holeEntrance != null)
                                {
                                    StartCoroutine(JumpDownRoutine(holeEntrance, fwd));
                                    return; // 아래 복귀 로직 실행 방지
                                }
                            }
                        }
                    }

                    // 확인 키가 아니거나 구멍이 아니면 평소대로 복귀
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
                if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(_player.JumpRoutine(0.6f, 20f, null));

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
            
            float startPitch = _player.Pitch;
            float endPitch = 0f;
            if (targetState == LookState.Up) endPitch = -100f;
            else if (targetState == LookState.Down) endPitch = 100f;

            float startOffset = _player.BackwardOffset;
            float endOffset = (targetState == LookState.None) ? this.backwardOffset : 0f;

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

        private IEnumerator JumpDownRoutine(EntranceData entrance, Vector2Int moveDir)
        {
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
            
            yield return new WaitForSeconds(0.3f);

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

            _player.Pitch = 0f;
            _player.BackwardOffset = this.backwardOffset; // 오프셋 복구
            _currentLookState = LookState.None;
            _inputLocked = false;
            _isLookTransitioning = false;
        }
        
        private void PerformMove(Vector2Int moveVec)
        {
            int tx = _player.LogicX + moveVec.x;
            int ty = _player.LogicY + moveVec.y;

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
            if (_inputLocked) return;
            Debug.Log("ACTION 버튼 클릭됨 (기획 미정)");
            // TODO: 추후 상호작용(문 열기, NPC 대화, 조사 등) 로직 연결
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

            if (backgroundImage != null) backgroundImage.texture = theme.background;
            
            // 시스템 초기화
            _renderer.LoadAssets(theme.texture, 64, 64, null); 
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
            encounterSystem.OnStepTaken();
            
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

        private void OnGameStateChanged(GameState newState)
        {
            _canRender = (newState == GameState.Exploration);
            if (!_canRender) return;
            
            SoundManager.Instance.PlayBGM(DungeonManager.Instance.GetDungeonTheme(_currentMap.themeName).bgmID);
            RefreshAppVisible();
        }
    }
}