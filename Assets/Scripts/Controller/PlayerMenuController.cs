using System.Collections.Generic;
using Data;
using Manager;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Controller
{
    public class PlayerMenuController : MonoBehaviour
    {
        private enum MenuState { Main, Status, Skill, Item, Move, Equip, Module, 
                                Compile, System, Suspend, SelectEquipChar, SelectStatusChar }        
        private MenuState currentState;
        public List<Button> allMenuBtns;
        private int currentBtnIndex;

        private bool isMenuOpen = false;
        private bool isAlertMode = false;

        public GameObject statusUI;
        public GameObject moveUI;
        public GameObject skillUI;
        public GameObject itemUI;
        public GameObject equipUI;
        public GameObject moduleUI;
        public GameObject compileUI;
        public GameObject playerPrefab;
        
        [Header("Background")]
        public SimpleGradient background;

        [Header("Popup UI")]
        public GameObject confirmPopup; // 팝업 창 부모 오브젝트
        public TextMeshProUGUI popupMessage;  // 팝업 창에 표시될 메시지
        public Button popupYesBtn;            // '예' 버튼
        public Button popupNoBtn;             // '아니오' 버튼

        [Header("Input Settings")]
        [SerializeField] private float inputDelay = 0.15f; 
        private float lastInputTime; 
        private int lastInputFrame; // 마지막으로 입력이 처리된 프레임

        [Header("Party Visuals")]
        public Transform[] partySlots; // 6개의 슬롯 (0~5)
        private PlayerController[] spawnedControllers = new PlayerController[6];
        private int currentPartySelectIndex = 0;
        public Color charHighlightColor = Color.gold; // 선택 시 하이라이트 색상

        // 입력을 처리할 수 있는 상태인지 확인하는 프로퍼티. 시간 조건과 프레임 조건을 모두 만족해야 입력 가능
        public bool CanProcessInput => (Time.unscaledTime >= lastInputTime + inputDelay) && (Time.frameCount > lastInputFrame);
        public bool isInputLocked = false;
        
        public void ResetInputTimer() 
        {
            lastInputTime = Time.unscaledTime;
            lastInputFrame = Time.frameCount;
        }

        private bool isPopupOpen = false;     // 팝업이 열려있는지 확인
        public bool IsPopupOpen => isPopupOpen;
        void Start()
        {
            if (ManagerRoot.GameState)
            {
                ManagerRoot.GameState.OnStateChanged += OnGameStateChanged;
                OnGameStateChanged(ManagerRoot.GameState.CurrentState);
            }
            
            if (confirmPopup != null) confirmPopup.SetActive(false);
        }

        void OnDestroy()
        {
            if (ManagerRoot.GameState != null)
                ManagerRoot.GameState.OnStateChanged -= OnGameStateChanged;
        }

        void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.PlayerMenu)
            {
                isMenuOpen = true;
                currentState = MenuState.Main;
                currentBtnIndex = 0;
                
                // 메뉴 진입 시 파티 슬롯 갱신
                RefreshPartyUI();

                UpdateSelection(currentBtnIndex, false); 
                ResetInputTimer();
            }
            else
            {
                isMenuOpen = false;
            }
        }

        void Update()
        {
            if (!isMenuOpen) return;
    
            // 컷신(연출) 중일 때 메뉴의 모든 조작을 강제로 무시합니다. (이 줄을 추가!)
            if (isInputLocked) return; 
            
            // 서브 UI가 닫히면서 타이머가 리셋되었다면, 여기서 바로 차단됨
            if (!CanProcessInput) return; 

            if (isPopupOpen) 
            {
                HandlePopupNavigation();
                return;
            }

            // 서브 메뉴가 눈에 보이는 상태라면 무조건 리턴 (서브 메뉴 안에서의 입력은 각각의 UI Controller가 처리함)
            if (IsAnySubMenuOpen()) return;

            // 장비, 스테이터스 캐릭터 선택 상태
            if (currentState == MenuState.SelectEquipChar || currentState == MenuState.SelectStatusChar)
            {
                HandleCharacterSelection();
                return;
            }
            
            HandleMenuNavigation(ref currentBtnIndex);
        }

        // 팝업에서의 키보드 조작
        void HandlePopupNavigation()
        {
            // 알림 모드가 아닐 때만 방향키로 Yes/No 선택 가능
            if (!isAlertMode)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) 
                {
                    popupYesBtn.Select();
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    ResetInputTimer(); 
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) 
                {
                    popupNoBtn.Select();
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    ResetInputTimer();
                }
            }
            
            // 확인 키
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ResetInputTimer();
                // 알림 모드일 때는 확인 키를 누르면 무조건 닫기(No버튼 동작) 수행
                if (isAlertMode)
                {
                    OnClickCancelButton();
                }
                else
                {
                    // 일반 모드일 때는 선택된 버튼에 따라 동작
                    GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
                    if (currentSelected == popupYesBtn.gameObject)
                    {
                        OnClickConfirmButton();
                    }
                    else if (currentSelected == popupNoBtn.gameObject)
                    {
                        OnClickCancelButton();
                    }
                }
            }
            
            // 취소 키
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                ResetInputTimer();
                OnClickCancelButton();
            }
        }

        void HandleMenuNavigation(ref int currentBtnIndex)
        {
            if (currentState != MenuState.Main) return;
            if (allMenuBtns == null || allMenuBtns.Count == 0) return;

            bool changed = false;
            int columnCount = 5; // 5열 설정
            int totalCount = allMenuBtns.Count;

            // [상/하 이동] 행 단위 이동 (인덱스 +/- 5)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                // 위로 갈 공간이 있으면 이동
                if (currentBtnIndex - columnCount >= 0)
                {
                    currentBtnIndex -= columnCount;
                }
                else
                {
                    // 맨 위라면 같은 열의 맨 아래 유효한 버튼으로 이동 (순환)
                    int bottomIndex = currentBtnIndex + columnCount;
                    if (bottomIndex < totalCount) currentBtnIndex = bottomIndex;
                }
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                // 아래로 갈 공간이 있으면 이동
                if (currentBtnIndex + columnCount < totalCount)
                {
                    currentBtnIndex += columnCount;
                }
                else
                {
                    // 맨 아래라면 같은 열의 맨 위로 이동 (순환)
                    currentBtnIndex = currentBtnIndex % columnCount;
                }
                changed = true;
            }

            // [좌/우 이동] 아이템 단위 이동 (인덱스 +/- 1)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                currentBtnIndex = (currentBtnIndex - 1 + totalCount) % totalCount;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                currentBtnIndex = (currentBtnIndex + 1) % totalCount;
                changed = true;
            }

            if (changed)
            {
                UpdateSelection(currentBtnIndex);
                ResetInputTimer();
                return; 
            }

            // 확인 키
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (allMenuBtns[currentBtnIndex].interactable)
                {
                    ResetInputTimer(); 
                    allMenuBtns[currentBtnIndex].onClick.Invoke();
                }
            }

            // 취소 키
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                ResetInputTimer();
                ManagerRoot.GameState.ChangeState(GameState.Exploration);
            }
        }

        // 파티 슬롯 갱신 (MoveUI 로직 차용)
        private void RefreshPartyUI()
        {
            var party  = ManagerRoot.Party.partyData;
            RuntimeCharacterData[] slotAssignments = new RuntimeCharacterData[6];
            List<RuntimeCharacterData> pending = new List<RuntimeCharacterData>();

            // 위치 충돌 해결 및 배치 계산
            foreach (var member in party)
            {
                int idx = GetIndexFromRowColumn(member.row, member.column);
                if (slotAssignments[idx] == null) slotAssignments[idx] = member;
                else pending.Add(member);
            }

            foreach (var member in pending)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (slotAssignments[i] == null)
                    {
                        slotAssignments[i] = member;
                        break;
                    }
                }
            }

            // 프리팹 생성 및 초기화
            for (int i = 0; i < 6; i++)
            {
                if (partySlots.Length <= i) break;

                // 기존 오브젝트 제거
                foreach (Transform child in partySlots[i]) Destroy(child.gameObject);
                spawnedControllers[i] = null;

                GameObject go = Instantiate(playerPrefab, partySlots[i]);
                go.transform.localPosition = Vector3.zero;
                
                PlayerController pc = go.GetComponent<PlayerController>();
                // 메뉴 화면용 초기화 (BattleManager는 null)
                pc.Initialize(slotAssignments[i], null);
                
                // 파티 슬롯의 캐릭터 버튼은 클릭되지 않도록 설정 (직접 조작하므로)
                if(pc.selectButton) pc.selectButton.interactable = false;

                spawnedControllers[i] = pc;
                int slotIndex = i;
                AddMouseEvents(go, slotIndex);
            }
        }

        private void AddMouseEvents(GameObject go, int index)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => {
                if (currentState == MenuState.SelectEquipChar || currentState == MenuState.SelectStatusChar)
                {
                    if (spawnedControllers[index] != null)
                    {
                        currentPartySelectIndex = index;
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                        foreach(var pc in spawnedControllers) pc.ResetHighlightColor();
                        spawnedControllers[index].SetHighlightColor(charHighlightColor);
                    }
                } 
            });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => {
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;
                
                if (spawnedControllers[index] != null)
                {
                    var targetChar = spawnedControllers[index].sourceData;
                    if (currentState == MenuState.SelectEquipChar)
                    {
                        OpenEquipUI(targetChar);
                    }
                    else if (currentState == MenuState.SelectStatusChar)
                    {
                        OpenStatusUI(targetChar);
                    }
                }
                else
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                }
            });
            trigger.triggers.Add(clickEntry);
        }

        private int GetIndexFromRowColumn(RowType row, ColumnType col)
        {
            int rowIndex = (row == RowType.Front) ? 0 : 3;
            return rowIndex + (int)col;
        }

        // 캐릭터 선택 조작 (방향키)
        private void HandleCharacterSelection()
        {
            bool moved = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { if (currentPartySelectIndex % 3 > 0) { currentPartySelectIndex--; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { if (currentPartySelectIndex % 3 < 2) { currentPartySelectIndex++; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { if (currentPartySelectIndex >= 3) { currentPartySelectIndex -= 3; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { if (currentPartySelectIndex < 3) { currentPartySelectIndex += 3; moved = true; } }

            if (moved)
            {
                UpdatePartyHighlight();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                ResetInputTimer();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (spawnedControllers[currentPartySelectIndex] != null)
                {
                    var targetChar = spawnedControllers[currentPartySelectIndex].sourceData;

                    if (currentState == MenuState.SelectEquipChar)
                    {
                        OpenEquipUI(targetChar);
                    }
                    else if (currentState == MenuState.SelectStatusChar)
                    {
                        OpenStatusUI(targetChar);
                    }
                }
                else
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                }
            }

            // 취소. 메인 메뉴로 복귀
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                CancelCharacterSelection();
            }
        }

        private void UpdatePartyHighlight()
        {
            // 모든 하이라이트 끄기
            for(int i=0; i<6; i++) 
                if(spawnedControllers[i] != null) spawnedControllers[i].ResetHighlightColor();

            // 현재 선택된 캐릭터 하이라이트
            if(spawnedControllers[currentPartySelectIndex] != null)
                spawnedControllers[currentPartySelectIndex].SetHighlightColor(charHighlightColor);
        }

        private void CancelCharacterSelection()
        {
            // 하이라이트 초기화
            for(int i=0; i<6; i++) 
                if(spawnedControllers[i] != null) spawnedControllers[i].ResetHighlightColor();

            currentState = MenuState.Main;
            ResetInputTimer();
            UpdateSelection(currentBtnIndex); // EQUIP 버튼 재선택
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        void UpdateSelection(int index, bool sound = true)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0 || index < 0 || index >= allMenuBtns.Count) return;
            allMenuBtns[index].Select();
            if (sound) ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
        }

        public void OnClick_Skill()
        {
            currentState = MenuState.Skill;
            skillUI.SetActive(true);
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        public void CloseSkillUI()
        {
            skillUI.SetActive(false);

            // 변경된 위치 데이터를 다시 읽어와 파티 슬롯을 새로고침
            RefreshPartyUI();

            currentState = MenuState.Main;
            ResetInputTimer();
            UpdateSelection(currentBtnIndex);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        public void OnClick_Spirit()
        {
            // currentState = MenuState.Spirit;
            // UpdatePopupMessage();
            // ResetInputTimer();
            Debug.Log("SPIRIT 미구현");
        }

        public void OnClick_Compile()
        {
            OpenCompileUI();
        }

        public void OnClick_Module()
        {
            OpenModuleUI();
        }

        public void OnClick_Tactics()
        {
            // currentState = MenuState.Tactics;
            // UpdatePopupMessage();
            // ResetInputTimer();
            Debug.Log("TACTICS 미구현");
        }

        public void OnClick_Item()
        {
            currentState = MenuState.Item;
            itemUI.SetActive(true);
            UpdatePopupMessage();
            ResetInputTimer();
        }

        public void CloseItemUI()
        {
            itemUI.SetActive(false);

            // 변경된 위치 데이터를 다시 읽어와 파티 슬롯을 새로고침
            RefreshPartyUI();

            currentState = MenuState.Main;
            ResetInputTimer();
            UpdateSelection(currentBtnIndex); // 마지막으로 선택했던 메인 메뉴 버튼에 다시 포커스
            
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        public void OnClick_Status()
        {
            currentState = MenuState.SelectStatusChar;
            
            // 첫 번째 유효한 캐릭터를 찾아 포커스 (Equip과 동일 로직)
            currentPartySelectIndex = 0;
            for(int i=0; i<6; i++) {
                if(spawnedControllers[i] != null) { currentPartySelectIndex = i; break; }
            }

            UpdatePartyHighlight();
            EventSystem.current.SetSelectedGameObject(null);
            
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        private bool IsAnySubMenuOpen()
        {
            // 열려있는 서브 UI가 단 하나라도 있다면 true 반환
            if (statusUI != null && statusUI.activeInHierarchy) return true;
            if (moveUI != null && moveUI.activeInHierarchy) return true;
            if (skillUI != null && skillUI.activeInHierarchy) return true;
            if (itemUI != null && itemUI.activeInHierarchy) return true;
            if (equipUI != null && equipUI.activeInHierarchy) return true;
            if (moduleUI != null && moduleUI.activeInHierarchy) return true;
            return false;
        }

        // 실제 StatusUI 열기
        private void OpenStatusUI(RuntimeCharacterData charData)
        {
            currentState = MenuState.Status;
            statusUI.SetActive(true);

            // StatusUIController에 선택된 캐릭터 전달
            StatusUIController statusController = statusUI.GetComponentInChildren<StatusUIController>();
            if (statusController != null)
            {
                statusController.SetTargetCharacter(charData);
            }

            UpdatePopupMessage();
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        // 닫을 때 캐릭터 선택 화면으로 복귀
        public void CloseStatusUI()
        {
            statusUI.SetActive(false);
            
            // 메인 메뉴가 아닌 캐릭터 선택 상태로 복귀
            currentState = MenuState.SelectStatusChar;
            
            // 하이라이트 복구
            UpdatePartyHighlight();
            
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        private void OpenCompileUI()
        {
            currentState = MenuState.Compile;
            compileUI.SetActive(true);

            // CompileUI가 열릴 때 Controller를 초기화
            CompileUIController compileController = compileUI.GetComponentInChildren<CompileUIController>();
            
            if (compileController != null)
                compileController.Initialize(this);
            
            EventSystem.current.SetSelectedGameObject(null);
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        // CompileUIController에서 몬스터 2마리를 골랐을 때 호출할 함수
        public void RequestCompilePopup()
        {
            currentState = MenuState.Compile;
            UpdatePopupMessage();
            confirmPopup.SetActive(true);
            isPopupOpen = true;
            popupNoBtn.Select();
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        public void CloseCompileUI()
        {
            compileUI.SetActive(false);
            currentState = MenuState.Main;
            RefreshPartyUI(); // 합체 결과를 반영
            
            ResetInputTimer();
            UpdateSelection(currentBtnIndex); 
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        // ModuleUI 열기
        private void OpenModuleUI()
        {
            currentState = MenuState.Module;
            moduleUI.SetActive(true);

            // 버튼 포커스 해제 
            EventSystem.current.SetSelectedGameObject(null);

            UpdatePopupMessage();
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        // 닫을 때 캐릭터 선택 화면으로 복귀
        public void CloseModuleUI()
        {
            moduleUI.SetActive(false);
            currentState = MenuState.Main;
            
            ResetInputTimer();
            
            // 메인 메뉴를 다시 조작할 수 있도록 원래 누르던 버튼으로 포커스 복구
            UpdateSelection(currentBtnIndex); 
            
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }
        
        public void OnClick_Equip()
        {
            // 바로 UI를 열지 않고, 캐릭터 선택 모드로 진입
            currentState = MenuState.SelectEquipChar;
            
            // 첫 번째 유효한 캐릭터를 찾아 포커스
            currentPartySelectIndex = 0;
            for(int i=0; i<6; i++) {
                if(spawnedControllers[i] != null) { currentPartySelectIndex = i; break; }
            }

            UpdatePartyHighlight();
            
            // 버튼 포커스 시각적 해제 (캐릭터 포커스와 겹치지 않게)
            EventSystem.current.SetSelectedGameObject(null);
            
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        // 실제 장비창 열기
        private void OpenEquipUI(RuntimeCharacterData charData)
        {
            currentState = MenuState.Equip;
            equipUI.SetActive(true);
            
            // EquipUIController에 선택된 캐릭터 전달
            EquipUIController equipController = equipUI.GetComponentInChildren<EquipUIController>();
            if(equipController != null)
            {
                equipController.SetCharacter(charData);
                equipController.onCloseCallback = CloseEquipUI; 
            }

            UpdatePopupMessage();
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        // 장비창 닫기 -> 캐릭터 선택 화면으로 복귀
        public void CloseEquipUI()
        {
            equipUI.SetActive(false);
            
            // 메인 메뉴가 아닌 캐릭터 선택 상태로 복귀
            currentState = MenuState.SelectEquipChar;
            
            // 하이라이트 복구
            UpdatePartyHighlight();
            
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }
        
        public void OnClick_Move()
        {
            currentState = MenuState.Move;
            moveUI.SetActive(true);
            ResetInputTimer();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        public void CloseMoveUI()
        {
            moveUI.SetActive(false);
            
            // 변경된 위치 데이터를 다시 읽어와 파티 슬롯을 새로고침
            RefreshPartyUI();

            currentState = MenuState.Main;
            ResetInputTimer();
            UpdateSelection(currentBtnIndex);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        public void OnClick_System()
        {
            // currentState = MenuState.System;
            // UpdatePopupMessage();
            // ResetInputTimer();
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
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
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
                case MenuState.Module:
                    popupMessage.SetText("설치할 공간이 부족합니다.");
                break;
                case MenuState.Compile:
                    popupMessage.SetText("선택한 몬스터들을 Compile 하시겠습니까?");
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
                    SaveManager.Instance.SaveGame(SaveManager.SUSPEND_SLOT_INDEX);
                    ManagerRoot.GameState.ChangeState(GameState.None);
                    UnityEngine.SceneManagement.SceneManager.LoadScene(GameScene.TITLE_SCENE);
                    break;

                case MenuState.Compile:
                    // 이때 내부 로직에 의해 currentState가 Main으로 바뀌고, 메인 메뉴 버튼이 강제 선택됨
                    ClosePopup();

                    // Main으로 덮어씌워진 상태를 다시 Compile 상태로 복구
                    currentState = MenuState.Compile;

                    // EventSystem이 방금 강제로 선택해버린 메인 메뉴 버튼의 포커스를 날림
                    // 이 처리가 없으면 합체 중 스페이스바를 누를 때 메인 메뉴 버튼이 클릭돼버림
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                    // 연출을 시작
                    CompileUIController compileController = compileUI.GetComponentInChildren<CompileUIController>();
                    if (compileController != null)
                        compileController.ExecuteCompileCutscene();
                    break;

                case MenuState.Item:
                case MenuState.Skill:
                case MenuState.Module:
                    ClosePopup();
                    break;

                default:
                    ClosePopup();
                    break;
            }
        }

        public void ShowAlertPopup(string message)
        {
            popupMessage.SetText(message);
            confirmPopup.SetActive(true);
            
            // AlertPopup은 선택이 아닌 단순 알림 용도이므로 YES버튼은 끄고 NO버튼의 문자는 확인(OK)으로 바꿈.
            popupYesBtn.gameObject.SetActive(false); 
            popupNoBtn.Select();
            var tmp = popupNoBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = "OK";
            isPopupOpen = true;
            isAlertMode = true;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);

            ResetInputTimer();
        }

        // 팝업 닫기
        private void ClosePopup()
        {
            // 알림 모드였다면 currentState를 초기화하지 않음 (Item 등 현재 상태 유지)
            if (!isAlertMode)
            {
                currentState = MenuState.Main;
            }

            confirmPopup.SetActive(false);
            popupYesBtn.gameObject.SetActive(true); // 버튼 복구
            var tmp = popupNoBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = "NO"; // 버튼 표시 문자 복구
            isPopupOpen = false;
            isAlertMode = false;

            // 메인 메뉴 상태라면 메인 버튼으로 포커스 복귀
            if (currentState == MenuState.Main && allMenuBtns.Count > currentBtnIndex)
            {
                allMenuBtns[currentBtnIndex].Select();
            }
        }

        // 팝업 NO 버튼
        public void OnClickCancelButton()
        {
            ResetInputTimer();
            
            // 팝업이 닫히면 ClosePopup() 내부에서 currentState가 Main으로 덮어씌워지므로 임시 저장
            MenuState tempState = currentState;

            ClosePopup();
            
            // Compile 팝업을 취소한 경우의 예외 처리
            if (tempState == MenuState.Compile)
            {
                // 메인으로 돌아가버린 상태를 다시 Compile 상태로 복구
                currentState = MenuState.Compile; 

                // CompileUIController의 선택 초기화 함수 호출
                CompileUIController compileController = compileUI.GetComponentInChildren<CompileUIController>();
                if (compileController != null)
                {
                    compileController.ClearSelection();
                }
            }
            
            UpdatePopupMessage();
        }
    }
}

