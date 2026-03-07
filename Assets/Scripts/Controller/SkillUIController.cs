using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using TMPro;
using UI.Common;
using UnityEngine.EventSystems;
using System;

namespace Controller
{
    public class SkillUIController : MonoBehaviour
    {
        public enum SkillUIState { SelectCaster, SelectSkill, SelectTarget }

        public PlayerMenuController menuController;
        
        [Header("Skill Info (Center)")]
        public GameObject skillPanel;
        public SkillInfoController skillInfo;
        public Transform skillContent;
        public GameObject skillSlotPrefab;    

        [Header("Party List (Left)")]
        public Transform[] partySlots;
        public GameObject partyPrefab;
        private PlayerController[] partyControllers = new PlayerController[6];

        [Header("Skill List (Right)")]
        public TextMeshProUGUI mpText;
        public TextMeshProUGUI descriptionText;

        [Header("Highlight Colors")]
        public Color casterHighlightColor = new Color(0.5f, 0.5f, 1f, 1f); 
        public Color targetHighlightColor = Color.yellow;
        public Color disabledTextColor = Color.gray;
        public Color enabledTextColor = Color.white;

        // 상태 관리
        private SkillUIState currentState = SkillUIState.SelectCaster;
        
        // 인덱스 관리
        private int currentCasterIndex = 0; 
        private int currentSkillIndex = 0;  
        private int currentTargetIndex = 0; 

        // 데이터
        private List<string> currentSkillIds = new List<string>();
        private SkillData selectedSkillData;
        private PlayerController currentCaster;

        void OnEnable()
        {
            ResolvePositionConflicts();
            RefreshPartyList();
            
            currentState = SkillUIState.SelectCaster;
            currentCasterIndex = GetFirstValidMemberIndex();
            currentSkillIndex = 0;
            
            if (skillInfo) skillInfo.ResetText();

            // 유효한 캐릭터가 하나도 없을 경우의 예외처리
            if (currentCasterIndex == -1) currentCasterIndex = 0;

            RefreshSkillList(currentCasterIndex); 
            UpdateVisuals();
        }

        void Update()
        {
            if (!menuController.CanProcessInput) return;
            if (menuController.IsPopupOpen) return;

            switch (currentState)
            {
                case SkillUIState.SelectCaster:
                    HandleCasterSelection();
                    break;
                case SkillUIState.SelectSkill:
                    HandleSkillSelection();
                    break;
                case SkillUIState.SelectTarget:
                    HandleTargetSelection();
                    break;
            }
        }

