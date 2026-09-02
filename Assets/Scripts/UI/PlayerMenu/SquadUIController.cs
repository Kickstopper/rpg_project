using System.Collections.Generic;
using System.Linq;
using Data;
using Manager;
using UI.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Controller;
using UI.Common;

namespace UI
{
    public class SquadUIController : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject playerPrefab;
        public Transform groupA_Container; // 전투 참여 멤버 표시용 (조작 불가)
        public Transform groupB_Container; // 교체 가능한 몬스터 풀 (조작 전용)

        private PlayerMenuController menuController;
        
        private List<PlayerController> interactablePanels = new List<PlayerController>(); // 조작 가능한 그룹 B의 패널들만 관리
        
        private int currentFocusedIndex = 0;
        private int columns = 3; // 내비게이션용 열 개수 (GridLayoutGroup과 동기화됨)

        public void Initialize(PlayerMenuController controller)
        {
            this.menuController = controller;
            RefreshSquadUI();
        }

        private void RefreshSquadUI()
        {
            // 기존 패널 삭제
            foreach (Transform child in groupA_Container) Destroy(child.gameObject);
            foreach (Transform child in groupB_Container) Destroy(child.gameObject);
            interactablePanels.Clear();

            // GridLayoutGroup의 열 개수 동기화 (GroupB 기준으로 가져옴)
            GridLayoutGroup grid = groupB_Container.GetComponent<GridLayoutGroup>();
            if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = grid.constraintCount;
            }

            var party = ManagerRoot.Party.partyData;

            // 인간 강제 전투 참여 처리
            foreach (var member in party)
            {
                if (member.race == Race.Human)
                {
                    member.isRegular = true;
                }
            }

            // 그룹 A 세팅: 출전 멤버 (isRegular == true) 단순 표시
            foreach (var member in party)
            {
                if (member.isRegular)
                {
                    GameObject go = Instantiate(playerPrefab, groupA_Container);
                    PlayerController pc = go.GetComponent<PlayerController>();
                    pc.Initialize(member, null);
                    
                    // 그룹 A는 단순 표시용이므로 클릭 상호작용 차단
                    if (pc.selectButton != null) pc.selectButton.interactable = false;
                    
                    // 호버 이벤트 등이 있다면 겹치지 않게 제거
                    EventTrigger trigger = go.GetComponent<EventTrigger>();
                    if (trigger != null) trigger.triggers.Clear();
                }
            }

            // 그룹 B 세팅: 몬스터 풀 (isMonster == true) 조작 패널
            foreach (var member in party)
            {
                if (member.isMonster)
                {
                    GameObject go = Instantiate(playerPrefab, groupB_Container);
                    PlayerController pc = go.GetComponent<PlayerController>();
                    pc.Initialize(member, null);

                    // 딤 처리 로직
                    pc.SetSquadIndicator(member.isRegular);

                    if (member.isRegular)
                    {
                        pc.SetMessage("IN SQUAD");
                    }
                    else
                    {
                        pc.SetMessage("");
                    }

                    interactablePanels.Add(pc);
                    int index = interactablePanels.Count - 1;

                    // 클릭 이벤트 연결
                    if (pc.selectButton != null)
                    {
                        pc.selectButton.interactable = true;
                        pc.selectButton.onClick.RemoveAllListeners();
                        pc.selectButton.onClick.AddListener(() => OnClickCharacter(index));
                    }

                    // 마우스 호버 시 포커스 갱신
                    EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                    trigger.triggers.Clear();
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enterEntry.callback.AddListener((data) => {
                        if (menuController.IsPopupOpen || menuController.isInputLocked) return;
                        currentFocusedIndex = index;
                        UpdateHighlight();
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    });
                    trigger.triggers.Add(enterEntry);
                }
            }

            // 포커스 인덱스 안전장치
            if (interactablePanels.Count > 0)
            {
                currentFocusedIndex = Mathf.Clamp(currentFocusedIndex, 0, interactablePanels.Count - 1);
            }
            else
            {
                currentFocusedIndex = -1; 
            }

            UpdateHighlight();
        }

        private void Update()
        {
            // 입력 잠금 및 팝업 상태 시 자체 조작 무시
            if (menuController.isInputLocked || menuController.IsPopupOpen) return;

            // 마우스/터치 취소 처리
            if (GameInput.GetCancelDown())
            {
                CloseUI();
                return;
            }

            // PlayerMenuController의 타이머를 통과하지 못하면 무시
            if (!menuController.CanProcessInput) return;

            HandleNavigation();
        }

        private void HandleNavigation()
        {
            // 상호작용 가능한 그룹 B 몬스터가 한 마리도 없을 때는 취소 키만 허용
            if (interactablePanels.Count == 0)
            {
                if (GameInput.GetCancelDown())
                {
                    CloseUI();
                }
                return;
            }

            bool moved = false;
            int total = interactablePanels.Count;

            // 방향키 조작
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { currentFocusedIndex = (currentFocusedIndex - 1 + total) % total; moved = true; }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { currentFocusedIndex = (currentFocusedIndex + 1) % total; moved = true; }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) 
            { 
                if (currentFocusedIndex - columns >= 0) currentFocusedIndex -= columns;
                else
                {
                    int bottom = currentFocusedIndex;
                    while (bottom + columns < total) bottom += columns;
                    currentFocusedIndex = bottom;
                }
                moved = true; 
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) 
            { 
                if (currentFocusedIndex + columns < total) currentFocusedIndex += columns;
                else currentFocusedIndex = currentFocusedIndex % columns;
                moved = true; 
            }

            if (moved)
            {
                UpdateHighlight();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                menuController.ResetInputTimer();
                return;
            }

            // 확인 키
            if (UI.Common.GameInput.GetSelectDown())
            {
                OnClickCharacter(currentFocusedIndex);
                menuController.ResetInputTimer();
            }

            // 취소 키
            if (GameInput.GetCancelDown())
            {
                CloseUI();
            }
        }

        private void OnClickCharacter(int index)
        {
            if (index < 0 || index >= interactablePanels.Count) return;

            RuntimeCharacterData targetChar = interactablePanels[index].sourceData;

            if (targetChar.isRegular)
            {
                // 이미 출전 중인 멤버를 클릭하면 대기 상태로
                targetChar.isRegular = false;
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                RefreshSquadUI();
            }
            else
            {
                // 대기 중인 몬스터를 클릭하여 출전시키려 할 때 인원 검사
                int currentRegulars = ManagerRoot.Party.partyData.Count(c => c.isRegular);
                
                if (currentRegulars < PartyManager.MAX_PARTY_SIZE)
                {
                    targetChar.isRegular = true;
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                    RefreshSquadUI();
                }
                else
                {
                    // 최대 인원 초과 시 경고 팝업 호출
                    menuController.ShowAlertPopup($"전투 참여 인원은 최대 {PartyManager.MAX_PARTY_SIZE}명입니다.");
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                }
            }
        }

        private void UpdateHighlight()
        {
            EventSystem.current.SetSelectedGameObject(null);

            // 상호작용 가능한 그룹 B 패널들의 하이라이트 초기화
            foreach (var pc in interactablePanels)
            {
                pc.ResetHighlightColor();
            }

            // 현재 포커스된 그룹 B 멤버만 하이라이트 적용
            if (currentFocusedIndex >= 0 && currentFocusedIndex < interactablePanels.Count)
            {
                interactablePanels[currentFocusedIndex].SetHighlightColor(menuController.charHighlightColor);
            }
        }

        private void CloseUI()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
            menuController.CloseSquadUI();
            menuController.ResetInputTimer();
        }
    }
}