using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Data;
using Manager;
using UI.PlayerMenu;
using Helper;
using UnityEngine.UI;

namespace Controller
{
    public class CompileUIController : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject monsterPanelPrefab;
        public Transform monsterGridList;
        public GameObject monsterSelectPanel; // 리스트 화면
        public SelectedMonsterIndicator firstMonster;
        public SelectedMonsterIndicator secondMonster;
        public SelectedMonsterIndicator resultMonster;
        public GameObject compilePanel;       // 연출 화면
        public MonsterCompileManager compileManager;

        private int columns; // GridLayoutGroup의 열 개수

        private PlayerMenuController menuController;
        private List<MonsterPanelController> spawnedPanels = new List<MonsterPanelController>();
        private List<string> selectedMonsterIDs = new List<string>();
        
        private int currentFocusedIndex = 0;
        private bool isEmptyState = false; // 몬스터가 한 마리도 없는 상태인지 체크

        public void Initialize(PlayerMenuController controller)
        {
            this.menuController = controller;

            GridLayoutGroup gridLayout = monsterGridList.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                // Constraint 설정이 Fixed Column Count로 되어있는지 확인
                if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                {
                    columns = gridLayout.constraintCount;
                }
                else
                {
                    Debug.LogWarning("[CompileUI] GridLayoutGroup의 Constraint가 'Fixed Column Count'가 아닙니다! 상/하 이동이 꼬일 수 있습니다.");
                    columns = gridLayout.constraintCount > 0 ? gridLayout.constraintCount : 4; // 임시 예외 처리
                }
            }
            
            // 패널 및 상태 초기화
            compilePanel.SetActive(false);
            monsterSelectPanel.SetActive(true);
            
            selectedMonsterIDs.Clear();
            currentFocusedIndex = 0;
            isEmptyState = false;

