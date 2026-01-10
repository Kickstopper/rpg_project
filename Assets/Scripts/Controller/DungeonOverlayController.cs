using UnityEngine;
using UnityEngine.UI;
using UI.DungeonMapScene; // RaycastScreen namespace

namespace Controller
{
    public class DungeonOverlayController : MonoBehaviour
    {
        [Header("References")]
        public RaycastScreen raycastScreen;
        public GameObject autoMapPanel; // 오토맵 전체 패널 (RawImage + Render)

        [Header("UI Feedback")]
        public Image freeMoveToggleBtnIcon; // 자유 이동 버튼 아이콘 (색상 변경용)
        public Color gridModeColor = Color.white;
        public Color freeModeColor = Color.green;

        [Header("Movement Settings")]
        public float freeMoveSpeed = 3.0f;
        public float freeRotateSpeed = 2.0f;

        // 버튼 상태 플래그
        private bool _isForwardPressed;
        private bool _isBackwardPressed;
        private bool _isMoveLeftPressed;   // 게걸음(Move) or 회전(Turn)
        private bool _isMoveRightPressed;
        private bool _isTurnLeftPressed; // Q/E 회전용 (별도 버튼이 있다면)
        private bool _isTurnRightPressed;

        private void Start()
        {
            // 초기 UI 상태 동기화
            UpdateFreeMoveButtonVisual();
        }

        private void Update()
        {
            if (raycastScreen == null) return;

            // 자유 이동 모드일 때만 Update에서 연속 이동 처리
            // (RaycastScreen에 public bool IsGridMove { get => isGridMove; } 추가 필요)
            if (raycastScreen.isGridMove)
            {
                HandleGridRepeat(); // 그리드 모드용 반복 이동 처리
            }
            else
            {
                HandleFreeMove();   // 자유 모드용 이동 처리
            }
        }

        // =========================================================
        // [핵심] 그리드 모드 반복 이동 로직
        // =========================================================
        private void HandleGridRepeat()
        {
            // 1. 이미 이동(또는 회전) 중이라면 명령을 무시하고 대기
            // (RaycastScreen에 'public bool IsMoving => _isMoving;' 추가 필요)
            if (raycastScreen.IsMoving) return;

            // 2. 버튼 상태에 따라 이동 명령 내리기
            // (else if를 사용하여 대각선 이동 방지 - 우선순위: 전/후 -> 좌/우)
            
            if (_isForwardPressed)
            {
                raycastScreen.AttemptMoveForward();
            }
            else if (_isBackwardPressed)
            {
                raycastScreen.AttemptMoveBackward();
            }
            else if (_isMoveLeftPressed)
            {
                raycastScreen.AttemptMoveLeft(); 
            }
            else if (_isMoveRightPressed)
            {
                raycastScreen.AttemptMoveRight();
            }
        }

        // =========================================================
        // 이동 입력 처리 (ContinuousButton 이벤트와 연결)
        // =========================================================

        public void OnPressForward() 
        { 
            _isForwardPressed = true; 
        }
        public void OnReleaseForward() { _isForwardPressed = false; }

        public void OnPressBackward() 
        { 
            _isBackwardPressed = true;
            if (raycastScreen.isGridMove) raycastScreen.AttemptMoveBackward();
        }
        public void OnReleaseBackward() { _isBackwardPressed = false; }

        // 왼쪽 버튼 (A키 역할: 그리드에선 왼쪽 이동, 자유에선 회전 or 이동)
        // 여기서는 편의상 "좌회전" 버튼으로 가정합니다. (게걸음 버튼을 따로 만들 수도 있음)
        public void OnPressTurnLeft() 
        { 
            _isTurnLeftPressed = true;
            if (raycastScreen.isGridMove) raycastScreen.AttemptTurnLeft(); // 혹은 AttemptMoveLeft
        }
        public void OnReleaseTurnLeft() { _isTurnLeftPressed = false; }

        public void OnPressTurnRight() 
        { 
            _isTurnRightPressed = true;
            if (raycastScreen.isGridMove) raycastScreen.AttemptTurnRight();
        }
        public void OnReleaseTurnRight() { _isTurnRightPressed = false; }

        public void OnPressMoveLeft() 
        { 
            _isMoveLeftPressed = true;
            if (raycastScreen.isGridMove) raycastScreen.AttemptMoveLeft(); // 혹은 AttemptMoveLeft
        }
        public void OnReleaseMoveLeft() { _isMoveLeftPressed = false; }

        public void OnPressMoveRight() 
        { 
            _isMoveRightPressed = true;
            if (raycastScreen.isGridMove) raycastScreen.AttemptMoveRight();
        }
        public void OnReleaseMoveRight() { _isMoveRightPressed = false; }

        // =========================================================
        // 기능 토글 (일반 Button OnClick과 연결)
        // =========================================================

        public void ToggleAutoMap()
        {
            if (autoMapPanel != null)
            {
                bool isActive = !autoMapPanel.activeSelf;
                autoMapPanel.SetActive(isActive);
                
                // 오토맵을 켤 때 렌더러 갱신이 필요하다면 호출
                // if (isActive) autoMapPanel.GetComponent<AutoMapRenderer>()?.DrawFullMap(...);
            }
        }

        public void ToggleMovementMode()
        {
            if (raycastScreen != null)
            {
                raycastScreen.ToggleMovementMode();
                UpdateFreeMoveButtonVisual();
            }
        }

        public void ToggleMenu()
        {
            // 메뉴 열렸을 때 게임 일시정지 로직이 필요하다면 Time.timeScale 조절
            // Time.timeScale = isActive ? 0f : 1f;
        }

        // =========================================================
        // 내부 로직
        // =========================================================

        private void HandleFreeMove()
        {
            float dt = Time.deltaTime;

            if (_isForwardPressed) raycastScreen.MoveForward(freeMoveSpeed * dt);
            if (_isBackwardPressed) raycastScreen.MoveBackward(freeMoveSpeed * dt);
            
            // 좌우 버튼을 회전으로 쓸지, 게걸음으로 쓸지는 기획에 따라 선택
            // 1. 회전(Turn)으로 사용 시:
            if (_isTurnLeftPressed) raycastScreen.TurnLeft(freeRotateSpeed * dt);
            if (_isTurnRightPressed) raycastScreen.TurnRight(freeRotateSpeed * dt);


            if (_isMoveLeftPressed) raycastScreen.AttemptMoveLeft();
            if (_isMoveRightPressed) raycastScreen.AttemptMoveRight();


            // 2. 게걸음(Move)으로 사용 시 (주석 해제하여 사용):
            // if (_isMoveLeftPressed) raycastScreen.MoveLeft(); // RaycastScreen에 FreeMove용 MoveLeft(float speed) 구현 필요
        }

        private void UpdateFreeMoveButtonVisual()
        {
            if (freeMoveToggleBtnIcon != null && raycastScreen != null)
            {
                freeMoveToggleBtnIcon.color = raycastScreen.isGridMove ? gridModeColor : freeModeColor;
            }
        }
    }
}