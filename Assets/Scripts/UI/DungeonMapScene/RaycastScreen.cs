using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using DG.Tweening;
using TMPro;

namespace UI.DungeonMapScene
{
    public class RaycastScreen : MonoBehaviour
    {
        [Header("Display Settings")]
        public Material screenMaterial; // 인스펙터에서 DungeonScreenMat 연결

        [Header("Visual Effects")]
        public bool useWallDistortion = false;
        public float distortionFreq = 0.5f;
        public float distortionAmp = 2.0f;
         // 실린더 효과 켜기
        public bool useCylinderEffect = false;
        [Range(-10f, 10f)]
        public float cylinderStrength = 3.0f; // 곡률 강도 (양수: 볼록, 음수: 오목)
         // 애너글리프 3D 효과 켜기/끄기
        public static bool useAnaglyph = false;
        [Range(0.03f, 0.07f)]
        public float stereoSeparation = 0.05f; // 두 눈 사이의 거리 (값이 클수록 입체감이 강해짐)

        private Color[] _leftEyeBuffer; // 애너글리프 사용 시 왼쪽 눈 렌더링 결과를 저장할 임시 버퍼

        [Header("Transition Settings")]
        public CanvasGroup fadeOverlay; // 인스펙터에서 할당 (검은색 패널)
        public float fadeDuration = 0.5f; // 페이드 효과 지속 시간

        [Header("Bump Effect Settings")]
        public float bumpDuration = 0.2f;   // 벽에 튕기는 시간
        public float bumpIntensity = 0.3f;  // 벽으로 밀리는 거리 (0.5 이하로만)

        [Header("Look (Pitch) Settings")]
        public float lookSpeed = 300.0f;  // 시점 변경 속도
        public float maxPitch = 150.0f;   // 최대 시점 제한 (픽셀 단위, 화면 절반 넘지 않게)
        public bool autoCenterLook = true; // 이동 시 시점 자동 복귀 여부

        private float _currentPitch = 0f; // 현재 시점 오프셋 (양수=위, 음수=아래)
        private bool IsViewSkewed => Mathf.Abs(_currentPitch) > 1.0f; // 시점이 위나 아래로 쏠려있는지 확인 (약간의 오차 허용)

        [Header("Wall Animation Settings")]
        public WallAnimConfig[] wallAnimations; // 인스펙터에서 설정

        // 맵 크기(width x height)와 동일한 2차원 배열로 상태 관리
        private TileAnimState[,] _tileAnimStates;

        [Header("Background")]
        public RawImage backgroundImage; // 인스펙터에서 배경 RawImage 연결

        [Header("View Settings")]
        // 그리드 중앙으로부터 뒤로 얼마나 물러날지 결정 (0.0 = 정중앙, 0.5 = 타일 끝)
        // 0 이상 ~ 0.5 미만이어야 함.
        [Range(0.0f, 0.499f)]
        public float backwardOffset = 0.499f;
        public float fovScale = 1f; // 기본값 0.66 (약 66도), 1.0이면 90도

        private float _cachedOffset = -1f; // 오프셋 변경 감지용

        [Header("Input Settings")]
        public float doubleTapThreshold = 0.3f; // 더블 탭 판정 시간 (초)

        private float _lastWPressTime = -100f; // 키를 누른 마지막 시간
        private bool _isRunning = false;       // 현재 달리기 모드인지 여부

        [Header("Jump Settings")]
        public float jumpDuration = 0.6f; // 점프 체공 시간
        public float jumpHeight = 20.0f;  // 점프 높이 (픽셀 단위 오프셋, 해상도에 따라 조절 필요)
        
        private float _currentJumpOffset = 0f; // 현재 프레임의 수직 오프셋 값
        private bool _isJumping = false;

        
        [Header("Encounter Settings")]
        public int minSteps = 15; // 최소 15걸음은 안전
        public int maxSteps = 30; // 최대 30걸음 안에는 무조건 전투
        
        private int stepsUntilNextBattle; // 다음 전투까지 남은 걸음
        private int _initialSteps; // 초기 걸음 수 (비율 계산용)

        [Header("Encounter UI")]
        public Slider dangerSlider;
        public TextMeshProUGUI dangerText;
        public Image fillImage; // 슬라이더의 Fill 영역 이미지 (색상 변경용)
        
        public Color32 safeColor = Color.green;
        public Color32 warningColor = Color.yellow;
        public Color32 dangerColor = Color.red;
        private Tween _pulseTween;
        
        [Header("Movement Settings")]
        public float gridBaseTurnDuration = .2f; 
        public float gridBaseMoveDuration = .2f;
        public float runMultiplier = 2.0f; // Shift 누르면 2배 빨라짐

        [Header("Lighting Settings")]
        public float lightingIntensity = 3.5f; 
        public bool useGridLighting = true; // true면 그리드 단위로 밝기 끊어짐

        private int precalcLightScale = 255;

        [Header("Scanner Effect Settings")]
        public Color32 wireframeColor = Color.green; // 벽 와이어프레임 색상
        public Color32 floorWireframeColor = new Color(0f, 0.5f, 0f); // 바닥/천장 와이어프레임
        public Color32 pulseColor = Color.white;
        public float scanSpeed = 15.0f; 
        public float maxScanDistance = 20.0f; 
        public float pulseWidth = 0.5f; // 경계선의 두께 (조절 가능)
        public float scanWaitTime = 2.0f; // 최대로 퍼진 후 대기하는 시간
        public float returnSpeedMultiplier = 1.5f; // 돌아올 때의 속도 배율 (1.0이면 갈때랑 똑같이, 높으면 더 빨리 돌아옴)


        [Header("References")]
        public GameObject screenContainer;
        public CompassUI compassUI;
        public GridMap miniMap; 
        public GameObject autoMapContainer;
        public AutoMapRenderer autoMapRenderer; // autoMap에 포함된 렌더러
        public GameObject controllerPanel;
        public DialogueUI dialogueUI; // 인스펙터에서 할당
        
        private DungeonMapState currentMapState; // 현재 오토맵의 상태
        
        // 심도 조명 계산용 고정 좌표
        private int _logicX, _logicY;

        private float _currentScanRadius = 0f;
        private bool _isScanning = false;

        public bool isInputLocked = false;

        private bool canRender = true; // 렌더링 허용 여부 플래그

        //Shift 키를 누르거나, 키 더블 탭 유지 중일 때 빨라짐
        private float CurrentMoveDuration => 
            _isRunning ? (gridBaseMoveDuration / runMultiplier) : gridBaseMoveDuration;

        private float CurrentTurnDuration => 
            Input.GetKey(KeyCode.LeftShift) ? (gridBaseTurnDuration / runMultiplier) : gridBaseTurnDuration;

        [Header("Secret Settings")]
        // 이 리스트에 포함된 텍스처 ID를 가진 벽면은 통과 가능 (일루전 월)
        public List<int> illusionTextureIds = new List<int>(); 

        // 내부 렌더링 해상도 (실제 화면보다 작게 그려서 확대함)
        private int screenWidth = 512;
        private int screenHeight = 256;
        private Vector2 screenScale = new Vector2(2.5f, 2.8125f); // 저해상도를 확대해서 보여줄 비율 (1280 / screenWidth, 720 / screenHeight)
        private Vector2 battleScreenScale = new Vector2(0.8f, 0.7111f); // 전투 UI가 표시될 때의 화면 비율

        private int texWidth = 64;  // 텍스처 가로 크기 (2의 n승 권장)
        private int texHeight = 64; // 텍스처 세로 크기

        // 플레이어 상태 변수
        private int _direction = 0; // 0:North, 1:East, 2:South, 3:West
        private float _posX, _posY; // 현재 위치
        private float _dirX = -1.0f, _dirY = 0.0f; // 바라보는 방향 벡터
        private float _planeX = 0.0f, _planeY = 0.0f; // 카메라 평면 벡터
        private bool _isMoving; // 현재 이동/회전 애니메이션 중인지 체크
        public bool IsMoving => _isMoving;
        
        private Coroutine _moveCoroutine;

        // 맵 및 렌더링 데이터
        private MapData _worldMap;
        private Texture2D[] _textures;
        private SpriteInfo[] _sprtData;
        
        private int _spriteNum;
        private int[] _spriteOrder;
        private float[] _spriteDistance;

        private int _ceilTexIdx; // 천장 텍스쳐 인덱스 
        private int _floorTexIdx; // 바닥 텍스쳐 인덱스

        private Color32[] _buffer;    // 화면에 그려질 픽셀 색상 배열
        private Color32[] _flatTexturePixels;
        private float[] _zBuffer;   // 깊이 버퍼 (스프라이트 가림 처리용)
        
        // 유니티 컴포넌트
        private RawImage _rawImg;
        private Texture2D _screenTexture;

        public bool isGridMove = true;

        private DungeonTheme currentTheme;

        private BgmID bgmID;

        void Start()
        {
            // 버퍼 메모리 할당
            _buffer = new Color32[screenWidth * screenHeight];
            _leftEyeBuffer = new Color[screenWidth * screenHeight]; // 애너글리프용
            _zBuffer = new float[screenWidth];

            if (controllerPanel != null)
            {
                controllerPanel.SetActive(false);
            }

            // 데이터 로드 (DungeonManager에서 가져옴)
            LoadMapData();

            // [최적화] 텍스처 픽셀 샘플링 (Start 시점에 미리 수행)
            PrecomputeTexturePixels();

            // 화면 생성 및 최초 렌더링
            CreateScreen();
            Render();

            // 1. 던전 상태 이벤트 구독
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            
            // 2. 초기 상태
            GameStateManager.Instance.ChangeState(GameState.Exploration);
        }

        // 스크립트가 꺼지거나 씬이 바뀔 때 안전하게 상태 초기화
        private void OnDisable()
        {
            StopAllCoroutines(); // 이동 중이던 코루틴 강제 종료
            _isMoving = false;   // 입력 잠금 해제 (중요!)
        }

        // 인스펙터에서 값을 바꾸면 즉시 적용되는 유니티 이벤트 함수
        private void OnValidate()
        {
            // 게임 실행 중에만 반영
            if (Application.isPlaying && _worldMap != null)
            {
                UpdateDirectionVectors();
                Render();
            }
        }

