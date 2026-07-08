using UnityEngine;
using Controller;
using Manager;

namespace UI.DungeonMapScene
{
    public class VirtualControllerUI : MonoBehaviour
    {
        [Header("References")]
        public GameObject controllerPanel;
        public RaycastingController raycastingController;

        [Header("Settings")]
        public float autoHideTime = 5.0f;
        public float doubleTapThreshold = 0.3f; // 더블 탭 판정 시간

        private float _lastInteractionTime;
        private float _lastMovePressTime = -100f; // 마지막으로 이동 버튼을 누른 시간

        private enum moveButtons { None, Forward, Backward, Left, Right }

        private moveButtons clickedBtn;
        private moveButtons prevBtn; // 이전에 누른 버튼

        void Start()
        {
            HideController();
            if (ManagerRoot.GameState != null)
            {
                ManagerRoot.GameState.OnStateChanged += OnGameStateChanged;
            }
        }

        void OnDestroy()
        {
            if (ManagerRoot.GameState != null)
            {
                ManagerRoot.GameState.OnStateChanged -= OnGameStateChanged;
            }
        }
        
        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Exploration)
            {
                HideController();
            }
        }

        void Update()
        {
            if (ManagerRoot.GameState.CurrentState != GameState.Exploration) return;
            
            // 마우스 클릭 시 패널 나타남
            if (Input.GetMouseButtonDown(0))
            {
                ShowController();
            }

            // 키보드 입력 시 패널 사라짐
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
            {
                HideController();
            }

            // 버튼을 누르고 있는 동안 매 프레임 이동 명령 전달
            if (clickedBtn == moveButtons.Forward)  { ResetTimer(); raycastingController.UI_MoveForward(); }
            if (clickedBtn == moveButtons.Backward) { ResetTimer(); raycastingController.UI_MoveBackward(); }
            if (clickedBtn == moveButtons.Left)     { ResetTimer(); raycastingController.UI_MoveLeft(); }
            if (clickedBtn == moveButtons.Right)    { ResetTimer(); raycastingController.UI_MoveRight(); }

            // 패널이 표시된 상태에서 아무 버튼도 안 누르고 있을 때 자동 숨김
            if (controllerPanel.activeSelf && clickedBtn == moveButtons.None)
            {
                if (Time.time - _lastInteractionTime > autoHideTime)
                {
                    HideController();
                }
            }
        }

        private void ShowController()
        {
            controllerPanel.SetActive(true);
            ResetTimer();
        }

        private void HideController()
        {
            if (!controllerPanel.activeSelf) return;

            controllerPanel.SetActive(false);
            
            clickedBtn = moveButtons.None;

            raycastingController.isUIHoldingMovement = false; 
            raycastingController.UI_SetRunning(false);
        }

        public void ResetTimer()
        {
            _lastInteractionTime = Time.time;
        }

        // 더블 탭 공통 로직
        private void HandleMovePointerDown()
        {
            raycastingController.isUIHoldingMovement = true; // UI 누름 상태 활성화
            
            if (prevBtn != clickedBtn)
            {
                prevBtn = clickedBtn;
            }
            // 더블 탭 간격 안에 다시 누르면 달리기 시작
            else if (Time.time - _lastMovePressTime < doubleTapThreshold)
            {
                raycastingController.UI_SetRunning(true);
            }
            
            _lastMovePressTime = Time.time;
        }

        private void HandleMovePointerUp()
        {
            if (clickedBtn == moveButtons.None)
            {
                raycastingController.isUIHoldingMovement = false; // UI 누름 상태 해제
                raycastingController.UI_SetRunning(false);
            }
        }

        // 버튼 프레스 및 업 이벤트
        public void PointerDownForward()  
        { 
            clickedBtn = moveButtons.Forward;
            HandleMovePointerDown(); 
        }
        public void PointerUpForward()    
        { 
            clickedBtn = moveButtons.None; 
            HandleMovePointerUp(); 
        }

        public void PointerDownBackward() 
        { 
            clickedBtn = moveButtons.Backward;
            HandleMovePointerDown(); 
        }
        public void PointerUpBackward()   
        { 
            clickedBtn = moveButtons.None;
            HandleMovePointerUp(); 
        }

        public void PointerDownLeft()     
        { 
            clickedBtn = moveButtons.Left;
            HandleMovePointerDown(); 
        }
        public void PointerUpLeft()       
        { 
            clickedBtn = moveButtons.None;
            HandleMovePointerUp(); 
        }

        public void PointerDownRight()    
        { 
            clickedBtn = moveButtons.Right;
            HandleMovePointerDown(); 
        }
        public void PointerUpRight()      
        { 
            clickedBtn = moveButtons.None;
            HandleMovePointerUp(); 
        }

        // 버튼 클릭 이벤트
        public void OnClickTurnLeft()
        {
            ResetTimer();
            raycastingController.UI_TurnLeft();
        }

        public void OnClickTurnRight()
        {
            ResetTimer();
            raycastingController.UI_TurnRight();
        }

        public void OnClickAction()
        {
            ResetTimer();
            raycastingController.UI_Action();
        }
    }
}