using System.Collections.Generic;
using DG.Tweening;
using Manager;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
namespace Controller
{
    public class PlayerMenuController : MonoBehaviour
    {
        private enum MenuState { Main, Status, Skill, Item, Equip, Move, System, Suspend}

        private MenuState currentState;
        public List<Button> allMenuBtns;
        private int currentBtnIndex;

        private bool isMenuOpen = false;

        public GameObject statusUI;

        [Header("Background")]
        public SimpleGradient background;

        private Tweener angleTween;

        [Header("Popup UI")]
        public GameObject confirmPopup; // 팝업 창 부모 오브젝트
        public TextMeshProUGUI popupMessage;  // 팝업 창에 표시될 메시지
        public Button popupYesBtn;            // '예' 버튼
        public Button popupNoBtn;             // '아니오' 버튼

        [Header("Input Settings")]
        [SerializeField] private float inputDelay = 0.15f; // 입력 간 지연 시간
        private float lastInputTime; // 마지막으로 입력이 처리된 시간

        // 입력을 처리할 수 있는 상태인지 확인하는 프로퍼티
        private bool CanProcessInput => Time.time >= lastInputTime + inputDelay;

        // 입력 성공 시 쿨타임을 갱신
        private void ResetInputTimer() => lastInputTime = Time.time;

        private bool isPopupOpen = false;     // 팝업이 열려있는지 확인

        void Start()
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            OnGameStateChanged(GameStateManager.Instance.CurrentState);
            if (confirmPopup != null) confirmPopup.SetActive(false);
        }