        private void InitializeWallAnimations(DungeonTheme theme)
        {
            // 방어 코드: 테마에 애니메이션 설정이 없으면 패스
            if (_worldMap == null || theme == null || theme.wallAnimations == null) return;

            int w = _worldMap.width;
            int h = _worldMap.height;
            _tileAnimStates = new TileAnimState[w, h];

            // 1. 테마(ScriptableObject)에 있는 설정을 딕셔너리로 변환
            Dictionary<int, WallAnimConfig> animDict = new Dictionary<int, WallAnimConfig>();
            foreach (var cfg in theme.wallAnimations)
            {
                if (!animDict.ContainsKey(cfg.baseTexId))
                    animDict.Add(cfg.baseTexId, cfg);
            }

            // 애니메이션 설정이 하나도 없으면 루프 돌 필요 없음
            if (animDict.Count == 0) return;

            // 2. 전체 맵 순회 및 상태 생성
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    CellData cell = _worldMap.GetCell(x, y);
                    if (cell != null && cell.HasWall())
                    {
                        foreach (int texID in cell.wallTextureIDs)
                        {
                            if (animDict.ContainsKey(texID))
                            {
                                // 해당 타일에 애니메이션 상태 할당
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

        // =========================================================
        // 최하단의 배경 스크롤
        // =========================================================
        private void UpdateBackgroundUV()
        {
            if (backgroundImage == null) return;

            // 플레이어의 방향(각도)에 따라 UV의 x좌표를 이동시킴
            // 0 ~ 360도를 0.0 ~ 1.0으로 매핑
            // Atan2를 사용하여 현재 바라보는 정확한 각도 계산
            float angle = Mathf.Atan2(_dirY, _dirX) * Mathf.Rad2Deg;
            
            // UV는 0~1 사이에서 반복되므로 각도를 정규화
            float uvX = -angle / 360.0f; 

            // Rect 수정 (x, y, width, height)
            Rect uv = backgroundImage.uvRect;
            uv.x = uvX; 
            backgroundImage.uvRect = uv;
        }

        
        // =========================================================
        // Helper: 오프셋이 적용된 좌표 구하기
        // =========================================================

        /*
        * 그리드 좌표(x, y)와 바라볼 방향(dir)을 입력받아
        * '중앙에서 살짝 뒤로 물러난' 실제 월드 좌표를 반환.
        */
        private Vector2 GetOffsetPosition(int gridX, int gridY, int dirIdx)
        {
            // 1. 해당 그리드의 정중앙 좌표
            Vector2 centerPos = new Vector2(gridX + 0.5f, gridY + 0.5f);

            // 2. 바라보는 방향의 벡터 가져오기
            (Vector2 dirVec, Vector2 _) = GetVectorsForDirection(dirIdx);

            // 3. 방향의 반대(-dirVec) 쪽으로 offset만큼 이동
            // 예: 북쪽(-1, 0)을 보면, 위치는 중앙에서 남쪽(+1, 0)으로 살짝 밀려남
            return centerPos - (dirVec * backwardOffset);
        }

        private void LoadMapData(WarpData entryWarp = null)
        {
            // 몬스터 인카운터 설정
            ResetEncounterSteps();

            _worldMap = LevelManager.Instance.CurrentMapData;
            currentMapState = LevelManager.Instance.CurrentMapState;
            
            autoMapContainer.SetActive(false);
            autoMapRenderer.DrawFullMap(_worldMap, currentMapState);

            if (miniMap != null)
            {
                // entryWarp가 있을 경우, 기본 위치와 방향을 사용하지 않고 entryWarp에 설정된 것을 사용한다.
                if (entryWarp != null)
                {
                    _worldMap.startDirection = entryWarp.targetDirection;
                    _worldMap.startX = entryWarp.targetX;
                    _worldMap.startY = entryWarp.targetY;
                }
                miniMap.Initialize(_worldMap);
            } 
            currentTheme = LevelManager.Instance.GetTheme(_worldMap.themeName);
            backgroundImage.texture = currentTheme.background;

            if (currentTheme.bgmID != bgmID)
            {
                bgmID = currentTheme.bgmID;
                SoundManager.Instance.PlayBGM(bgmID);
            }

            _textures = currentTheme.texture;
            _ceilTexIdx = currentTheme.ceilingTexIdx;
            _floorTexIdx = currentTheme.floorTexIdx;
            //_sprtData = data.DUMMY_MAP_SPRITE_DATA;

            // 시작 위치 설정
            _direction = (int)_worldMap.startDirection;

            if (compassUI != null) compassUI.SetDirection(_direction);
            
            UpdateMapDiscovery(_worldMap.startX, _worldMap.startY);

            Vector2 startPos = GetOffsetPosition(_worldMap.startX, _worldMap.startY, _direction);
            _posX = startPos.x;
            _posY = startPos.y;

            UpdateDirectionVectors();

            InitializeWallAnimations(currentTheme);

            // 초기 논리 좌표 설정
            _logicX = Mathf.FloorToInt(_posX);
            _logicY = Mathf.FloorToInt(_posY);

            MapManager.Instance.UpdatePlayerPosition(_logicX, _logicY, _worldMap.startDirection, _worldMap.mapID);
        }

        private void InitializeWallDatabaseDummy()
        {
            if (_sprtData != null && _sprtData.Length > 0) {
                _spriteNum = _sprtData.Length;
                _spriteOrder = new int[_spriteNum];
                _spriteDistance = new float[_spriteNum];
            }
        }

        // [최적화 핵심] 모든 텍스처를 하나의 거대한 1차원 색상 배열로 변환
        private void PrecomputeTexturePixels()
        {
            if (!Mathf.IsPowerOfTwo(texWidth) || !Mathf.IsPowerOfTwo(texHeight))
            {
                Debug.LogError($"[RaycastScreen] 텍스처 크기는 반드시 2의 승수여야 합니다! 현재: {texWidth}x{texHeight}");
                return;
            }
            // 전체 배열 크기 = (텍스처 개수) * (가로) * (세로)
            int pixelsPerTexture = texWidth * texHeight;
            int totalPixels = _textures.Length * pixelsPerTexture;
            
            // 최적화: Color32 사용 (메모리 절약)
            _flatTexturePixels = new Color32[totalPixels]; 

            for (int i = 0; i < _textures.Length; i++)
            {
                // 1. 현재 텍스처의 픽셀들을 가져옴
                Color[] sourcePixels = _textures[i].GetPixels();

                // 2. 오프셋 계산
                // 이 텍스처가 전체 배열의 어디서부터 시작해야 하는지 결정
                int offset = i * pixelsPerTexture;

                // 3. 픽셀 복사 (Color -> Color32 변환)
                for (int p = 0; p < sourcePixels.Length; p++)
                {
                    // _flatTexturePixels의 [시작점 + p] 위치에 저장
                    _flatTexturePixels[offset + p] = (Color32)sourcePixels[p];
                }
            }
        }

        // 최적화된 1차원 배열에서 색상을 가져오는 함수
        private Color32 GetPixelFast(int texIdx, int x, int y)
        {
            if (texIdx < 0 || texIdx >= _textures.Length) return new Color32(255, 0, 255, 255);

            x = x & (texWidth - 1); 
            y = y & (texHeight - 1);

            int index = (texIdx * texWidth * texHeight) + (y * texWidth) + x;
            return _flatTexturePixels[index];
        }

        private bool UpdateWallAnimations()
        {
            if (_tileAnimStates == null) return false;

            float dt = Time.deltaTime;
            int w = _tileAnimStates.GetLength(0);
            int h = _tileAnimStates.GetLength(1);
            
            bool anyChanged = false; // 변경 사항이 있었는지 체크하는 플래그

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    TileAnimState state = _tileAnimStates[x, y];
                    
                    // 애니메이션 중인 타일만 계산
                    if (state != null && state.isAnimating)
                    {
                        state.timer -= dt;
                        if (state.timer <= 0)
                        {
                            // 상태 토글
                            state.showAlt = !state.showAlt;
                            
                            // 타이머 리셋
                            state.timer = UnityEngine.Random.Range(state.config.minInterval, state.config.maxInterval);
                            
                            // [핵심] 화면이 바뀌어야 함을 알림
                            anyChanged = true; 
                        }
                    }
                }
            }
            
            return anyChanged; // 변경 여부 반환
        }
        
        void OnDestroy()
        {
            _pulseTween?.Kill();
            
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        // [핵심] 상태가 바뀔 때마다 자동으로 호출되는 함수
        void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Exploration)
            {
                // 탐험 모드: 렌더링 재개
                canRender = true;
                Debug.Log("탐험 모드 복귀: 렌더링 시작");
                SoundManager.Instance.PlayBGM(currentTheme.bgmID);
            }
            else
            {
                // 전투/메뉴 등: 렌더링 중지 (성능 확보)
                canRender = false;
            }
        }

        void Update()
        {
            if (!canRender) return;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                GameStateManager.Instance.ChangeState(GameState.PlayerMenu);
                return;  
            } 
            
            // 애니메이션 상태 업데이트 및 변경 여부 확인
            bool animUpdated = UpdateWallAnimations();

            if (isInputLocked) return;

            // 시점 입력 처리 및 변경 확인
            bool lookUpdated = HandleLookInput();

            if (Input.GetKeyDown(KeyCode.Tab)) ToggleMovementMode();
            
            if (Input.GetKeyDown(KeyCode.M)) autoMapContainer.SetActive(!autoMapContainer.activeSelf);
            if (Input.GetKeyDown(KeyCode.P))
            {
                // 애너글리프 토글
                useAnaglyph = !useAnaglyph;
                Render(); // 즉시 반영
                Debug.Log($"Anaglyph Mode: {useAnaglyph}");
            }

            if (isGridMove)
            {
                if (!lookUpdated) HandleInput();

                if (!_isMoving)
                {
                    _logicX = Mathf.FloorToInt(_posX);
                    _logicY = Mathf.FloorToInt(_posY);
                }

                if (_isMoving || animUpdated || lookUpdated)
                {
                    Render();
                }
                else
                {
                    // backwardOffset 실시간 변경 감지
                    if (Mathf.Abs(_cachedOffset - backwardOffset) > 0.001f)
                    {
                        _cachedOffset = backwardOffset;
                        
                        // 오차 없이 현재 타일의 인덱스를 구함
                        // 소수점 이하는 절삭하여 좌표의 이동을 막는다
                        int gridX = Mathf.FloorToInt(_posX);
                        int gridY = Mathf.FloorToInt(_posY);
                        
                        Vector2 fixedPos = GetOffsetPosition(gridX, gridY, _direction);
                        
                        _posX = fixedPos.x;
                        _posY = fixedPos.y;
                        
                        Render();
                    }
                }
            }
            else
            {
                if (!lookUpdated) HandleFreeMoveInput();
                Render();
            }
        }

        // =========================================================
        // Render Logic (Raycasting)
        // =========================================================
        // step이 1이면 정밀 렌더링, 2면 고속(반해상도) 렌더링
        private void PerformRenderPass(int step)
        {
            // 각 함수에도 step을 전달
            CastFloorAndCeiling(step);
            CastWalls(step);
            
            // 스프라이트는 굳이 step을 적용 안 해도(또는 복잡해서) 
            // 성능 영향이 적다면 그대로 둬도 되지만, 최적화를 위해 적용 추천
            if (_sprtData != null && _sprtData.Length > 0) CastSprites(step);
        }