        private void HandleCasterSelection()
        {
            bool moved = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { if (currentCasterIndex % 3 > 0) { currentCasterIndex--; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { if (currentCasterIndex % 3 < 2) { currentCasterIndex++; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { if (currentCasterIndex >= 3) { currentCasterIndex -= 3; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { if (currentCasterIndex < 3) { currentCasterIndex += 3; moved = true; } }

            if (moved)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                RefreshSkillList(currentCasterIndex); 
                UpdateVisuals();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (partyControllers[currentCasterIndex].IsEmpty || currentSkillIds.Count == 0)
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    return;
                }
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                currentState = SkillUIState.SelectSkill;
                currentSkillIndex = 0;
                UpdateVisuals();
                menuController.ResetInputTimer(); // 상태가 넘어간 직후 이중 입력 방지
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            {
                menuController.CloseSkillUI();
            }
        }

        private void HandleSkillSelection()
        {
            if (currentSkillIds.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentSkillIndex = (currentSkillIndex - 1 + currentSkillIds.Count) % currentSkillIds.Count;
                UpdateSkillScroll();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentSkillIndex = (currentSkillIndex + 1) % currentSkillIds.Count;
                UpdateSkillScroll();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) AttemptSelectSkill();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                currentState = SkillUIState.SelectCaster;
                UpdateVisuals();
            }
        }

        private void UpdateSkillScroll()
        {
            var buttons = skillContent.GetComponentsInChildren<Button>();
            if (buttons.Length > currentSkillIndex) buttons[currentSkillIndex].Select();
            UpdateSkillInfo();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        private void UpdateSkillInfo()
        {
            if (skillInfo == null || currentSkillIds == null || currentSkillIndex >= currentSkillIds.Count) return;
            string skillId = currentSkillIds[currentSkillIndex];
            SkillData skillData = DatabaseManager.Instance.GetSkill(skillId);
            skillInfo.UpdateInfo(skillData);
        }

        private void OnClickListItem(int skillIndex)
        {
            currentSkillIndex = skillIndex;
            AttemptSelectSkill();
        }

        private void AttemptSelectSkill()
        {
            // 유효성 검사 로직
            string skillId = currentSkillIds[currentSkillIndex];
            selectedSkillData = DatabaseManager.Instance.GetSkill(skillId);
            currentCaster = partyControllers[currentCasterIndex];

            if (selectedSkillData == null) return;

            if (!IsCostEnough() || selectedSkillData.useType != UseType.All && selectedSkillData.useType != UseType.Exploration)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            if (selectedSkillData.targetScope == TargetScope.Dead_Ally || selectedSkillData.targetScope == TargetScope.All_Dead_Allies)
            {
                if (GetFirstDeadMemberIndex() == -1)
                {
                    menuController.ShowAlertPopup("되살릴 대상이 없습니다.");
                    return;
                }
            }

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            currentState = SkillUIState.SelectTarget;
            InitializeTargetCursor();
            UpdateVisuals();
            menuController.ResetInputTimer(); // 쿨타임 갱신
        }

        private void HandleTargetSelection()
        {
            // 타겟 선택 로직
            TargetScope scope = selectedSkillData.targetScope;
            bool canMove = (scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally);

            if (canMove)
            {
                bool moved = false;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { if (currentTargetIndex % 3 > 0) { currentTargetIndex--; moved = true; } }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { if (currentTargetIndex % 3 < 2) { currentTargetIndex++; moved = true; } }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { if (currentTargetIndex >= 3) { currentTargetIndex -= 3; moved = true; } }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { if (currentTargetIndex < 3) { currentTargetIndex += 3; moved = true; } }

                if (moved)
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                    UpdateVisuals();
                }
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (IsCostEnough() && IsValidTarget(currentTargetIndex)) UseSkill();
                else SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                menuController.ResetInputTimer(); // 타겟 선택 완료 후 쿨타임 갱신
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                currentState = SkillUIState.SelectSkill;
                UpdateVisuals();
            }
        }

        private bool IsCostEnough()
        {
            if (currentCaster == null || selectedSkillData == null) return false;

            bool isMpCost = !selectedSkillData.useHpCost;
            int cost = selectedSkillData.costValue;

            if (isMpCost && currentCaster.currentMp < cost)
            {
                // "MP 부족" 메시지 등을 띄울 수 있음
                return false;
            }
            else if (!isMpCost && currentCaster.currentHp < cost)
            {
                return false;
            }
            return true;
        }

        private bool IsValidTarget(int index)
        {
            if (index < 0 || index >= partyControllers.Length ||partyControllers[index].IsEmpty || selectedSkillData == null) return false;
            PlayerController target = partyControllers[index];
            TargetScope scope = selectedSkillData.targetScope;

            if ((scope == TargetScope.Dead_Ally) && target.currentHp > 0) return false;
            if ((scope == TargetScope.One_Ally) && target.currentHp <= 0) return false;

            return true;
        }

        // EffectManager를 사용하여 스킬 발동
        private void UseSkill()
        {
            bool success = false;
            TargetScope scope = selectedSkillData.targetScope;

            // 효과 적용 시도
            if (scope == TargetScope.All_Allies || scope == TargetScope.All_Dead_Allies)
            {
                // 전체 대상: 한 명이라도 성공하면 OK
                foreach (var pc in partyControllers)
                {
                    if (pc.IsEmpty) continue;
                    
                    if (EffectManager.Instance.ApplyEffect(pc, selectedSkillData))
                    {
                        success = true;
                    }
                }
            }
            else if (scope == TargetScope.Self)
            {
                if (EffectManager.Instance.ApplyEffect(currentCaster, selectedSkillData))
                {
                    success = true;
                }
            }
            else // 단일 타겟 (One_Ally, Dead_Ally)
            {
                if (!partyControllers[currentTargetIndex].IsEmpty)
                {
                    if (EffectManager.Instance.ApplyEffect(partyControllers[currentTargetIndex], selectedSkillData))
                    {
                        success = true;
                    }
                }
            }

        
            // 결과 처리
            if (success)
            {
                SoundManager.Instance.PlaySFX(SfxID.Attack_Magic); 

                // 코스트 차감 (시전자)
                if (selectedSkillData.useHpCost)
                    currentCaster.currentHp -= selectedSkillData.costValue;
                else
                    currentCaster.currentMp -= selectedSkillData.costValue;

                currentCaster.sourceData.currentHp = currentCaster.currentHp;
                currentCaster.sourceData.currentMp = currentCaster.currentMp;

                // UI 갱신
                RefreshSkillList(currentCasterIndex); 
                // currentState = SkillUIState.SelectSkill; 
                UpdateVisuals();
            }
            else
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            }
        }

        private void UpdateVisuals()
        {
            if (skillPanel) skillPanel.SetActive(currentState != SkillUIState.SelectCaster);
            // 모든 하이라이트 초기화
            foreach (var pc in partyControllers) pc.ResetHighlightColor();

            // 시전자 하이라이트
            if (currentState == SkillUIState.SelectCaster || currentState == SkillUIState.SelectSkill || currentState == SkillUIState.SelectTarget)
            {
                if (!partyControllers[currentCasterIndex].IsEmpty)
                {
                    partyControllers[currentCasterIndex].SetHighlightColor(casterHighlightColor);
                }
            }

            // 스킬 리스트 버튼 Focus
            var buttons = skillContent.GetComponentsInChildren<Button>();
            if (currentState == SkillUIState.SelectSkill && buttons.Length > currentSkillIndex)
            {
                buttons[currentSkillIndex].Select();
                UpdateSkillInfo();
            }
            else if (currentState == SkillUIState.SelectTarget)
            {
                // 타겟을 고르는 중에도 스킬 설명을 유지합니다!
                EventSystem.current.SetSelectedGameObject(null);
                UpdateSkillInfo(); 
            }
            else
            {
                if (skillInfo) skillInfo.ResetText();
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (descriptionText)
            {
                string descText = string.Empty;
                if (currentState == SkillUIState.SelectCaster) descText = "누가 스킬을 사용합니까?";
                if (currentState == SkillUIState.SelectSkill) descText = "스킬을 선택해 주십시오.";
                if (currentState == SkillUIState.SelectTarget) descText = "누구에게 스킬을 사용합니까?";
                descriptionText.text = descText;
            }
        }

        private void HighlightTargets()
        {
            TargetScope scope = selectedSkillData.targetScope;
            Color blinkColor = Color.Lerp(Color.clear, targetHighlightColor, Mathf.PingPong(Time.time * 5f, 1f));

            if (scope == TargetScope.All_Allies || scope == TargetScope.All_Dead_Allies)
            {
                foreach (var pc in partyControllers) 
                    if (!pc.IsEmpty) pc.SetHighlightColor(blinkColor);
            }
            else if (scope == TargetScope.Self)
            {
                partyControllers[currentTargetIndex].SetHighlightColor(blinkColor);
            }
            else // 단일 타겟
            {
                partyControllers[currentTargetIndex].SetHighlightColor(blinkColor);
            }
        }

        void LateUpdate()
        {
            if (currentState == SkillUIState.SelectTarget)
            {
                HighlightTargets();
            }
        }

        private void RefreshSkillList(int casterIdx)
        {
            foreach (Transform child in skillContent) Destroy(child.gameObject);
            skillContent.DetachChildren();

            currentCaster = partyControllers[casterIdx];
            if (currentCaster.IsEmpty) 
            {
                currentSkillIds.Clear();
                return;
            }

            if (mpText) mpText.text = $"MP: {currentCaster.currentMp}/{currentCaster.maxMp}";
            currentSkillIds = currentCaster.learnedSkillIds;
            
            for (int i = 0; i < currentSkillIds.Count; i++)
            {
                string skillId = currentSkillIds[i];
                SkillData sData = DatabaseManager.Instance.GetSkill(skillId);
                if (sData == null) continue;

                GameObject go = Instantiate(skillSlotPrefab, skillContent);
                var slot = go.GetComponent<SimpleListItemView>();
                
                string costStr = sData.useHpCost ? $"{sData.costValue}HP" : $"{sData.costValue}MP";
                if(slot) slot.SetData(sData.dataName, costStr);

                bool isUsableType = (sData.useType == UseType.All || sData.useType == UseType.Exploration);
                bool hasResource = sData.useHpCost ? (currentCaster.currentHp > sData.costValue) : (currentCaster.currentMp >= sData.costValue);
                bool isUsable = isUsableType && hasResource;

                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                foreach(var t in texts) t.color = isUsable ? enabledTextColor : disabledTextColor;

                int itemIndex = i;
                Button btn = go.GetComponent<Button>();
                if (btn) btn.onClick.AddListener(() => OnClickListItem(itemIndex));

                // 마우스를 올렸을 때 포커스 및 설명창 갱신
                EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) => {
                    if (currentState == SkillUIState.SelectSkill && btn)
                    {
                        // 마우스가 올라간 스킬의 인덱스로 내부 변수 동기화
                        if (currentSkillIndex != itemIndex)
                        {
                            currentSkillIndex = itemIndex;
                            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                            btn.Select();
                            UpdateSkillInfo();
                        }
                    }
                    else
                    {
                        if (skillInfo) skillInfo.ResetText();
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                });
                trigger.triggers.Add(enterEntry);
            }
        }

        private void RefreshPartyList()
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

                int slotIndex = i; 
                AddMouseEvents(go, slotIndex);
            }
        }

        // 마우스 이벤트 동적 할당 및 처리
        // 마우스 이벤트 동적 할당 및 처리
        private void AddMouseEvents(GameObject go, int index)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => {
                if (!partyControllers[index].IsEmpty)
                {
                    if (currentState == SkillUIState.SelectCaster)
                    {
                        if (currentCasterIndex != index)
                        {
                            currentCasterIndex = index;
                            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                            UpdateVisuals(); 
                        }
                    }
                    else if (currentState == SkillUIState.SelectTarget)
                    {
                        if (currentTargetIndex != index)
                        {
                            currentTargetIndex = index;
                            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                            UpdateVisuals();
                        }
                    }
                }
            });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => {
                
                // 우클릭은 무시
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;

                if (currentState == SkillUIState.SelectCaster || currentState == SkillUIState.SelectSkill)
                {
                    currentCasterIndex = index; 

                    if (partyControllers[currentCasterIndex].IsEmpty || partyControllers[currentCasterIndex].learnedSkillIds.Count == 0)
                    {
                        SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                        return;
                    }
                    SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                    currentState = SkillUIState.SelectSkill;
                    currentSkillIndex = 0;
                    
                    currentCasterIndex = index;
                    RefreshSkillList(index);
                    UpdateVisuals();
                }
                else if (currentState == SkillUIState.SelectTarget)
                {
                    if (IsCostEnough() && IsValidTarget(index)) UseSkill();
                    else SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                }
            });
            trigger.triggers.Add(clickEntry);
        }

        private void InitializeTargetCursor()
        {
            TargetScope scope = selectedSkillData.targetScope;
            
            if (scope == TargetScope.Self)
            {
                currentTargetIndex = currentCasterIndex;
            }
            else if (scope == TargetScope.Dead_Ally || scope == TargetScope.All_Dead_Allies)
            {
                int deadIdx = GetFirstDeadMemberIndex();
                currentTargetIndex = (deadIdx != -1) ? deadIdx : 0;
            }
            else
            {
                int validIdx = GetFirstValidMemberIndex();
                currentTargetIndex = (validIdx != -1) ? validIdx : 0;
            }
        }
        
        private void ResolvePositionConflicts()
        {
            var party = PartyManager.Instance.partyData;
            if (party == null || party.Count == 0) return;

            RuntimeCharacterData[] slotAssignments = new RuntimeCharacterData[6];
            List<RuntimeCharacterData> pending = new List<RuntimeCharacterData>();

            foreach (var member in party)
            {
                int targetIndex = (member.row == RowType.Front ? 0 : 3) + (int)member.column;
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

        private int GetFirstValidMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp > 0);
        private int GetFirstDeadMemberIndex() => System.Array.FindIndex(partyControllers, p => !p.IsEmpty && p.currentHp <= 0);
    }
}