        void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.PlayerMenu)
            {
                // 배경 화면 애니메이션 시작
                StartTween();

                isMenuOpen = true;
                currentState = MenuState.Main;
                currentBtnIndex = 0;
                UpdateSelection(currentBtnIndex, false); // 최초에는 무음으로 첫 번째 버튼에 포커스
            }
            else
            {
                // 배경 화면 애니메이션 멈춤
                StopTween();

                isMenuOpen = false;
            }
        }

        void StartTween() 
        {
            angleTween = DOTween.To(() => background.angle, x => background.angle = x, 180f, 12f)
                .From(-180f)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }

        void StopTween() {
            if (angleTween != null && angleTween.IsActive()) {
                angleTween.Kill();
            }
        }

        void Update()
        {
            if (!isMenuOpen) return;
            if (!CanProcessInput) return; // 쿨타임 중이면 모든 입력 무시

            if (isPopupOpen) 
            {
                HandlePopupNavigation();
                return;
            }

            HandleMenuNavigation(ref currentBtnIndex);
        }

        // 팝업에서의 키보드 조작
        void HandlePopupNavigation()
        {
            // 방향키 조작
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) 
            {
                popupYesBtn.Select();
                SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cursor);
                ResetInputTimer(); // 조작 시에도 쿨타임 적용
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) 
            {
                popupNoBtn.Select();
                SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cursor);
                ResetInputTimer();
            }

            // 확인 키
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                GameObject currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;

                if (currentSelected == popupYesBtn.gameObject)
                {
                    ResetInputTimer();
                    OnClickConfirmButton();
                }
                else if (currentSelected == popupNoBtn.gameObject)
                {
                    ResetInputTimer();
                    OnClickCancelButton();
                }
            }

            // 취소 키
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cancel);
                ResetInputTimer();
                OnClickCancelButton();
            }
        }

        void HandleMenuNavigation(ref int currentBtnIndex)
        {
            if (currentState != MenuState.Main)
            {
                return;
            } 
            if (allMenuBtns == null || allMenuBtns.Count == 0) return;
            bool changed = false;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentBtnIndex = (currentBtnIndex - 1 + allMenuBtns.Count) % allMenuBtns.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentBtnIndex = (currentBtnIndex + 1) % allMenuBtns.Count;
                changed = true;
            }

            if (changed)
            {
                UpdateSelection(currentBtnIndex);
                ResetInputTimer();
                return; // 이동 시에는 여기서 종료
            }

            // 메뉴 확인 키
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (allMenuBtns[currentBtnIndex].interactable)
                {
                    ResetInputTimer(); // 버튼 실행 직전 타이머 리셋
                    allMenuBtns[currentBtnIndex].onClick.Invoke();
                }
            }
            
            // 메뉴 취소 키 (나가기)
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cancel);
                GameStateManager.Instance.ChangeState(GameState.Exploration);
            }
        }

        void UpdateSelection(int index, bool sound = true)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0 || index < 0 || index >= allMenuBtns.Count) return;
            allMenuBtns[index].Select();
            if (sound) SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cursor);
        }
        
        public void OnClick_Skill()
        {
            currentState = MenuState.Skill;
            UpdatePopupMessage();
            ResetInputTimer();
            Debug.Log("SKILL 미구현");
        }

        public void OnClick_Item()
        {
            currentState = MenuState.Item;
            UpdatePopupMessage();
            ResetInputTimer();
            Debug.Log("ITEM 미구현");
        }

        public void OnClick_Status()
        {
            currentState = MenuState.Status;
            statusUI.SetActive(true);
            UpdatePopupMessage();
            ResetInputTimer();
        }

        public void CloseStatusUI()
        {
            statusUI.SetActive(false);
            
            currentState = MenuState.Main;
            ResetInputTimer();
            UpdateSelection(currentBtnIndex); // 마지막으로 선택했던 메인 메뉴 버튼에 다시 포커스
            
            SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cancel);
        }
        
        public void OnClick_Equip()
        {
            currentState = MenuState.Equip;
            UpdatePopupMessage();
            ResetInputTimer();
            Debug.Log("EQUIP 미구현");
        }
        
        public void OnClick_Move()
        {
            currentState = MenuState.Move;
            UpdatePopupMessage();
            ResetInputTimer();
            Debug.Log("MOVE 미구현");
        }

        public void OnClick_System()
        {
            currentState = MenuState.System;
            UpdatePopupMessage();
            ResetInputTimer();
            Debug.Log("SYSTEM 미구현");
        }
        
        public void OnClick_Suspend()
        {
            if (confirmPopup != null)
            {
                currentState = MenuState.Suspend;
                UpdatePopupMessage();
                confirmPopup.SetActive(true);
                isPopupOpen = true;
                popupNoBtn.Select();
                ResetInputTimer();
                SoundManager.Instance.PlaySFX(Data.SfxID.UI_Click);
            }
        }

        private void UpdatePopupMessage()
        {
            switch(currentState)
            {
                case MenuState.Item:
                    popupMessage.SetText("선택한 아이템을 사용합니까?");
                break;
                case MenuState.Skill:
                    popupMessage.SetText("선택한 스킬을 발동합니까?");
                break;
                case MenuState.Equip:
                    popupMessage.SetText("선택한 아이템을 장비합니까?");
                break;
                case MenuState.System:
                    popupMessage.SetText("설정을 저장합니까?");
                break;
                case MenuState.Move:
                    popupMessage.SetText("선택한 자리로 이동합니까?");
                break;
                case MenuState.Suspend:
                    popupMessage.SetText("중단 저장을 하고 타이틀 화면으로 이동합니까?");
                break;

                case MenuState.Status:
                default:
                popupMessage.SetText(string.Empty);
                break;
            }
        }

        // YES 버튼
        public void OnClickConfirmButton()
        {
            ResetInputTimer(); // 입력 쿨타임 갱신

            switch (currentState)
            {
                case MenuState.Suspend:
                    // 중단 저장 및 타이틀 이동
                    SaveManager.Instance.SaveGame(SaveManager.SUSPEND_SLOT_INDEX);
                    UnityEngine.SceneManagement.SceneManager.LoadScene(GameScene.TITLE_SCENE);
                    break;

                case MenuState.Item:
                    Debug.Log("아이템 사용 로직 실행");
                    // 추후 구현: ItemManager.UseItem(...);
                    ClosePopup();
                    break;
                    
                case MenuState.Skill:
                    Debug.Log("스킬 사용 로직 실행");
                    ClosePopup();
                    break;

                default:
                    ClosePopup();
                    break;
            }
        }

        // 팝업 닫기
        private void ClosePopup()
        {
            currentState = MenuState.Main; // 상태 초기화
            confirmPopup.SetActive(false);
            isPopupOpen = false;
            allMenuBtns[currentBtnIndex].Select(); // 메뉴로 포커스 복귀
        }

        // NO 버튼
        public void OnClickCancelButton()
        {
            ResetInputTimer();
            ClosePopup();
            
            UpdatePopupMessage();
        }
    }
}