        private void Render()
        {
            // 버퍼를 투명값(0,0,0,0)으로 초기화하여 이전 프레임 잔상 제거
            Array.Clear(_buffer, 0, _buffer.Length);

            // 깊이 버퍼도 초기화 (안전장치)
            Array.Clear(_zBuffer, 0, _zBuffer.Length);

            if (useAnaglyph)
            {
                float originalX = _posX;
                float originalY = _posY;
                
                // [최적화] 3D 모드에서는 가로 해상도를 절반으로 낮춰서 연산량 보존
                int step = 2; 

                // 1. Left Eye
                _posX = originalX - _planeX * stereoSeparation;
                _posY = originalY - _planeY * stereoSeparation;
                PerformRenderPass(step); 
                Array.Copy(_buffer, _leftEyeBuffer, _buffer.Length);
                
                // 왼쪽 눈 그린 뒤 버퍼를 다시 비워야 오른쪽 눈이 깨끗하게 그려짐
                Array.Clear(_buffer, 0, _buffer.Length); 

                // 2. Right Eye
                _posX = originalX + _planeX * stereoSeparation;
                _posY = originalY + _planeY * stereoSeparation;
                PerformRenderPass(step); 

                // 3. Merge
                for (int i = 0; i < _buffer.Length; i++)
                {
                    Color32 left = _leftEyeBuffer[i];
                    Color32 right = _buffer[i];
                    // 투명도 고려하여 병합 (둘 다 투명하면 투명)
                    byte alpha = (byte)Mathf.Max(left.a, right.a);
                    _buffer[i] = new Color32(left.r, right.g, right.b, alpha);
                }

                _posX = originalX;
                _posY = originalY;
            }
            else
            {
                // 일반 모드는 모든 픽셀 정밀 계산 (step = 1)
                PerformRenderPass(1); 
            }
            
            _screenTexture.LoadRawTextureData(
                System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(_buffer, 0),
                _buffer.Length * 4 // byte size
            );
            _screenTexture.Apply();

            UpdateBackgroundUV();
        }

        private void CastWalls(int step)
        {
            // x를 1씩 증가시키는 대신 step만큼 건너뛰며 반복
            for (int x = 0; x < screenWidth; x += step)
            {
                // ---------------------------------------------------------
                // 1. 레이(Ray) 초기화
                // ---------------------------------------------------------
                float cameraX = 2 * x / (float)screenWidth - 1; // -1 ~ 1
                float rayDirX = _dirX + _planeX * cameraX;
                float rayDirY = _dirY + _planeY * cameraX;

                int mapX = Mathf.FloorToInt(_posX);
                int mapY = Mathf.FloorToInt(_posY);

                // DDA 변수
                float sideDistX, sideDistY;
                // 0으로 나누는 경우 방지 (무한대 대신 아주 큰 값 사용)
                float deltaDistX = (rayDirX == 0) ? 1e30f : Mathf.Abs(1 / rayDirX);
                float deltaDistY = (rayDirY == 0) ? 1e30f : Mathf.Abs(1 / rayDirY);
                float perpWallDist;

                int stepX, stepY;
                int hit = 0; 
                int side = 0; // 0: 세로선(NS), 1: 가로선(EW)
                bool hitBackFace = false;
                int hitTexId = -1; 

                // ---------------------------------------------------------
                // 2. 초기 스텝 및 sideDist 계산
                // ---------------------------------------------------------
                if (rayDirX < 0) { stepX = -1; sideDistX = (_posX - mapX) * deltaDistX; }
                else             { stepX = 1;  sideDistX = (mapX + 1.0f - _posX) * deltaDistX; }
                
                if (rayDirY < 0) { stepY = -1; sideDistY = (_posY - mapY) * deltaDistY; }
                else             { stepY = 1;  sideDistY = (mapY + 1.0f - _posY) * deltaDistY; }

                // ---------------------------------------------------------
                // 3. DDA 알고리즘 (벽 찾기)
                // ---------------------------------------------------------
                while (hit == 0)
                {
                    int prevMapX = mapX;
                    int prevMapY = mapY;

                    // 다음 칸으로 이동
                    if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; side = 0; }
                    else                       { sideDistY += deltaDistY; mapY += stepY; side = 1; }

                    // 맵 범위 체크
                    if (mapX < 0 || mapX >= _worldMap.width || mapY < 0 || mapY >= _worldMap.height)
                    {
                        hit = 1; 
                        // 맵 끝에 도달했을 때 마지막 칸의 벽 정보 가져오기
                        CellData lastCell = _worldMap.GetCell(prevMapX, prevMapY);
                        if (lastCell != null)
                        {
                            int boundaryTexId = GetTextureIdOnSide(lastCell, side, stepX, stepY, true);
                            hitTexId = (boundaryTexId != -1) ? boundaryTexId : 0;
                        }
                        else hitTexId = 0;
                        hitBackFace = true;
                    }
                    else 
                    {
                        // 벽 충돌 검사
                        // A. 진입면 (Front Face)
                        CellData cell = _worldMap.GetCell(mapX, mapY);
                        if (cell != null && cell.HasWall())
                        {
                            int frontTexId = GetTextureIdOnSide(cell, side, stepX, stepY, false);
                            if (frontTexId != -1)
                            {
                                hit = 1;
                                hitBackFace = false;
                                hitTexId = frontTexId;
                            }
                        }

                        // B. 이탈면 (Back Face) - 지나온 칸의 뒷면 확인
                        if (hit == 0)
                        {
                            cell = _worldMap.GetCell(prevMapX, prevMapY);
                            if (cell != null && cell.HasWall())
                            {
                                int backTexId = GetTextureIdOnSide(cell, side, stepX, stepY, true);
                                if (backTexId != -1)
                                {
                                    hit = 1;
                                    hitBackFace = true;
                                    hitTexId = backTexId;
                                }
                            }
                        }
                    }
                }

                // 벽을 되돌려 좌표 보정 (BackFace인 경우)
                if (hitBackFace)
                {
                    if (side == 0) mapX -= stepX;
                    else           mapY -= stepY;
                }

                // ---------------------------------------------------------
                // 4. 텍스처 애니메이션 교체 로직
                // ---------------------------------------------------------
                if (hitTexId != -1 && 
                    mapX >= 0 && mapX < _tileAnimStates.GetLength(0) &&
                    mapY >= 0 && mapY < _tileAnimStates.GetLength(1))
                {
                    TileAnimState state = _tileAnimStates[mapX, mapY];
                    if (state != null && state.isAnimating && state.showAlt)
                    {
                        if (hitTexId == state.config.baseTexId) hitTexId = state.config.altTexId;
                    }
                }

                // ---------------------------------------------------------
                // 5. 거리 및 높이 계산
                // ---------------------------------------------------------
                if (side == 0) perpWallDist = (sideDistX - deltaDistX);
                else           perpWallDist = (sideDistY - deltaDistY);

                // =========================================================
                // 실린더 효과를 여기서 적용해야 벽의 높이가 변합니다.
                // =========================================================
                if (useCylinderEffect)
                {
                    // cameraX: 화면 왼쪽(-1) ~ 중앙(0) ~ 오른쪽(1)
                    float distFactor = cameraX * cameraX; 
                    
                    // 가장자리로 갈수록 거리를 조작하여 벽 높이를 바꿈
                    float distortion = 1.0f + (distFactor * cylinderStrength);
                    
                    perpWallDist *= distortion;
                }
                // =========================================================

                // 스캔 효과용 플래그
                bool renderWireframe = _isScanning && (perpWallDist < _currentScanRadius);

                // 0으로 나누기 방지
                if (perpWallDist <= 0.001f) perpWallDist = 0.001f;

                // 화면 높이 계산 (FOV Scale, Pitch, Jump 반영)
                float heightScale = 0.66f / fovScale;
                int horizon = (int)(screenHeight / 2 - _currentJumpOffset + _currentPitch);
                int lineHeight = (int)((screenHeight / perpWallDist) * heightScale);

                int drawStart = -lineHeight / 2 + horizon;
                if (drawStart < 0) drawStart = 0;
                int drawEnd = lineHeight / 2 + horizon;
                if (drawEnd >= screenHeight) drawEnd = screenHeight - 1;

                // ---------------------------------------------------------
                // 6. 텍스처 좌표 (X) 계산
                // ---------------------------------------------------------
                float wallX; 
                if (side == 0) wallX = _posY + perpWallDist * rayDirY;
                else           wallX = _posX + perpWallDist * rayDirX;
                wallX -= Mathf.Floor(wallX);

                int texX = (int)(wallX * (float)texWidth);
                // 텍스처 반전 처리 (벽의 방향에 따라 좌우가 뒤집히는 것 방지)
                if ((side == 0 && rayDirX > 0) ^ hitBackFace) texX = texWidth - texX - 1;
                if ((side == 1 && rayDirY < 0) ^ hitBackFace) texX = texWidth - texX - 1;

                // ---------------------------------------------------------
                // 7. 조명(Gamma) 계산
                // ---------------------------------------------------------
                // 조명(Gamma) 값을 0~255 정수로 미리 계산
                int lightScale = 255;
                if (useGridLighting)
                {
                    float distX = Mathf.Abs(mapX - _logicX);
                    float distY = Mathf.Abs(mapY - _logicY);
                    float dist = Mathf.Max(distX, distY);
                    // 0.0~1.0 float를 0~255 int로 변환
                    lightScale = (int)(Mathf.Clamp(lightingIntensity / (dist + 1.0f), 0f, 1f) * 255);
                }
                else
                {
                    lightScale = (int)(Mathf.Clamp(lightingIntensity / perpWallDist, 0f, 1f) * 255);
                }
                if (side == 1) lightScale = (lightScale * 230) >> 8; // 약 0.9배 (230/256)
                // ---------------------------------------------------------
                // 8. 수직선 그리기 (Texture Mapping Loop)
                // ---------------------------------------------------------
                int trueDrawStart = -lineHeight / 2 + horizon;
                float stepVal = 1.0f * texHeight / lineHeight; // 텍스처 Y 증가량
                float texPos = (drawStart - trueDrawStart) * stepVal;

                for (int y = drawStart; y < drawEnd; y++)
                {
                    Color32 color;

                    if (renderWireframe)
                    {
                        // --- 와이어프레임/스캔 모드 ---
                        float distanceToScanBoundary = Mathf.Abs(perpWallDist - _currentScanRadius);
                        bool isPulseEdge = distanceToScanBoundary < pulseWidth;
                        bool isVerticalEdge = (texX == 0 || texX == texWidth - 1); 
                        bool isHorizontalEdge = (y == drawStart || y == drawEnd - 1);
                        
                        if (isPulseEdge) color = pulseColor; 
                        else if (isVerticalEdge || isHorizontalEdge) color = wireframeColor;
                        else color = Color.black;
                    }
                    else
                    {
                        // --- 일반 텍스처 모드 ---
                        int sampleTexX = texX;

                        // 벽 울렁거림 효과 (Distortion)
                        if (useWallDistortion)
                        {
                            float wave = Mathf.Sin((y + x) * distortionFreq) * distortionAmp;
                            sampleTexX = (int)(texX + wave) & (texWidth - 1); 
                        }

                        // 텍스처 Y 좌표 계산 (비트 연산 활용을 위해 texHeight는 2의 승수여야 함)
                        int d = y * 256 - screenHeight * 128 + lineHeight * 128 - (int)_currentPitch * 256 + (int)_currentJumpOffset * 256;
                        int texY = ((d * texHeight) / lineHeight) / 256;
                        
                        color = GetPixelFast(hitTexId, texX, texY);

                        // [중요 3] 정수 비트 연산으로 조명 적용 (매우 빠름)
                        // Color32 구조체는 r,g,b가 byte입니다.
                        if (lightScale < 255)
                        {
                            color.r = (byte)((color.r * lightScale) >> 8); // 나누기 256 대신 비트 시프트
                            color.g = (byte)((color.g * lightScale) >> 8);
                            color.b = (byte)((color.b * lightScale) >> 8);
                        }

                        // 버퍼에 쓰기
                        int bufferIndex = y * screenWidth + x;
                        
                        // Step 처리 (가로로 픽셀 복사)
                        for (int s = 0; s < step; s++)
                        {
                            if (x + s < screenWidth)
                            {
                                _buffer[bufferIndex + s] = color;
                                _zBuffer[x + s] = perpWallDist; // 깊이 버퍼
                            }
                        }
                    }
                }
                // ---------------------------------------------------------
                // 9. Z-Buffer 채우기 (스프라이트 깊이 판정용)
                // ---------------------------------------------------------
                // 벽의 거리 정보도 step만큼 동일하게 채워줍니다.
                for (int s = 0; s < step; s++)
                {
                    if (x + s < screenWidth)
                    {
                        _zBuffer[x + s] = perpWallDist;
                    }
                }
            }
        }