            RefreshMonsterGrid();
            RefreshSelectedMonsterPanels();
        }

        private void RefreshMonsterGrid()
        {
            // 기존 목록 삭제
            foreach (Transform child in monsterGridList) Destroy(child.gameObject);
            spawnedPanels.Clear();

            // 파티 리스트 불러오기
            var party  = ManagerRoot.Party.partyData;
            // "enemy_"로 시작하는 몬스터만 필터링하여 새 리스트 생성
            var validMonsters = party.FindAll(m => m != null && 
                                                 !string.IsNullOrEmpty(m.characterId) && 
                                                 m.characterId.StartsWith("enemy_"));

            // 1. 합체 가능한 몬스터가 한 마리도 없는 경우 처리
            if (validMonsters.Count == 0)
            {
                monsterSelectPanel.SetActive(false); // 리스트 UI 가리기
                isEmptyState = true; // 비어있는 상태 켜기
                return;
            }

            // 2. 유효한 몬스터가 있는 경우 정상적으로 리스트 생성
            monsterSelectPanel.SetActive(true);
            isEmptyState = false;

            for (int i = 0; i < validMonsters.Count; i++)
            {
                var monster = validMonsters[i];

                GameObject go = Instantiate(monsterPanelPrefab, monsterGridList);
                MonsterPanelController panelCtrl = go.GetComponent<MonsterPanelController>();
                
                panelCtrl.Initialize(monster.characterId); 
                spawnedPanels.Add(panelCtrl);

                int index = i;
                
                // 마우스 클릭 이벤트
                panelCtrl.selectButton.onClick.AddListener(() => OnClickMonster(index));

                // 마우스 호버 이벤트
                EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) => {
                    if (menuController.IsPopupOpen || compilePanel.activeSelf) return; // 팝업 중 방지
                    currentFocusedIndex = index;
                    UpdateHighlight();
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                });
                trigger.triggers.Add(enterEntry);
            }

            UpdateHighlight();
        }

        private void Update()
        {
            // PlayerMenu 전체의 글로벌 잠금 상태를 확인하여 입력 차단
            if (menuController.isInputLocked) return;

            // 팝업이 열려있거나, 합체 연출 중일 때도 자체 입력을 무시
            if (menuController.IsPopupOpen || compilePanel.activeSelf) return;

            // 몬스터가 없어서 화면이 가려진 상태일 때의 단독 입력 처리
            if (isEmptyState)
            {
                if (Input.anyKeyDown)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    menuController.CloseCompileUI();
                }
                return; 
            }

            // PlayerMenuController의 딜레이 타이머를 통과하지 못하면 무시
            if (!menuController.CanProcessInput) return;

            // 위의 모든 조건을 통과했을 때만 네비게이션(방향키/선택) 허용
            HandleNavigation();
        }

        private void HandleNavigation()
        {
            if (spawnedPanels.Count == 0) return;

            bool moved = false;
            int total = spawnedPanels.Count;

            // 방향키 이동
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { currentFocusedIndex = (currentFocusedIndex - 1 + total) % total; moved = true; }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { currentFocusedIndex = (currentFocusedIndex + 1) % total; moved = true; }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { currentFocusedIndex = (currentFocusedIndex - columns + total) % total; moved = true; }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { currentFocusedIndex = (currentFocusedIndex + columns) % total; moved = true; }

            if (moved)
            {
                UpdateHighlight();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                menuController.ResetInputTimer();
                return;
            }

            // 확인 키
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                OnClickMonster(currentFocusedIndex);
                menuController.ResetInputTimer();
            }

            // 취소 키
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab))
            {
                if (selectedMonsterIDs.Count > 0)
                {
                    selectedMonsterIDs.Clear();
                    UpdateHighlight();
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                }
                else
                {
                    menuController.CloseCompileUI(); 
                }
                menuController.ResetInputTimer();
            }
        }

        private void OnClickMonster(int index)
        {
            string monsterID = spawnedPanels[index].currentMonsterID;

            // 이미 선택된 몬스터를 다시 누르면 선택 취소
            if (selectedMonsterIDs.Contains(monsterID))
            {
                selectedMonsterIDs.Remove(monsterID);
                UpdateHighlight();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 2마리가 꽉 안 찼을 때만 추가
            if (selectedMonsterIDs.Count < 2)
            {
                selectedMonsterIDs.Add(monsterID);
                UpdateHighlight();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                RefreshSelectedMonsterPanels();

                // 2마리가 모두 선택된 순간, 합체 결과를 미리 예측하여 중복 검사를 수행
                if (selectedMonsterIDs.Count == 2)
                {
                    string idA = selectedMonsterIDs[0];
                    string idB = selectedMonsterIDs[1];

                    // 합체 결과 몬스터 데이터를 미리 가져옴
                    var resultEntry = ManagerRoot.Database.monsterDB.GetCompileResult(idA, idB);

                    if (resultEntry != null)
                    {
                        resultMonster.SetUI(new RuntimeCharacterData(MonsterConversionHelper.ToCharacterEntry(resultEntry)));
                        // partyData에 동일한 ID를 가진 몬스터가 존재하는지 검사
                        bool isAlreadyInParty  = ManagerRoot.Party.partyData.Exists(m => 
                            m != null && m.characterId == resultEntry.id);

                        if (isAlreadyInParty)
                        {
                            menuController.ShowAlertPopup("이미 파티에 존재하는 몬스터입니다.\n다른 조합을 선택해 주세요.");

                            // 직전에 선택한 두 번째 몬스터의 선택만 취소
                            selectedMonsterIDs.RemoveAt(1);
                            UpdateHighlight();
                            return;
                        }
                    }

                    // 중복이 없다면 정상 진행 팝업을 띄웁니다.
                    menuController.RequestCompilePopup();
                }
            }
        }
        
        private void RefreshSelectedMonsterPanels()
        {
            firstMonster.ResetUI();
            secondMonster.ResetUI();
            resultMonster.ResetUI();
            if (selectedMonsterIDs.Count > 0)
            {
                for(var i =0; i <selectedMonsterIDs.Count; i++)
                {
                    RuntimeCharacterData data = ManagerRoot.Party.GetCharacterByID(selectedMonsterIDs[i]);
                    if (i == 0) firstMonster.SetUI(data);
                    if (i == 1) secondMonster.SetUI(data);
                }
            }
        }
        private void UpdateHighlight()
        {
            EventSystem.current.SetSelectedGameObject(null); 

            for (int i = 0; i < spawnedPanels.Count; i++)
            {
                bool isSelected = selectedMonsterIDs.Contains(spawnedPanels[i].currentMonsterID);
                bool isFocused = (i == currentFocusedIndex);
                
                spawnedPanels[i].SetVisualState(isFocused, isSelected);
            }
        }

        public void ExecuteCompileCutscene()
        {
            monsterSelectPanel.SetActive(false); 
            compilePanel.SetActive(true);        

            // 컷신 연출이 시작될 때 PlayerMenu의 모든 입력을 잠금
            menuController.isInputLocked = true;

            compileManager.OnCompileFinished = () => 
            {
                compilePanel.SetActive(false);
                monsterSelectPanel.SetActive(true);
                selectedMonsterIDs.Clear();
                RefreshMonsterGrid(); 
                RefreshSelectedMonsterPanels();

                // 컷신 연출이 완전히 끝났을 때 입력을 다시 풀어줌
                menuController.isInputLocked = false;
                
                // 오작동을 막기 위해 입력 타이머를 한 번 초기화
                menuController.ResetInputTimer(); 
            };

            compileManager.StartCompileSequence(selectedMonsterIDs[0], selectedMonsterIDs[1]);
        }

        // 확인 팝업에서 취소를 눌렀을 때 호출될 외부용 함수
        public void ClearSelection()
        {
            if (selectedMonsterIDs.Count > 0)
            {
                selectedMonsterIDs.Clear();
                UpdateHighlight(); // 하이라이트를 모두 기본색으로 되돌림.
                RefreshSelectedMonsterPanels();
            }
        }
    }
}