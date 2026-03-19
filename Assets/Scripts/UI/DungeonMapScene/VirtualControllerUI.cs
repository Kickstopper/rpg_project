using UnityEngine;
using Controller;
using Manager;
using System;

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

        // 누르고 있는 상태를 추적하는 변수들
        private bool _isHoldingForward = false;
        private bool _isHoldingBackward = false;
        private bool _isHoldingLeft = false;
        private bool _isHoldingRight = false;

        void Start()
        {
            HideController();
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            }
        }

        void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
            }
        }
        
        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Exploration)
            {
                // 컨트롤러 상태 초기화
                HideController();
            }
        }

        void Update()
        {
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
            if (_isHoldingForward)  { ResetTimer(); raycastingController.UI_MoveForward(); }
            if (_isHoldingBackward) { ResetTimer(); raycastingController.UI_MoveBackward(); }
            if (_isHoldingLeft)     { ResetTimer(); raycastingController.UI_MoveLeft(); }
            if (_isHoldingRight)    { ResetTimer(); raycastingController.UI_MoveRight(); }

            // 패널이 표시된 상태에서 일정 시간이 지나면 자동 숨김
            if (controllerPanel.activeSelf)
            {
                if (!_isHoldingForward && !_isHoldingBackward && !_isHoldingLeft && !_isHoldingRight)
                {
                    if (Time.time - _lastInteractionTime > autoHideTime)
                    {
                        HideController();
                    }
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
            
            _isHoldingForward = false;
            _isHoldingBackward = false;
            _isHoldingLeft = false;
            _isHoldingRight = false;

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

            // 더블 탭 간격 안에 다시 누르면 달리기 시작
            if (Time.time - _lastMovePressTime < doubleTapThreshold)
            {
                raycastingController.UI_SetRunning(true);
            }
            _lastMovePressTime = Time.time;
        }

        private void HandleMovePointerUp()
        {
            // 누르고 있는 이동 버튼이 하나도 없다면 달리기 해제
            if (!_isHoldingForward && !_isHoldingBackward && !_isHoldingLeft && !_isHoldingRight)
            {
                raycastingController.isUIHoldingMovement = false; // UI 누름 상태 해제
                raycastingController.UI_SetRunning(false);
            }
        }

        // 버튼 프레스 및 업 이벤트
        public void PointerDownForward()  
        { 
            _isHoldingForward = true; 
            HandleMovePointerDown(); 
        }
        public void PointerUpForward()    
        { 
            _isHoldingForward = false; 
            HandleMovePointerUp(); 
        }

        public void PointerDownBackward() 
        { 
            _isHoldingBackward = true; 
            HandleMovePointerDown(); 
        }
        public void PointerUpBackward()   
        { 
            _isHoldingBackward = false; 
            HandleMovePointerUp(); 
        }

        public void PointerDownLeft()     
        { 
            _isHoldingLeft = true; 
            HandleMovePointerDown(); 
        }
        public void PointerUpLeft()       
        { 
            _isHoldingLeft = false; 
            HandleMovePointerUp(); 
        }

        public void PointerDownRight()    
        { 
            _isHoldingRight = true; 
            HandleMovePointerDown(); 
        }
        public void PointerUpRight()      
        { 
            _isHoldingRight = false; 
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