        // [헬퍼 함수] 현재 레이가 부딪힌 면(Side)의 텍스처 ID를 가져옴
        private int GetTextureIdOnSide(CellData cell, int side, int stepX, int stepY, bool isBackFace)
        {
            // 인덱스 규칙: 0:North, 1:East, 2:South, 3:West
            
            // isBackFace == false (진입하는 면 검사)
            // side 0 (세로 이동): stepX > 0 (남행) -> North면(0) 충돌 / stepX < 0 (북행) -> South면(2) 충돌
            // side 1 (가로 이동): stepY > 0 (동행) -> West면(3) 충돌 / stepY < 0 (서행) -> East면(1) 충돌
            
            // isBackFace == true (나가는 면 검사 - 되돌아보기)
            // side 0 (세로 이동): stepX > 0 (남행) -> South면(2) 통과 / stepX < 0 (북행) -> North면(0) 통과
            // side 1 (가로 이동): stepY > 0 (동행) -> East면(1) 통과 / stepY < 0 (서행) -> West면(3) 통과

            if (!isBackFace)
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[0] : cell.wallTextureIDs[2];
                else           return (stepY > 0) ? cell.wallTextureIDs[3] : cell.wallTextureIDs[1];
            }
            else
            {
                if (side == 0) return (stepX > 0) ? cell.wallTextureIDs[2] : cell.wallTextureIDs[0];
                else           return (stepY > 0) ? cell.wallTextureIDs[1] : cell.wallTextureIDs[3];
            }
        }

        private void CastFloorAndCeiling(int step)
        {
            // Horizon 계산 (점프, 피치 반영)
            float horizon = screenHeight / 2 - _currentJumpOffset + _currentPitch;
            
            // Unity 텍스처 좌표계는 Y=0이 맨 아래, Y=Height가 맨 위.
            // 따라서 0 ~ horizon은 바닥, horizon ~ screenHeight는 천장.

            // 벽 렌더링(CastWalls)에서 사용한 것과 동일한 비율 계수(heightScale).
            float heightScale = 0.66f / fovScale;

            // --------------------------------------------------------
            // 1. 바닥 그리기 (y: 0 ~ horizon)
            // --------------------------------------------------------
            for (int y = 0; y < (int)horizon; ++y) 
            {
                if (y >= screenHeight) break; // 안전장치

                // 바닥에서 horizon까지의 거리 (y가 커질수록 horizon에 가까워짐 -> p는 작아짐)
                float p = horizon - y; 
                if (p <= 0.1f) p = 0.1f; // 0 나누기 방지

                // 천장 posZ에도 heightScale 적용
                float posZ = 0.5f * screenHeight * heightScale;
                float rowDistance = posZ / p;

                if (!useGridLighting) 
                {
                    precalcLightScale = (int)(Mathf.Clamp(lightingIntensity / rowDistance, 0f, 1f) * 255);
                }

                // 레이 방향 계산
                float rayDirX0 = _dirX - _planeX;
                float rayDirY0 = _dirY - _planeY;
                float rayDirX1 = _dirX + _planeX;
                float rayDirY1 = _dirY + _planeY;

                // x 루프에서 step 사용
                // 바닥 텍스처 매핑은 x가 건너뛰어지면 텍스처 좌표(floorX)도 그만큼 더 많이 이동해야 함을 주의!
                // 하지만 여기서는 픽셀 단위 렌더링이므로, 단순히 계산 횟수만 줄이고 옆칸을 복사하는 방식이 안전함.
                float floorStepX = rowDistance * (rayDirX1 - rayDirX0) / screenWidth;
                float floorStepY = rowDistance * (rayDirY1 - rayDirY0) / screenWidth;

                // step만큼 미리 점프하기 위해 보정
                floorStepX *= step;
                floorStepY *= step;

                // 초기 시작점은 0번째 스텝이므로 floorStep을 더할 필요가 없음
                float floorX = _posX + rowDistance * rayDirX0; 
                float floorY = _posY + rowDistance * rayDirY0;

                for (int x = 0; x < screenWidth; x += step) 
                {
                    Color32 color = GetFloorColor(floorX, floorY, rowDistance);
                    
                    // 가로로 채우기
                    for (int s = 0; s < step; s++)
                    {
                        if (x + s < screenWidth) _buffer[y * screenWidth + (x + s)] = color;
                    }

                    floorX += floorStepX;
                    floorY += floorStepY;
                }
            }

            // --------------------------------------------------------
            // 2. 천장 그리기 (y: horizon ~ screenHeight)
            // --------------------------------------------------------
            bool hasCeil = _ceilTexIdx != -1;
            if (hasCeil || _isScanning) // 천장이 있거나 스캔 중일 때
            {
                for (int y = (int)horizon; y < screenHeight; ++y) 
                {
                    if (y < 0) continue;

                    // horizon에서 천장 픽셀까지의 거리 (y가 커질수록 멀어짐 -> p 커짐)
                    float p = y - horizon;
                    if (p <= 0.1f) p = 0.1f;

                    // posZ 계산에 heightScale 곱하기
                    float posZ = 0.5f * screenHeight * heightScale;
                    float rowDistance = posZ / p;

                    if (!useGridLighting) 
                    {
                        precalcLightScale = (int)(Mathf.Clamp(lightingIntensity / rowDistance, 0f, 1f) * 255);
                    }
                    
                    // 천장은 바닥과 레이 계산 로직이 동일
                    float rayDirX0 = _dirX - _planeX;
                    float rayDirY0 = _dirY - _planeY;
                    float rayDirX1 = _dirX + _planeX;
                    float rayDirY1 = _dirY + _planeY;

                    float floorStepX = rowDistance * (rayDirX1 - rayDirX0) / screenWidth;
                    float floorStepY = rowDistance * (rayDirY1 - rayDirY0) / screenWidth;
                    
                    // 스텝만큼 이동하도록 보정
                    floorStepX *= step;
                    floorStepY *= step;

                    float floorX = _posX + rowDistance * rayDirX0 + (floorStepX * 0 / step); // 초기값 보정
                    float floorY = _posY + rowDistance * rayDirY0 + (floorStepY * 0 / step);

                    // 루프에서 step 사용
                    for (int x = 0; x < screenWidth; x += step) 
                    {
                        Color32 color = GetCeilingColor(floorX, floorY, rowDistance);

                        for (int s = 0; s < step; s++)
                        {
                            if (x + s < screenWidth) 
                                _buffer[y * screenWidth + (x + s)] = color;
                        }

                        floorX += floorStepX;
                        floorY += floorStepY;
                    }
                }
            }
        }

        // [헬퍼 함수] 바닥 색상 계산 (기존 루프 안의 내용을 복사해서 정리)
        private Color32 GetFloorColor(float worldX, float worldY, float rowDistance)
        {
            int cellX = (int)(worldX);
            int cellY = (int)(worldY);
            int tx = (int)(texWidth * (worldX - cellX)) & (texWidth - 1);
            int ty = (int)(texHeight * (worldY - cellY)) & (texHeight - 1);

            Color32 color;

            // 스캔 효과 및 텍스처
            if (_isScanning && rowDistance < _currentScanRadius)
            {
                float distToScanEdge = Mathf.Abs(rowDistance - _currentScanRadius);
                bool isPulse = distToScanEdge < pulseWidth;
                bool isGridEdge = (tx == 0 || tx == texWidth - 1 || ty == 0 || ty == texHeight - 1);

                if (isPulse) color = pulseColor;
                else if (isGridEdge) color = floorWireframeColor;
                else color = Color.black;
            }
            else
            {
                color = GetPixelFast(_floorTexIdx, tx, ty);
                // 감마 적용
                int lightScale = useGridLighting ? (int)(Mathf.Clamp(lightingIntensity / rowDistance, 0f, 1f) * 255) : precalcLightScale;
        
                if (lightScale < 255) {
                    color.r = (byte)((color.r * lightScale) >> 8);
                    color.g = (byte)((color.g * lightScale) >> 8);
                    color.b = (byte)((color.b * lightScale) >> 8);
                }
            }
            
            return color;
        }

        // [헬퍼 함수] 천장 색상 계산
        private Color32 GetCeilingColor(float worldX, float worldY, float rowDistance)
        {
            // 바닥 로직과 유사하지만 텍스처 ID와 와이어프레임 색상 등이 다를 수 있음
            // 여기서는 편의상 바닥 로직을 재사용하되 텍스처만 _ceilTexIdx 사용
            
            int cellX = (int)(worldX);
            int cellY = (int)(worldY);
            int tx = (int)(texWidth * (worldX - cellX)) & (texWidth - 1);
            int ty = (int)(texHeight * (worldY - cellY)) & (texHeight - 1);

            Color32 color;
            
            if (_isScanning && rowDistance < _currentScanRadius)
            {
                float distToScanEdge = Mathf.Abs(rowDistance - _currentScanRadius);
                bool isPulse = distToScanEdge < pulseWidth;
                bool isGridEdge = (tx == 0 || tx == texWidth - 1 || ty == 0 || ty == texHeight - 1);

                if (isPulse) color = pulseColor;
                else if (isGridEdge) color = floorWireframeColor;
                else color = Color.black;
            }
            else
            {
                color = GetPixelFast(_ceilTexIdx, tx, ty);
                int lightScale = useGridLighting ? (int)(Mathf.Clamp(lightingIntensity / rowDistance, 0f, 1f) * 255) : precalcLightScale;
    
                if (lightScale < 255) {
                    color.r = (byte)((color.r * lightScale) >> 8);
                    color.g = (byte)((color.g * lightScale) >> 8);
                    color.b = (byte)((color.b * lightScale) >> 8);
                }
            }

            return color;
        }

        private void CastSprites(int step)
        {
            // 스프라이트를 먼 것부터 가까운 것까지 정렬 
            for (int i = 0; i < _spriteNum; i++) {
                _spriteOrder[i] = i;

                //거리를 역순으로 저장  
                _spriteDistance[_spriteNum - 1 - i] = (_posX - _sprtData[i].x) * (_posX - _sprtData[i].x) + (_posY - _sprtData[i].y) * (_posY - _sprtData[i].y);
            }
            Array.Sort(_spriteDistance, _spriteOrder);

            // 정렬된 스프라이트를 그림 
            float invDet = 1.0f / (_planeX * _dirY - _dirX * _planeY); // 역행렬 계산식 

            for (int i = 0; i < _spriteNum; i++) {
                int spriteIdx = _spriteOrder[i];
                float spriteX = _sprtData[spriteIdx].x - _posX;
                float spriteY = _sprtData[spriteIdx].y - _posY;

                float transformX = invDet * (_dirY * spriteX - _dirX * spriteY);
                float transformY = invDet * (-_planeY * spriteX + _planeX * spriteY); // 화면 내 깊이
                if (transformY <= 0) continue; // 카메라 앞에 있는 경우만 처리

                // 스프라이트 스캔 조건
                bool isSpriteScanned = _isScanning && (transformY < _currentScanRadius);

                if (isSpriteScanned) continue; //스캔 중에는 벽만 보이게 함
                
                float gamma = 0f;
                if (useGridLighting)
                {
                    // 스프라이트의 위치를 정수 그리드 좌표로 변환
                    int spriteGridX = (int)_sprtData[spriteIdx].x;
                    int spriteGridY = (int)_sprtData[spriteIdx].y;

                    // 플레이어 논리 좌표와의 거리 계산 (최댓값 기준)
                    float distX = Mathf.Abs(spriteGridX - _logicX);
                    float distY = Mathf.Abs(spriteGridY - _logicY);
                    float dist = Mathf.Max(distX, distY);

                    gamma = Mathf.Clamp(lightingIntensity / (dist + 1.0f), 0f, 1f);
                }
                else
                {
                    gamma = Mathf.Clamp(lightingIntensity / transformY, 0f, 1f);
                }

                int spriteScreenX = (int)((screenWidth / 2.0f) * (1 + transformX / transformY));
                int vMoveScreen = (int)(0.0 / transformY);
                
                int spriteHeight = (int)(screenHeight / transformY); 
                if (spriteHeight <= 0) continue;

                // 1. 벽과 마찬가지로 점프 높이만큼 아래로(-방향) 내린다.
                // 2. transformY(깊이)로 나누지 않고, 화면 픽셀 단위로 그대로 뺀다.
                int vOffset = (int)(-_currentJumpOffset + _currentPitch); 
                int drawStartY = -spriteHeight / 2 + screenHeight / 2 + vOffset;
                if (drawStartY < 0) drawStartY = 0;
                int drawEndY = spriteHeight / 2 + screenHeight / 2 + vOffset;
                if (drawEndY >= screenHeight) drawEndY = screenHeight - 1;

                int spriteWidth = Mathf.Abs((int)(screenHeight / transformY));
                int drawStartX = Mathf.Max(-spriteWidth / 2 + spriteScreenX, 0);
                int drawEndX = Mathf.Min(spriteWidth / 2 + spriteScreenX, screenWidth);

                int lightScale = (int)(gamma * 255);
                // 화면의 스프라이트에 대해 수직 스트라이프 반복
                for (int stripe = drawStartX; stripe < drawEndX; stripe += step) {
                    int texX = (int)(256 * (stripe - (-spriteWidth / 2 + spriteScreenX)) * _textures[_sprtData[spriteIdx].texIdx].width / spriteWidth) / 256;
                    if (texX < 0) texX = 0;

                    // ZBuffer 검사
                    if (transformY < _zBuffer[stripe]) {
                        // 현재 스트라이프의 모든 픽셀에 대해 반복 
                        for (int y = drawStartY; y < drawEndY; y++) {
                            int d = (y - vMoveScreen - vOffset) * 256 - screenHeight * 128 + spriteHeight * 128; // float을 피하기 위해 256과 128 인자 사용 
                            int texY = d * _textures[_sprtData[spriteIdx].texIdx].height / spriteHeight / 256;
                            if (texY < 0) texY = 0;
                            
                            Color32 color = GetPixelFast(_sprtData[spriteIdx].texIdx, texX, texY);

                            // 투명한 픽셀은 그리지 않음 (알파값 0이면 스킵)
                            if (color.a > 0) 
                            {
                                // 조명 적용
                                if (lightScale < 255)
                                {
                                    color.r = (byte)((color.r * lightScale) >> 8);
                                    color.g = (byte)((color.g * lightScale) >> 8);
                                    color.b = (byte)((color.b * lightScale) >> 8);
                                }

                                // 버퍼에 쓰기
                                int bufferIdx = y * screenWidth + stripe;
                                _buffer[bufferIdx] = color; // 알파 블렌딩 없이 덮어쓰기 (성능 우선)
                                
                                // Step 처리 (각 픽셀별로 깊이 검사 수행)
                                for (int s = 0; s < step; s++)
                                {
                                    int currentX = stripe + s;
                                    if (currentX < drawEndX && currentX < screenWidth)
                                    {
                                        // 픽셀별로 깊이(ZBuffer)를 확인
                                        if (transformY < _zBuffer[currentX]) 
                                        {
                                            _buffer[bufferIdx + s] = color;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateScreen()
        {
            //최초 1회 페이드인
            fadeOverlay.alpha = 1;
            fadeOverlay.DOFade(0f, 1f);

            Vector2 pos = Vector2.zero;
            backgroundImage.transform.localPosition = pos;
            
            _screenTexture = new Texture2D(screenWidth, screenHeight, TextureFormat.RGBA32, false);
            _screenTexture.filterMode = FilterMode.Point; // 도트 느낌 살리기

            Material mat;
            if (screenMaterial != null)
            {
                mat = new Material(screenMaterial); // 원본 보존을 위해 복제 인스턴스 사용
            }
            else
            {
                // 할당 안 했을 경우 비상용 기본값
                mat = new Material(Shader.Find("UI/Default")); 
            }
            mat.mainTexture = _screenTexture;

            var screen = new GameObject("Screen");
            screen.transform.SetParent(transform);
            _rawImg = screen.AddComponent<RawImage>();
            _rawImg.rectTransform.sizeDelta = new Vector2(screenWidth, screenHeight);
            _rawImg.transform.localPosition = pos;
            _rawImg.transform.localScale = screenScale;
            _rawImg.material = mat;
        }

        // =========================================================
        // 와이어프레임 스캔 효과 
        // =========================================================
        public void AttemptScan()
        {
            if (!_isScanning) StartCoroutine(ScanDungeonRoutine());
        }

        private IEnumerator ScanDungeonRoutine()
        {
            _isScanning = true;
            
            _currentScanRadius = 0f;

            // ---------------------------------------------------------
            // Phase 1: 확장 (Expand) - 플레이어 -> 시야 끝
            // ---------------------------------------------------------
            while (_currentScanRadius < maxScanDistance)
            {
                _currentScanRadius += Time.deltaTime * scanSpeed;
                
                // 정지 상태에서도 렌더링을 강제하여 애니메이션을 보여줌
                Render(); 
                yield return null;
            }
            
            // 오차 보정: 반지름을 최대치로 고정
            _currentScanRadius = maxScanDistance;
            if (isGridMove) Render();

            // ---------------------------------------------------------
            // Phase 2: 대기 (Wait) - 잠시 멈춤
            // ---------------------------------------------------------
            yield return new WaitForSeconds(scanWaitTime);

            // ---------------------------------------------------------
            // Phase 3: 수축 (Contract) - 시야 끝 -> 플레이어
            // ---------------------------------------------------------
            while (_currentScanRadius > 0f)
            {
                // 반경을 줄여나감 (돌아오는 속도를 다르게 하고 싶다면 returnSpeedMultiplier 사용)
                _currentScanRadius -= Time.deltaTime * scanSpeed * returnSpeedMultiplier;
                
                if (isGridMove) Render();
                yield return null;
            }

            // ---------------------------------------------------------
            // 종료 (Finish)
            // ---------------------------------------------------------
            _isScanning = false;
            _currentScanRadius = 0f;
            if (isGridMove) Render();
        }


        // 벽 충돌 애니메이션 코루틴
        private IEnumerator BumpCoroutine(Vector2Int dirVec)
        {
            _isMoving = true; // 입력 잠금

            float elapsed = 0f;
            float startX = _posX;
            float startY = _posY;

            while (elapsed < bumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bumpDuration;

                // 0 -> 1 -> 0 으로 변하는 사인 곡선
                // t * PI => 0 ~ 3.14
                float sineValue = Mathf.Sin(t * Mathf.PI);
                
                // 이동 방향(dirVec)으로 잠시 밀려남
                float offsetX = dirVec.x * bumpIntensity * sineValue;
                float offsetY = dirVec.y * bumpIntensity * sineValue;

                _posX = startX + offsetX;
                _posY = startY + offsetY;

                Render(); // 화면 갱신
                yield return null;
            }

            // 위치 원상 복구 및 오차 제거
            _posX = startX;
            _posY = startY;
            
            Render();
            _isMoving = false; // 입력 잠금 해제
        }

        // =========================================================
        // 자유 이동과 그리드 단위 이동의 전환 
        // =========================================================
        // 모드 토글 (외부 버튼이나 키 입력으로 호출)
        public void ToggleMovementMode()
        {
            if (isGridMove) SwitchToFreeMove();
            else SwitchToGridMove();
        }

        private void SwitchToFreeMove()
        {
            // 1. 실행 중인 그리드 이동/회전 코루틴 강제 종료
            if (_isMoving) StopCoroutine(_moveCoroutine);

            // 2. 입력 잠금 해제
            _isMoving = false;
            
            // 3. 모드 변경
            isGridMove = false;
            Debug.Log("Switched to Free Move");
        }

        private void SwitchToGridMove()
        {
            // 1. 현재 자유 이동 상태의 위치와 각도를 그리드에 맞게 '반올림(Snap)' 해야 함
            SnapToNearestGrid();

            // 2. 모드 변경
            isGridMove = true;
            Debug.Log("Switched to Grid Move");
        }

        /*
        * 자유 이동 상태에서 그리드 모드로 돌아올 때 위치와 각도를 보정하는 함수
        */
        private void SnapToNearestGrid()
        {
            // 1. 가장 가까운 그리드 좌표 계산
            int nearestGridX = Mathf.RoundToInt(_posX);
            int nearestGridY = Mathf.RoundToInt(_posY);

            // 맵 범위 클램핑
            if (_worldMap != null)
            {
                nearestGridX = Mathf.Clamp(nearestGridX, 0, _worldMap.width - 1);
                nearestGridY = Mathf.Clamp(nearestGridY, 0, _worldMap.height - 1);
            }

            // 2. 가장 가까운 4방향 찾기
            int bestDir = 0;
            float maxDot = -2.0f;

            for (int i = 0; i < 4; i++)
            {
                var vectors = GetVectorsForDirection(i);
                float dot = _dirX * vectors.dir.x + _dirY * vectors.dir.y;
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestDir = i;
                }
            }
            
            // 3. 데이터 확정 (방향 및 위치)
            _direction = bestDir;
            UpdateDirectionVectors(); 

            Vector2 finalPos = GetOffsetPosition(nearestGridX, nearestGridY, _direction);
            _posX = finalPos.x;
            _posY = finalPos.y;

            // 스냅 시 조명 기준점도 같이 갱신
            _logicX = nearestGridX;
            _logicY = nearestGridY;

            // 4. 시각적 요소 동기화
            
            // A. 미니맵 (GridMap) 즉시 스냅
            if (miniMap != null)
            {
                miniMap.SnapToGrid(nearestGridX, nearestGridY, _direction);
            }

            // B. 오토맵 발견 처리 & 아이콘 스냅
            UpdateMapDiscovery(nearestGridX, nearestGridY);

            // 나침반 즉시 동기화
            if (compassUI != null) compassUI.SetDirection(_direction);

            // 5. 화면 렌더링
            Render();
        }

        // 맵 발견 상태 업데이트 함수
        private void UpdateMapDiscovery(int x, int y)
        {
            var state = LevelManager.Instance.CurrentMapState;

            // 1. Fog of War 밝히기 (데이터상 처음 방문일 때)
            if (!state.IsVisited(x, y))
            {
                state.MarkVisited(x, y);
                autoMapRenderer.RevealCell(x, y); // 맵 텍스처(바닥/벽)를 그림
            }
            // 2. 플레이어 아이콘 이동 (방문 여부와 상관없이 매번 호출)
            // 텍스처를 건드리는 게 아니라 위의 아이콘만 슥 움직임
            autoMapRenderer.UpdatePlayerIcon(x, y, (Direction)_direction);
            MapManager.Instance.UpdatePlayerPosition(x, y, (Direction)_direction, _worldMap.mapID);

        }

        // =========================================================
        // Free Move Input & Movement Logic (Key Logic)
        // =========================================================
        private void HandleFreeMoveInput()
        {
            float moveSpeed = Time.deltaTime * 3.0f;
            float rotSpeed = Time.deltaTime * 2.0f;

            if (Input.GetKeyDown(KeyCode.Space)) AttemptJump();
            if (Input.GetKeyDown(KeyCode.R)) AttemptScan();
            if (Input.GetKey(KeyCode.W)) MoveForward(moveSpeed);
            if (Input.GetKey(KeyCode.S)) MoveBackward(moveSpeed);
            bool isRotating = false;

            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) 
            {
                TurnLeft(rotSpeed); // 이 함수 내부에서 _dirX, _dirY가 바뀜
                isRotating = true;
            }
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) 
            {
                TurnRight(rotSpeed);
                isRotating = true;
            }

            // 회전 중이거나 이동 중일 때 미니맵 화살표 실시간 갱신
            if (isRotating)
            {
                // 미니맵 위치 갱신 (부드러운 이동 대신 즉시 이동)
                // Free Move일 때는 TranslateToNewPosition(DOTween) 대신 직접 좌표를 꽂는게 좋지만,
                // 일단 화살표 회전부터 적용:
                miniMap.SetFreeDirection(_dirX, _dirY);
            }
            else
            {
                miniMap.TranslateToNewPosition(Mathf.FloorToInt(_posX), Mathf.FloorToInt(_posY));
            }
            autoMapRenderer.UpdatePlayerIconFree(_posX, _posY, _dirX, _dirY);
        }

        /*
         * 전진 (Forward)
         */
        public void MoveForward(float moveSpeed)
        {
            // 1. X축 이동 시도
            float deltaX = _dirX * moveSpeed;
            float nextPosX = _posX + deltaX;

            // Y축은 그대로 두고 X축만 변했을 때 벽에 박는지 확인
            // (int)_posY를 넘기는 이유는 '현재 Y줄'에서 X칸만 옆으로 갈 때를 체크하기 위함
            if (IsWalkable(nextPosX, _posY, deltaX, 0))
            {
                _posX = nextPosX;
            }

            // 2. Y축 이동 시도 (슬라이딩 구현)
            float deltaY = _dirY * moveSpeed;
            float nextPosY = _posY + deltaY;

            // X축은 이미 이동했거나 막혔으므로 현재 _posX 기준, Y축만 변했을 때 확인
            if (IsWalkable(_posX, nextPosY, 0, deltaY))
            {
                _posY = nextPosY;
            }
            
            UpdateMapDiscovery(Mathf.FloorToInt(_posX), Mathf.FloorToInt(_posY));
        }

        /*
         * 후진 (Backward)
         */
        public void MoveBackward(float moveSpeed)
        {
            // 전진과 벡터 방향만 반대 (-)
            float deltaX = -_dirX * moveSpeed;
            float nextPosX = _posX + deltaX;

            if (IsWalkable(nextPosX, _posY, deltaX, 0))
            {
                _posX = nextPosX;
            }

            float deltaY = -_dirY * moveSpeed;
            float nextPosY = _posY + deltaY;

            if (IsWalkable(_posX, nextPosY, 0, deltaY))
            {
                _posY = nextPosY;
            }
            
            UpdateMapDiscovery(Mathf.FloorToInt(_posX), Mathf.FloorToInt(_posY));
        }

        /*
         * 이동하려는 좌표가 통과 가능한지 확인하는 함수
         * delta: 이동 변화량 (이 값을 통해 진입 방향을 유추)
         */
        private bool IsWalkable(float targetX, float targetY, float deltaX, float deltaY)
        {
            // (int) 대신 Mathf.FloorToInt 사용
            // 예: -0.5 -> -1 (정상적인 맵 밖 인덱스)
            int gridX = Mathf.FloorToInt(targetX);
            int gridY = Mathf.FloorToInt(targetY);

            // 1. 맵 범위 체크
            if (gridX < 0 || gridX >= _worldMap.width || gridY < 0 || gridY >= _worldMap.height) 
                return false;

            CellData targetCell = _worldMap.GetCell(gridX, gridY);
            if (targetCell == null) return false;

            // 2. 빈 공간(CORRIDOR)이거나 벽이 아니라면 통과
            if (!targetCell.HasWall()) return true;

            // 3. 벽이라면, "진입하려는 면"의 텍스처를 확인해야 함
            // 현재 플레이어가 있는 셀(current)과 목표 셀(target)이 다를 때만 면 검사가 의미 있음
            // 하지만 Free Move에서는 같은 셀 내부에서도 벽 판정을 할 수 없으므로,
            // '다른 그리드로 넘어가는 순간' 혹은 '벽인 그리드 내부에 있으려 할 때'를 막아야 함.

            int checkFaceIndex = -1;
            if (Mathf.Abs(deltaX) > 0.0001f)
            {
                if (deltaX > 0) checkFaceIndex = 3; 
                else            checkFaceIndex = 1;
            }
            else if (Mathf.Abs(deltaY) > 0.0001f)
            {
                if (deltaY > 0) checkFaceIndex = 2;
                else            checkFaceIndex = 0;
            }

            if (checkFaceIndex != -1)
            {
                int texID = targetCell.wallTextureIDs[checkFaceIndex];
                if (texID != -1)
                {
                    if (illusionTextureIds.Contains(texID)) return true;
                    return false;
                }
            }
            return true;
        }

        /*
         * 오른쪽 회전
         */
        public void TurnRight(float rotSpeed)
        {
            double oldDirX = _dirX;
            _dirX = (float)(oldDirX * Mathf.Cos(-rotSpeed) - _dirY * Mathf.Sin(-rotSpeed));
            _dirY = (float)(oldDirX * Mathf.Sin(-rotSpeed) + _dirY * Mathf.Cos(-rotSpeed));
            double oldPlaneX = _planeX;
            _planeX = (float)(oldPlaneX * Mathf.Cos(-rotSpeed) - _planeY * Mathf.Sin(-rotSpeed));
            _planeY = (float)(oldPlaneX * Mathf.Sin(-rotSpeed) + _planeY * Mathf.Cos(-rotSpeed));
        }

        /*
         * 왼쪽 회전
         */
        public void TurnLeft(float rotSpeed)
        {
            double oldDirX = _dirX;
            _dirX = (float)(oldDirX * Mathf.Cos(rotSpeed) - _dirY * Mathf.Sin(rotSpeed));
            _dirY = (float)(oldDirX * Mathf.Sin(rotSpeed) + _dirY * Mathf.Cos(rotSpeed));
            double oldPlaneX = _planeX;
            _planeX = (float)(oldPlaneX * Mathf.Cos(rotSpeed) - _planeY * Mathf.Sin(rotSpeed));
            _planeY = (float)(oldPlaneX * Mathf.Sin(rotSpeed) + _planeY * Mathf.Cos(rotSpeed));
        }

        // =========================================================
        // Grid Move Input & Movement Logic (Key Logic)
        // =========================================================
        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) AttemptJump();
            if (Input.GetKeyDown(KeyCode.R)) AttemptScan();

            if (IsViewSkewed) return;

            // --- 1. 회전 (Q/E) ---
            // 회전은 이동 중에 입력되면 씹히는 것이 자연스러우므로 _isMoving 체크 유지
            if (!_isMoving)
            {
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) AttemptTurnLeft();
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) AttemptTurnRight();
            }

            // --- 2. 전진 (W) : 더블 탭 홀드 시 달리기 ---
            // 이동 중이라도 W를 다시 누르면 달리기 상태(_isRunning)를 갱신해야 함
            // 그래야 첫 걸음(걷기) 후에 멈추지 않고 바로 달릴 수 있음
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || 
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
            {
                if (Time.time - _lastWPressTime <= doubleTapThreshold)
                {
                    _isRunning = true;
                }
                else
                {
                    _isRunning = false;
                }
                _lastWPressTime = Time.time;
            }
            
            if (Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.S) || 
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
            {
                _isRunning = false;
            }

            if (!_isMoving) 
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) AttemptMoveForward();
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) AttemptMoveBackward(); 
            }


            // --- 4. 좌우 이동 (A/D) ---
            // 좌우 이동도 멈춰있을 때만 가능
            if (!_isMoving)
            {
                if (Input.GetKey(KeyCode.A)) AttemptMoveLeft();
                if (Input.GetKey(KeyCode.D)) AttemptMoveRight();
            }
        }

        /*
        * 올려보기
        */
        private bool HandleLookInput()
        {
            if (isInputLocked) return false;

            float oldPitch = _currentPitch;

            // 위를 보기 (Look Up) -> Horizon이 내려가야 함 -> Pitch 감소
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
            {
                _currentPitch -= lookSpeed * Time.deltaTime;
            }
            // 아래를 보기 (Look Down) -> Horizon이 올라가야 함 -> Pitch 증가
            else if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.S))
            {
                _currentPitch += lookSpeed * Time.deltaTime;
            }
            else if (autoCenterLook)
            {
                _currentPitch = Mathf.Lerp(_currentPitch, 0f, Time.deltaTime * 5.0f);
                if (Mathf.Abs(_currentPitch) < 1.0f) _currentPitch = 0f;
            }

            // 시점 각도 제한
            _currentPitch = Mathf.Clamp(_currentPitch, -maxPitch, maxPitch);

            // 예시: 상호작용 로직
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (_currentPitch < -maxPitch * 0.8f)
                {
                    Debug.Log("사다리를 타고 위층으로 올라갑니다.");
                    // LevelManager.Instance.GoToNextFloor();
                }
                else if (_currentPitch > maxPitch * 0.8f) 
                {
                    Debug.Log("구멍으로 뛰어내립니다.");
                    // LevelManager.Instance.GoToPrevFloor();
                }
            }

            return Mathf.Abs(_currentPitch - oldPitch) > 0.001f;
        }

        /*
        * 점프 코루틴: Horizon을 아래로 밀어내어 카메라가 올라간 효과를 냄
        */
        private IEnumerator JumpCoroutine()
        {
            _isJumping = true;
            float elapsed = 0f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;

                // Sin(0 ~ PI)는 0 -> 1 -> 0으로 변하는 곡선.
                // 여기에 jumpHeight를 곱해 부드러운 포물선을 그린다.
                _currentJumpOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;

                if (_isMoving == false) Render(); // 정지 상태에서도 점프하면 화면 갱신 필요
                yield return null;
            }

            _currentJumpOffset = 0f;
            _isJumping = false;
            
            if (_isMoving == false) Render(); // 착지 후 화면 갱신
        }

        // 외부 입력용 함수
        public void AttemptJump()
        {
            if (!_isJumping) StartCoroutine(JumpCoroutine());
        }

        // 2. UI 버튼과 키보드가 공통으로 호출할 '시도(Attempt)' 메서드 작성
        // 모바일 UI 버튼의 OnClick 이벤트에 이 함수들을 연결.
        public void AttemptMoveForward() { if (!_isMoving) MoveFront(); }
        public void AttemptMoveBackward() { if (!_isMoving) MoveBack(); }
        public void AttemptMoveLeft() { if (!_isMoving) MoveLeft(); }
        public void AttemptMoveRight() { if (!_isMoving) MoveRight(); }
        public void AttemptTurnLeft()  
        {  
            if (_isMoving) return;
            StartCoroutine(OrbitTurn(-1)); // -1: 왼쪽
        }
        public void AttemptTurnRight() 
        { 
            if (_isMoving) return;
            StartCoroutine(OrbitTurn(1)); // 1: 오른쪽
        }
        public void AttemptTurnAround()
        {
            if (_isMoving) return;
            StartCoroutine(OrbitTurn(2)); 
        }

        // 외부 UI 버튼에서 호출할 수 있는 Public 함수들
        public void MoveFront() { MovePlayer(GetMovementVector(_direction)); }
        public void MoveBack()  { MovePlayer(GetMovementVector(_direction) * -1); }
        public void MoveLeft()  { 
            var vec = GetMovementVector(_direction); 
            MovePlayer(new Vector2Int(-vec.y, vec.x)); // 90도 회전 벡터
        }
        public void MoveRight() { 
            var vec = GetMovementVector(_direction); 
            MovePlayer(new Vector2Int(vec.y, -vec.x)); 
        }

        /*
        * 핵심 이동 로직 (0:West, 1:North, 2:East, 3:South)
        */
        private void MovePlayer(Vector2Int moveDir)
        {
            // 현재 위치
            int currentX = Mathf.FloorToInt(_posX);
            int currentY = Mathf.FloorToInt(_posY);

            int targetX = currentX + moveDir.x;
            int targetY = currentY + moveDir.y;

            bool isPassable = true; // 통과 가능 여부
            int blockedByTexId = -1; // 이동을 막은 벽의 텍스처 ID 저장용

            // ---------------------------------------------------------
            // 1. [현재 칸]에서 나가는 방향의 벽(내벽) 검사
            // ---------------------------------------------------------
            if (currentX >= 0 && currentX < _worldMap.width && 
                currentY >= 0 && currentY < _worldMap.height)
            {
                CellData currentCell = _worldMap.GetCell(currentX, currentY);

                if (currentCell != null && currentCell.HasWall())
                {
                    int exitTexId = -1;

                    if (moveDir.x != 0) 
                    {
                        exitTexId = (moveDir.x > 0) ? currentCell.wallTextureIDs[2] : currentCell.wallTextureIDs[0];
                    }
                    else if (moveDir.y != 0)
                    {
                        exitTexId = (moveDir.y > 0) ? currentCell.wallTextureIDs[1] : currentCell.wallTextureIDs[3];
                    }

                    // 벽이 존재하고 비밀벽이 아니면 차단
                    if (exitTexId != -1 && !illusionTextureIds.Contains(exitTexId))
                    {
                        isPassable = false;
                        blockedByTexId = exitTexId; // 차단한 텍스처 기록
                    }
                }
            }

            // ---------------------------------------------------------
            // 2. [목표 칸]으로 진입하는 방향의 벽(외벽) 검사
            // ---------------------------------------------------------
            if (isPassable)
            {
                // 맵 경계 체크
                if (targetX < 0 || targetX >= _worldMap.width || 
                    targetY < 0 || targetY >= _worldMap.height)
                {
                    isPassable = false;
                    // 맵 밖으로 나가는 것은 텍스처 충돌이 아님 (필요하다면 별도 처리)
                }
                else
                {
                    CellData targetCell = _worldMap.GetCell(targetX, targetY);
                
                    if (targetCell != null && targetCell.HasWall())
                    {
                        int enterTexId = -1;

                        if (moveDir.x != 0) 
                        {
                                enterTexId = (moveDir.x > 0) ? targetCell.wallTextureIDs[0] : targetCell.wallTextureIDs[2];
                        }
                        else if (moveDir.y != 0)
                        {
                                enterTexId = (moveDir.y > 0) ? targetCell.wallTextureIDs[3] : targetCell.wallTextureIDs[1];
                        }

                        if (enterTexId != -1 && !illusionTextureIds.Contains(enterTexId))
                        {
                            isPassable = false;
                            blockedByTexId = enterTexId; // 차단한 텍스처 기록
                        }
                    }
                }
            }

            // ---------------------------------------------------------
            // 최종 이동 처리 및 충돌 이벤트 분기
            // ---------------------------------------------------------
            if (isPassable)
            {
                _moveCoroutine = StartCoroutine(MoveToPosition(targetX, targetY));
            }
            else
            {
                // 현재 위치(Current)와 목표 위치(Target) 양쪽에서 워프를 검색합니다.
        
                Direction inputDir = VectorToDirection(moveDir);
                WarpData validWarp = null;

                // 1. 현재 위치(Source) 검사: "이 방에서 나갈 때(안쪽 벽) 발동하는 워프인가?"
                WarpData currentWarp = _worldMap.GetWarpAt(currentX, currentY);
                if (currentWarp != null && currentWarp.isWallWarp && currentWarp.triggerDirection == inputDir)
                {
                    validWarp = currentWarp;
                    Debug.Log($"[Wall Warp] 현재 위치({currentX},{currentY})에서 워프 발견!");
                }

                // 2. 목표 위치(Target) 검사: "저 방으로 들어갈 때(바깥 벽) 발동하는 워프인가?"
                // (현재 위치에서 워프를 못 찾았을 경우에만 검사)
                if (validWarp == null)
                {
                    WarpData targetWarp = _worldMap.GetWarpAt(targetX, targetY);
                    if (targetWarp != null && targetWarp.isWallWarp && targetWarp.triggerDirection == inputDir)
                    {
                        validWarp = targetWarp;
                        Debug.Log($"[Wall Warp] 목표 위치({targetX},{targetY})에서 워프 발견!");
                    }
                }

                // 3. 워프 실행 또는 벽 충돌 처리
                if (validWarp != null)
                {
                    Debug.Log($"[Wall Warp] {validWarp.targetMapName}으로 이동합니다.");
                    
                    // 이동하려던 방향(moveDir)을 함께 전달하여 페이드 아웃되는 동안 그 방향으로 걸어가는 연출을 실행
                    StartCoroutine(TransitionToLevel(validWarp, moveDir));
                }
                else
                {
                    // 워프가 없으면 일반 벽 충돌
                    SoundManager.Instance.PlaySFX(SfxID.Bump_Wall);
                    if (!_isMoving) 
                    {
                        StartCoroutine(BumpCoroutine(moveDir));
                    }
                }
            }
        }

        // 레벨 데이터가 변경된 후(LoadLevelFromJson 호출 후) 실행할 재초기화 함수
        // entryWarp: 워프를 통해 들어왔다면 해당 데이터, 아니면 null
        public void ReloadMap(WarpData entryWarp = null)
        {
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);

            LoadMapData(entryWarp);
            // [안전장치] 첫 번째 텍스처의 크기로 텍스처 사이즈 설정을 덮어씌움
            if (_textures != null && _textures.Length > 0)
            {
                texWidth = _textures[0].width;
                texHeight = _textures[0].height;
            }
            
            _isMoving = false;
            _isScanning = false;
            _isJumping = false;
            _currentPitch = 0f;
            isInputLocked = true;

            // 1. 워프를 타고 왔다면 위치와 방향을 덮어씌움
            if (entryWarp != null)
            {
                _posX = entryWarp.targetX; // 0.5f 같은 오프셋이 필요하다면 GetOffsetPosition 활용
                _posY = entryWarp.targetY;
                _direction = (int)entryWarp.targetDirection;

                // 논리 좌표 및 벡터 갱신
                _logicX = entryWarp.targetX;
                _logicY = entryWarp.targetY;
                
                // 방향 벡터 재계산 및 오프셋 적용
                UpdateDirectionVectors();
                
                // 0.5f 중앙 정렬 및 BackwardOffset 적용
                Vector2 finalPos = GetOffsetPosition(_logicX, _logicY, _direction);
                _posX = finalPos.x;
                _posY = finalPos.y;
                
                Debug.Log($"Warp Spawned at ({_posX}, {_posY}) Dir: {_direction}");
            }
            else
            {
                // 2. 맵 데이터 로드 (기본 startX, startY로 세팅됨)
                LoadMapData();
            }
            
            // 나침반 초기화
            if (compassUI != null) compassUI.SetDirection(_direction);

            PrecomputeTexturePixels();
            Render();
        }

        // moveDir 인자 추가 (이동하려는 방향)
        private IEnumerator TransitionToLevel(WarpData warp, Vector2Int moveDir)
        {
            isInputLocked = true;

            // -----------------------------------------------------
            // Phase 1: Fade Out + Walk Animation (동시 실행)
            // -----------------------------------------------------
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                
                // 이동 시작 위치
                float startX = _posX;
                float startY = _posY;

                // 이동 목표 위치 계산 (벽 안쪽 좌표)
                // 현재 위치에서 moveDir만큼 더한 그리드의 '시각적 중심(OffsetPosition)'을 구함
                int targetGridX = Mathf.FloorToInt(_posX) + moveDir.x;
                int targetGridY = Mathf.FloorToInt(_posY) + moveDir.y;
                Vector2 targetPos = GetOffsetPosition(targetGridX, targetGridY, _direction);

                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = true;

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    
                    // 1. 화면 점점 어둡게
                    fadeOverlay.alpha = t;

                    // 2. 플레이어를 벽(워프) 쪽으로 이동시킴
                    _posX = Mathf.Lerp(startX, targetPos.x, t);
                    _posY = Mathf.Lerp(startY, targetPos.y, t);

                    // 이동했으니 화면 갱신
                    Render();

                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            yield return new WaitForSeconds(0.2f);

            // -----------------------------------------------------
            // Phase 2: Data Load
            // -----------------------------------------------------
            DungeonEventManager.Instance.SetCurrentMapID(warp.targetMapName);
            LevelManager.Instance.LoadLevelFromJson(warp.targetMapName);
            
            ReloadMap(warp); 

            yield return null; 

            // -----------------------------------------------------
            // Phase 3: Fade In
            // -----------------------------------------------------
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
            }
            isInputLocked = false;
        }

        // 좌표 보정 함수
        private (float x, float y) GetModifiedPosition(float x, float y, int dir)
        {
            return (x + 0.5f, y + 0.5f);
        }

        // 이동 벡터(Vector2Int)를 Direction Enum으로 변환하는 헬퍼 함수
        private Direction VectorToDirection(Vector2Int dirVec)
        {
            if (dirVec.y == 1) return Direction.North;
            if (dirVec.x == 1) return Direction.East;
            if (dirVec.y == -1) return Direction.South;
            if (dirVec.x == -1) return Direction.West;
            
            return Direction.North; // 기본값
        }

        private Vector2Int GetMovementVector(int direction)
        {
            return direction switch {
                // 좌표계 (X: East-West, Y: North-South)
                
                // North: 위쪽 (+Y)
                (int)Direction.North => new Vector2Int(0, 1), 
                
                // East: 오른쪽 (+X)
                (int)Direction.East => new Vector2Int(1, 0),
                
                // South: 아래쪽 (-Y)
                (int)Direction.South => new Vector2Int(0, -1),
                
                // West: 왼쪽 (-X)
                (int)Direction.West => new Vector2Int(-1, 0),
                
                _ => Vector2Int.zero,
            };
        }
        
        // =========================================================
        // Direction & Rotation Logic
        // =========================================================
        /*
        * 현재 _direction(0~3) 값에 맞춰 벡터를 강제로 재설정 (오차 보정용)
        * Start()나 회전이 완전히 끝난 직후에 호출.
        */
        private void UpdateDirectionVectors()
        {
            var targetVectors = GetVectorsForDirection(_direction);
            
            _dirX = targetVectors.dir.x;
            _dirY = targetVectors.dir.y;
            _planeX = targetVectors.plane.x;
            _planeY = targetVectors.plane.y;
        }

        private (Vector2 dir, Vector2 plane) GetVectorsForDirection(int dirIndex)
        {
            return dirIndex switch
            {
                // North
                (int)Direction.North => (new Vector2(0, 1), new Vector2(fovScale, 0)),

                // East
                (int)Direction.East  => (new Vector2(1, 0),  new Vector2(0, -fovScale)),

                // South
                (int)Direction.South => (new Vector2(0, -1),  new Vector2(-fovScale, 0)),

                // West
                (int)Direction.West  => (new Vector2(-1, 0), new Vector2(0, fovScale)),

                _ => (new Vector2(0, 1), new Vector2(fovScale, 0))
            };
        }

        // 위치와 방향을 동시에 회전시키는 코루틴
        private IEnumerator OrbitTurn(int directionStep)
        {
            _isMoving = true;

            int prevDirIdx = _direction;
            // 음수 나머지 연산 보정: (a % n + n) % n
            int nextDirIdx = ((_direction + directionStep) % 4 + 4) % 4;

            // 1. 회전할 총 각도 계산
            // directionStep: 1(우회전) -> -90도, -1(좌회전) -> +90도, 2(뒤로) -> 180도
            // (Raycast 좌표계상: North(0,1) -> East(1,0) 은 시계방향 회전이므로 수학적으로는 각도가 감소함)
            float targetAngleDeg = 0f;
            if (directionStep == 1) targetAngleDeg = -90f;       // Right
            else if (directionStep == -1) targetAngleDeg = 90f;  // Left
            else if (Mathf.Abs(directionStep) == 2) targetAngleDeg = 180f; // 180 Turn (부호는 취향, 보통 180)

            // 2. 시간(Duration) 설정: 180도 회전은 90도보다 2배 오래 걸려야 속도가 같음
            float baseDuration = CurrentTurnDuration;
            float duration = (Mathf.Abs(directionStep) == 2) ? baseDuration * 2.0f : baseDuration;

            // 나침반 애니메이션 실행
            // directionStep: 1(우회전), -1(좌회전), 2(뒤로)
            if (compassUI != null) 
            {
                compassUI.AnimateTurn(nextDirIdx, directionStep, duration);
            }

            float elapsed = 0f;

            // 3. 시작 상태 저장 (Start Snapshot)
            // 현재 정확한 벡터들을 가져옴
            (Vector2 startDir, Vector2 startPlane) = GetVectorsForDirection(prevDirIdx);

            // 회전 중심축 계산
            int gridX = Mathf.FloorToInt(_posX);
            int gridY = Mathf.FloorToInt(_posY);
            Vector2 centerPos = new Vector2(gridX + 0.5f, gridY + 0.5f);
            
            // 시작 위치의 오프셋 (중심점으로부터의 상대 좌표)
            Vector2 startPos = GetOffsetPosition(gridX, gridY, prevDirIdx);
            Vector2 startOffset = startPos - centerPos;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 4. 현재 프레임의 각도 계산 (Lerp)
                float currentAngleDeg = Mathf.Lerp(0f, targetAngleDeg, t);
                float rad = currentAngleDeg * Mathf.Deg2Rad; // 라디안 변환

                // 5. 회전 행렬(Rotation Matrix) 적용
                // 공식: x' = x cos - y sin, y' = x sin + y cos
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                // A. 방향 벡터 회전
                _dirX = startDir.x * cos - startDir.y * sin;
                _dirY = startDir.x * sin + startDir.y * cos;

                // B. 카메라 평면 벡터 회전
                _planeX = startPlane.x * cos - startPlane.y * sin;
                _planeY = startPlane.x * sin + startPlane.y * cos;

                // C. 위치 공전 (Orbit)
                // 오프셋 벡터를 회전시킨 뒤, 중심점에 더함
                float currOffsetX = startOffset.x * cos - startOffset.y * sin;
                float currOffsetY = startOffset.x * sin + startOffset.y * cos;

                _posX = centerPos.x + currOffsetX;
                _posY = centerPos.y + currOffsetY;

                Render();
                yield return null;
            }

            // 6. 최종 확정 (오차 제거를 위해 스냅핑)
            _direction = nextDirIdx;
            UpdateDirectionVectors();

            // 위치 보정
            Vector2 finalPos = GetOffsetPosition(gridX, gridY, _direction);
            _posX = finalPos.x;
            _posY = finalPos.y;

            Render();
            _isMoving = false;

            // 미니맵 화살표 회전
            if (miniMap != null) 
            {
                // 180도 회전이면 시간도 2배로 줘서 부드럽게
                float miniMapDuration = (Mathf.Abs(directionStep) == 2) ? 0.2f : 0.1f;
                miniMap.SetDirection(_direction, miniMapDuration);
            }

            UpdateMapDiscovery(gridX, gridY);
        }

        /*
        * 수정된 이동 코루틴: 항상 그리드의 중앙(x.5, y.5)으로 이동
        */
        private IEnumerator MoveToPosition(int targetGridX, int targetGridY)
        {
            _isMoving = true;
            
            float elapsed = 0f;
            float startX = _posX;
            float startY = _posY;
            // 시작할 때 현재 속도(걷기 vs 달리기)를 결정
            float targetDuration = CurrentMoveDuration;

            // 목표 지점은 '그리드 중앙'이 아니라 '방향에 맞춰 뒤로 물러난 위치'
            // 이동 중에는 방향(_direction)이 바뀌지 않으므로 현재 방향 유지
            Vector2 targetPos = GetOffsetPosition(targetGridX, targetGridY, _direction);
            miniMap.TranslateToNewPosition(targetGridX, targetGridY, targetDuration);
            
            float endX = targetPos.x;
            float endY = targetPos.y;

            while (elapsed < targetDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / targetDuration; 
                
                _posX = Mathf.Lerp(startX, endX, t);
                _posY = Mathf.Lerp(startY, endY, t);
                
                Render();
                yield return null;
            }

            _posX = endX;
            _posY = endY;

            _logicX = targetGridX;
            _logicY = targetGridY;
            
            UpdateMapDiscovery(targetGridX, targetGridY);
            SearchEvent(targetGridX, targetGridY);
            Render();
            
            _isMoving = false;

            // 전투 인카운터 체크
            OnStepTaken();
        }

        private void SearchEvent(int x, int y)
        {
            // 이벤트 체크 (DungeonEventManager가 유효한 ID만 리턴한다고 가정)
            string eventID = DungeonEventManager.Instance.CheckEvent(x, y);

            if (!string.IsNullOrEmpty(eventID))
            {
                // 플레이어 조작 잠금
                isInputLocked = true;

                // 기존에 연결된 것이 있을 수 있으므로 안전하게 제거 후 다시 연결
                dialogueUI.OnDialogueFinished -= OnEventFinished; 
                dialogueUI.OnDialogueFinished += OnEventFinished;

                // 대화 시작
                dialogueUI.StartDialogue(eventID);
            }
        }

        // 대화가 끝났을 때 호출될 콜백 메서드
        private void OnEventFinished()
        {
            // 1. 조작 잠금 해제
            isInputLocked = false;
            dialogueUI.OnDialogueFinished -= OnEventFinished;
        }

        // 전투가 끝나거나 처음 시작할 때 호출
        void ResetEncounterSteps()
        {
            stepsUntilNextBattle = UnityEngine.Random.Range(minSteps, maxSteps);
            _initialSteps = stepsUntilNextBattle;
            
            Debug.Log($"다음 전투까지 {stepsUntilNextBattle} 걸음 남음");
            
            // UI 및 애니메이션 초기화
            UpdateEncounterUI();
        }

        // 플레이어가 한 칸 이동할 때마다 호출 (Move 함수 내부)
        private void OnStepTaken()
        {
            // 저장을 위한 현재 위치를 업데이트
            MapManager.Instance.UpdatePlayerPosition(_logicX, _logicY, (Direction)_direction, _worldMap.mapID);

            stepsUntilNextBattle--;
            
            // 걸을 때마다 게이지와 깜빡임 속도 갱신
            UpdateEncounterUI();

            if (stepsUntilNextBattle <= 0)
            {
                TriggerEncounter();
            }
        }

        void UpdateEncounterUI()
        {
            if (dangerSlider == null || fillImage == null) return;

            // 현재 위험도 비율 계산 (0.0 ~ 1.0)
            float ratio = 1.0f - ((float)stepsUntilNextBattle / _initialSteps);
            ratio = Mathf.Clamp01(ratio);

            // 텍스트 갱신
            if (dangerText != null) dangerText.text = $"DANGER: {ratio * 100f:F0}%";

            // 슬라이더 색상 결정 (초록 -> 노랑 -> 빨강)
            Color baseColor = (ratio < 0.5f) 
                ? Color.Lerp(safeColor, warningColor, ratio * 2f) 
                : Color.Lerp(warningColor, dangerColor, (ratio - 0.5f) * 2f);
            
            fillImage.color = baseColor;

            _pulseTween?.Kill();

            dangerSlider.value = 0f;

            // 안전함(0%) -> 1초 (천천히 차오름)
            // 위험함(100%) -> 0.1초 (빠르게 펌프질)
            float duration = Mathf.Lerp(1f, 0.1f, ratio);

            if (ratio > 0.01f)
            {
                _pulseTween = dangerSlider.DOValue(ratio, duration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                dangerSlider.value = 0f;
            }
        }

        void TriggerEncounter()
        {
            _pulseTween?.Kill();
            
            GameStateManager.Instance.StartEncounter(currentTheme.monsterList);
            ResetEncounterSteps();
        }
    }
}