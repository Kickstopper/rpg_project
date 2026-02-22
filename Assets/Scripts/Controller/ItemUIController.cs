using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
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
        public Color disabledTextColor = Color.gray;   
        public Color enabledTextColor = Color.white;   

        private List<string> inventoryItemIds;
        private int currentItemIndex = 0;
        private int currentPartyIndex = 0; // 타겟 커서 인덱스

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
                    bool isUsable = (itemData.useType == UseType.All || itemData.useType == UseType.Exploration);
                    var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                    foreach(var t in texts) t.color = isUsable ? enabledTextColor : disabledTextColor;
                }
            }

            if (inventoryItemIds.Count > 0) 
            {
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

                if (member != null) partyControllers[i].Initialize(member, null, true);
                else partyControllers[i].InitializeEmpty(i);
            }
        }

        void Update()
        {
            if (!menuController.CanProcessInput) return;

            if (menuController.IsPopupOpen) 
            {
                wasPopupOpen = true; 
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

            // 사용 불가 아이템 체크
            if (selectedItemData.useType != UseType.All && selectedItemData.useType != UseType.Exploration)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 부활 아이템 사용 조건 체크
            if (selectedItemData.effectType == EffectType.Revive_Empty || selectedItemData.effectType == EffectType.Revive_Fully)
            {
                bool hasDeadMember = false;
                foreach (var pc in partyControllers)
                {
                    if (!pc.IsEmpty && pc.currentHp <= 0)
                    {
                        hasDeadMember = true;
                        break;
                    }
                }

                if (!hasDeadMember)
                {
                    menuController.ShowAlertPopup("죽은 동료가 없습니다.");
                    return;
                }
            }

            isSelectingTarget = true;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            ApplyTargetHighlight();
        }

        private void ApplyTargetHighlight()
        {
            // 초기 진입 시 커서 위치 설정 및 하이라이트
            foreach (var pc in partyControllers) pc.ResetHighlightColor();

            TargetScope scope = selectedItemData.targetScope;

            // 전체 대상은 모두 하이라이트
            if (scope == TargetScope.All_Allies || scope == TargetScope.All_Dead_Allies)
            {
                foreach (var pc in partyControllers) 
                {
                    if (pc.IsEmpty) continue;
                    
                    // 죽은 자 전체 대상
                    if (scope == TargetScope.All_Dead_Allies && pc.currentHp > 0) continue;
                    
                    pc.SetHighlightColor(targetHighlightColor);
                }
                return;
            }

            // 단일 대상 (초기 커서 위치 계산)
            if (scope == TargetScope.One_Ally)
            {
                // 살아있는 첫 번째 아군
                int validIdx = GetFirstValidMemberIndex();
                currentPartyIndex = (validIdx != -1) ? validIdx : 0;
            }
            else if (scope == TargetScope.Dead_Ally)
            {
                // 죽은 첫 번째 아군
                int deadIdx = GetFirstDeadMemberIndex();
                currentPartyIndex = (deadIdx != -1) ? deadIdx : 0;
            }
            else // Self 등
            {
                currentPartyIndex = 0;
            }

            // 현재 커서 위치 하이라이트
            UpdatePartyCursorVisuals();
        }

        // 3x2 그리드 네비게이션 적용
        private void HandlePartyNavigation()
        {
            TargetScope scope = selectedItemData.targetScope;
            
            // 단일 대상일 때만 커서 이동 가능
            if (scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally || scope == TargetScope.Self)
            {
                bool moved = false;

                // [좌/우]
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    if (currentPartyIndex % 3 > 0) { currentPartyIndex--; moved = true; }
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    if (currentPartyIndex % 3 < 2) { currentPartyIndex++; moved = true; }
                }
                // [상/하]
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    if (currentPartyIndex >= 3) { currentPartyIndex -= 3; moved = true; }
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    if (currentPartyIndex < 3) { currentPartyIndex += 3; moved = true; }
                }

                if (moved)
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                    UpdatePartyCursorVisuals();
                }
            }

            // 확인
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                UseItemOnTarget();
            }

            // 취소
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                CancelTargetSelection();
            }
        }

        // 커서 위치만 업데이트하는 함수
        private void UpdatePartyCursorVisuals()
        {
            // 전체 초기화
            foreach (var pc in partyControllers) pc.ResetHighlightColor();

            // 현재 커서 위치만 하이라이트 (유효성 검사는 실행 시 수행)
            // 단, 빈 슬롯이라도 커서는 표시할 수 있어야 함 (SkillUI와 동일 동작)
            if (currentPartyIndex >= 0 && currentPartyIndex < 6)
            {
                partyControllers[currentPartyIndex].SetHighlightColor(targetHighlightColor);
            }
        }

        private void CancelTargetSelection()
        {
            isSelectingTarget = false;
            foreach (var pc in partyControllers) pc.ResetHighlightColor(); 
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            // 아이템 리스트로 포커스 복귀
            UpdateItemSelection();
        }

        private void UseItemOnTarget()
        {
            // 데이터 유효성 체크
            if (selectedItemData == null) return;

            // 효과 적용 시도 (EffectManager 위임)
            // 타겟을 인터페이스로 가져옴
            PlayerController targetPC = partyControllers[currentPartyIndex];
            IBattleTarget battleTarget = targetPC;

            // EffectManager 호출 (데이터 수정 -> UI 갱신 자동 수행)
            if (EffectManager.Instance.ApplyEffect(battleTarget, selectedItemData))
            {
                // 아이템 소모 (인벤토리 반영)
                InventoryManager.Instance.UseItem(selectedItemData.id);
                
                // 효과음 재생
                SoundManager.Instance.PlaySFX(SfxID.UI_Click); 

                // UI 리스트 갱신 (수량 변화 반영)
                RefreshItemList(); 

                // 연속 사용 처리 로직 (UX)
                int remainingCount = InventoryManager.Instance.GetItemCount(selectedItemData.id);

                if (remainingCount > 0)
                {
                    // 아이템이 아직 남았다면 타겟팅 모드를 유지.
                    // 아이템 정보창(수량)을 즉시 갱신.
                    if (itemInfo) itemInfo.UpdateInfo(selectedItemData); 
                }
                else
                {
                    // 아이템을 다 썼다면 타겟팅을 풀고 아이템 리스트로 포커스를 돌려줌.
                    CancelTargetSelection();
                    
                    // 리스트 인덱스 안전 장치 (방금 쓴 아이템이 사라졌으므로 커서 위치 조정)
                    if (inventoryItemIds.Count > 0)
                    {
                        currentItemIndex = Mathf.Clamp(currentItemIndex, 0, inventoryItemIds.Count - 1);
                        UpdateItemSelection();
                    }
                }
            }
            else
            {
                // 실패 처리 (예: HP가 가득 찬 대상에게 회복약 사용 시도)
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                // 필요하다면: menuController.ShowAlertPopup("효과가 없습니다.");
            }
        }

        private int GetFirstValidMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp > 0);
        private int GetFirstDeadMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp <= 0);
    }
}