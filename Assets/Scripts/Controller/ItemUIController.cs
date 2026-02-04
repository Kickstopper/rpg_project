using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using System.Linq;
using TMPro;

namespace Controller
{
    public class ItemUIController : MonoBehaviour
    {
        public PlayerMenuController menuController;

        [Header("Item Info (Center)")]
        public ItemInfoController itemInfo;

        [Header("Item List (Left)")]
        public Transform itemContent;         
        public GameObject itemSlotPrefab;     
        
        [Header("Party List (Right)")]
        public Transform[] partySlots;        
        public GameObject partyPrefab;
        private PlayerController[] partyControllers = new PlayerController[6];

        [Header("Highlight Colors")]
        public Color targetHighlightColor = Color.yellow; 
        public Color disabledTextColor = Color.gray;   // 사용 불가 아이템 텍스트 색상
        public Color enabledTextColor = Color.white;   // 사용 가능 아이템 텍스트 색상

        private List<string> inventoryItemIds;
        private int currentItemIndex = 0;
        private int currentPartyIndex = 0;

        private bool isSelectingTarget = false; 
        private ConsumableItemData selectedItemData;

        private bool wasPopupOpen = false;

        void OnEnable()
        {
            ResolvePositionConflicts();
            ResetUI();
            RefreshItemList();
            RefreshPartyList();
        }

        private void ResetUI()
        {
            currentItemIndex = 0;
            currentPartyIndex = 0;
            isSelectingTarget = false;
            selectedItemData = null;
            if (itemInfo) itemInfo.ResetText();
        }

        private void ResolvePositionConflicts()
        {
            var party = PartyManager.Instance.partyData;
            if (party == null || party.Count == 0) return;

            RuntimeCharacterData[] slotAssignments = new RuntimeCharacterData[6];
            List<RuntimeCharacterData> pending = new List<RuntimeCharacterData>();

            foreach (var member in party)
            {
                int targetIndex = GetIndexFromRowColumn(member.row, member.column);
                if (slotAssignments[targetIndex] == null) slotAssignments[targetIndex] = member;
                else pending.Add(member);
            }

            foreach (var pendingMember in pending)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (slotAssignments[i] == null)
                    {
                        slotAssignments[i] = pendingMember;
                        bool isFront = (i < 3);
                        pendingMember.row = isFront ? RowType.Front : RowType.Back;
                        pendingMember.column = (ColumnType)(isFront ? i : i - 3);
                        break;
                    }
                }
            }
        }

        private int GetIndexFromRowColumn(RowType row, ColumnType col)
        {
            int rowIndex = (row == RowType.Front) ? 0 : 3;
            return rowIndex + (int)col;
        }

        public void RefreshItemList()
        {
            foreach (Transform child in itemContent) Destroy(child.gameObject);
            itemContent.DetachChildren(); 

            inventoryItemIds = InventoryManager.Instance.GetAllItemIds();

            // UseType 순으로 정렬 (All -> Exploration -> Battle -> Passive 순)
            inventoryItemIds.Sort((idA, idB) => 
            {
                var itemA = DatabaseManager.Instance.GetConsumable(idA);
                var itemB = DatabaseManager.Instance.GetConsumable(idB);
                
                if (itemA == null && itemB == null) return 0;
                if (itemA == null) return 1;
                if (itemB == null) return -1;
                
                return itemA.useType.CompareTo(itemB.useType);
            });
            
            for (int i = 0; i < inventoryItemIds.Count; i++)
            {
                GameObject go = Instantiate(itemSlotPrefab, itemContent);
                var slot = go.GetComponent<SimpleListItemController>();
                var itemData = DatabaseManager.Instance.GetConsumable(inventoryItemIds[i]);

                if (slot != null && itemData != null)
                {
                    int count = InventoryManager.Instance.GetItemCount(itemData.id);
                    slot.SetData(itemData.dataName, count);

                    // 사용 가능 여부에 따른 Grayout 처리
                    // Exploration 환경에서 사용 가능한 타입: All, Exploration
                    bool isUsable = (itemData.useType == UseType.All || itemData.useType == UseType.Exploration);
                    
                    // Button btn = go.GetComponent<Button>();
                    // if (btn != null)
                    // {
                    //     btn.interactable = isUsable; // 버튼 상호작용 비활성화
                    // }

                    // 텍스트 색상 직접 변경
                    var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                    foreach(var t in texts)
                    {
                        t.color = isUsable ? enabledTextColor : disabledTextColor;
                    }
                }
            }

            if (inventoryItemIds.Count > 0) 
            {
                // 삭제 후 인덱스 보정
                if (currentItemIndex >= inventoryItemIds.Count) currentItemIndex = inventoryItemIds.Count - 1;
                UpdateItemSelection();
            }
        }

        public void RefreshPartyList()
        {
            var party = PartyManager.Instance.partyData;
            for (int i = 0; i < 6; i++)
            {
                foreach (Transform child in partySlots[i]) Destroy(child.gameObject);
                
                RowType r = (i < 3) ? RowType.Front : RowType.Back;
                ColumnType c = (ColumnType)(i % 3);
                var member = party.Find(m => m.row == r && m.column == c);

                GameObject go = Instantiate(partyPrefab, partySlots[i]);
                partyControllers[i] = go.GetComponent<PlayerController>();

                if (member != null) partyControllers[i].Initialize(member, null);
                else partyControllers[i].InitializeEmpty(i);
            }
        }

        void Update()
        {
            if (!menuController.CanProcessInput) return;

            if (menuController.IsPopupOpen) 
            {
                wasPopupOpen = true; // 팝업이 열려있음을 기록
                return;
            }

            if (wasPopupOpen)
            {
                wasPopupOpen = false;
                UpdateItemSelection();
            }

            if (isSelectingTarget)
                HandlePartyNavigation();
            else
                HandleItemNavigation();
        }

        private void HandleItemNavigation()
        {
            if (inventoryItemIds.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentItemIndex = (currentItemIndex - 1 + inventoryItemIds.Count) % inventoryItemIds.Count;
                UpdateItemSelection();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentItemIndex = (currentItemIndex + 1) % inventoryItemIds.Count;
                UpdateItemSelection();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SelectItem();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                menuController.CloseItemUI(); 
            }
        }

        private void UpdateItemSelection()
        {
            var buttons = itemContent.GetComponentsInChildren<Button>();
            if (buttons.Length > 0)
            {
                currentItemIndex = Mathf.Clamp(currentItemIndex, 0, buttons.Length - 1);
                buttons[currentItemIndex].Select();
                if (itemInfo)
                {
                    itemInfo.UpdateInfo(GetFocusedItemData());  
                }
            }
        }

        private ConsumableItemData GetFocusedItemData()
        {
            string itemId = inventoryItemIds[currentItemIndex];
            return DatabaseManager.Instance.GetConsumable(itemId);
        }

        private void SelectItem()
        {
            if (inventoryItemIds.Count == 0) return;
            if (currentItemIndex >= inventoryItemIds.Count) currentItemIndex = inventoryItemIds.Count - 1;

            string itemId = inventoryItemIds[currentItemIndex];
            selectedItemData = DatabaseManager.Instance.GetConsumable(itemId);

            if (selectedItemData == null) return;

            // 1. 사용 불가 아이템(Battle, Passive) 체크
            if (selectedItemData.useType != UseType.All && selectedItemData.useType != UseType.Exploration)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 2. 부활 아이템 사용 조건 체크 (죽은 동료가 있는가?)
            if (selectedItemData.effectType == EffectType.Revive_Empty || selectedItemData.effectType == EffectType.Revive_Fully)
            {
                bool hasDeadMember = false;
                foreach (var pc in partyControllers)
                {
                    // 빈 슬롯이 아니고, HP가 0 이하인 멤버가 하나라도 있는지 확인
                    if (!pc.IsEmpty && pc.currentHp <= 0)
                    {
                        hasDeadMember = true;
                        break;
                    }
                }

                if (!hasDeadMember)
                {
                    // 죽은 동료가 없으면 알림 팝업 호출 후 리턴
                    menuController.ShowAlertPopup("죽은 동료가 없습니다.");
                    return;
                }
            }

            // 조건 통과 시 타겟 선택 모드로 진입
            isSelectingTarget = true;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            ApplyTargetHighlight();
        }

        private void ApplyTargetHighlight()
        {
            foreach (var pc in partyControllers) pc.ResetHighlightColor();

            TargetScope scope = selectedItemData.targetScope;

            switch (scope)
            {
                case TargetScope.One_Ally:
                    currentPartyIndex = GetFirstValidMemberIndex();
                    if(currentPartyIndex != -1) partyControllers[currentPartyIndex].SetHighlightColor(targetHighlightColor);
                    break;
                case TargetScope.All_Allies:
                    foreach (var pc in partyControllers) if (!pc.IsEmpty) pc.SetHighlightColor(targetHighlightColor);
                    break;
                case TargetScope.Dead_Ally:
                    currentPartyIndex = GetFirstDeadMemberIndex();
                    if (currentPartyIndex != -1) partyControllers[currentPartyIndex].SetHighlightColor(targetHighlightColor);
                    break;
                case TargetScope.All_Dead_Allies: 
                    foreach (var pc in partyControllers) if (!pc.IsEmpty && pc.currentHp <= 0) pc.SetHighlightColor(targetHighlightColor);
                    break;
                case TargetScope.Self:
                    foreach (var pc in partyControllers) if (!pc.IsEmpty && pc.sourceData.isCommander) pc.SetHighlightColor(targetHighlightColor);
                    break;
            }
        }

        private void HandlePartyNavigation()
        {
            TargetScope scope = selectedItemData.targetScope;
            
            if (scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MovePartyCursor(-1);
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MovePartyCursor(1);
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                UseItemOnTarget();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                CancelTargetSelection();
            }
        }

        private void CancelTargetSelection()
        {
            isSelectingTarget = false;
            foreach (var pc in partyControllers) pc.ResetHighlightColor(); 
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
        }

        private void MovePartyCursor(int dir)
        {
            partyControllers[currentPartyIndex].ResetHighlightColor();
            
            int nextIdx = currentPartyIndex;
            for (int i = 0; i < 6; i++)
            {
                nextIdx = (nextIdx + dir + 6) % 6;
                if (!partyControllers[nextIdx].IsEmpty)
                {
                    if (selectedItemData.targetScope == TargetScope.Dead_Ally && partyControllers[nextIdx].currentHp > 0) continue;
                    if (selectedItemData.targetScope == TargetScope.One_Ally && partyControllers[nextIdx].currentHp <= 0) continue; 
                    break;
                }
            }
            
            currentPartyIndex = nextIdx;
            partyControllers[currentPartyIndex].SetHighlightColor(targetHighlightColor);
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        private void UseItemOnTarget()
        {
            bool success = false;
            TargetScope scope = selectedItemData.targetScope;

            if (scope == TargetScope.All_Allies)
            {
                foreach (var pc in partyControllers) if (!pc.IsEmpty) ApplyEffect(pc);
                success = true;
            }
            else if (scope == TargetScope.Self)
            {
                var cmdr = partyControllers.FirstOrDefault(p => !p.IsEmpty && p.sourceData.isCommander);
                if (cmdr != null) { ApplyEffect(cmdr); success = true; }
            }
            else if (scope == TargetScope.All_Dead_Allies)
            {
                foreach (var pc in partyControllers) if (!pc.IsEmpty && pc.currentHp <= 0) ApplyEffect(pc);
                success = true;
            }
            else
            {
                if (partyControllers[currentPartyIndex].IsEmpty) return;
                ApplyEffect(partyControllers[currentPartyIndex]);
                success = true;
            }

            if (success)
            {
                InventoryManager.Instance.UseItem(selectedItemData.id);
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                RefreshItemList(); 

                if (inventoryItemIds.Count > 0)
                {
                    currentItemIndex = Mathf.Clamp(currentItemIndex, 0, inventoryItemIds.Count - 1);
                }
                else
                {
                    currentItemIndex = 0;
                }

                isSelectingTarget = false;
                foreach (var pc in partyControllers) pc.ResetHighlightColor();
                if (itemInfo) itemInfo.ResetText();
                UpdateItemSelection();
            }
        }

        private void ApplyEffect(PlayerController target)
        {
            int hpRec = 0;
            int mpRec = 0;

            if (selectedItemData.effectType == EffectType.Recover_HP) hpRec = selectedItemData.effectValue;
            if (selectedItemData.effectType == EffectType.Recover_MP) mpRec = selectedItemData.effectValue;
            
            if (selectedItemData.effectType == EffectType.Revive_Empty || selectedItemData.effectType == EffectType.Revive_Fully)
            {
                if (target.currentHp <= 0)
                {
                    target.Revive(selectedItemData.effectValue);
                    target.sourceData.currentHp = target.currentHp;
                    return; 
                }
            }

            target.Recover(hpRec, mpRec);
            target.sourceData.currentHp = target.currentHp;
            target.sourceData.currentMp = target.currentMp;
        }

        private int GetFirstValidMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp > 0);
        private int GetFirstDeadMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp <= 0);
    }
}