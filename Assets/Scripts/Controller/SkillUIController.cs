using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using Data;
using TMPro;

namespace Controller
{
    public class SkillUIController : MonoBehaviour
    {
        public enum SkillUIState { SelectCaster, SelectSkill, SelectTarget }

        public PlayerMenuController menuController;
        
        [Header("Skill Info (Center)")]
        public SkillInfoController skillInfo;

        [Header("Party List (Left)")]
        public Transform[] partySlots;        
        public GameObject partyPrefab;        
        private PlayerController[] partyControllers = new PlayerController[6];

        [Header("Skill List (Right)")]
        public Transform skillContent;        
        public GameObject skillSlotPrefab;    
        public TextMeshProUGUI mpText;        

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

        // =================================================================================
        // 1. 시전자 선택 (SelectCaster) - 3x2 그리드 방식 적용
        // =================================================================================
        private void HandleCasterSelection()
        {
            bool moved = false;

            // [좌/우] 인덱스 +/- 1 (행 내부 이동)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                if (currentCasterIndex % 3 > 0) 
                { 
                    currentCasterIndex--; 
                    moved = true; 
                }
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                if (currentCasterIndex % 3 < 2) 
                { 
                    currentCasterIndex++; 
                    moved = true; 
                }
            }
            // [상/하] 인덱스 +/- 3 (열 간 이동)
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                if (currentCasterIndex >= 3) 
                { 
                    currentCasterIndex -= 3; 
                    moved = true; 
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (currentCasterIndex < 3) 
                { 
                    currentCasterIndex += 3; 
                    moved = true; 
                }
            }

            if (moved)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                RefreshSkillList(currentCasterIndex); // 커서 이동 시 스킬 리스트 갱신
                UpdateVisuals();
            }

            // 확인: 해당 캐릭터의 스킬 리스트로 이동
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // 빈 슬롯이면 진행 불가
                if (partyControllers[currentCasterIndex].IsEmpty) 
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    return;
                }

                // 스킬이 없으면 진행 불가
                if (currentSkillIds.Count == 0)
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    return;
                }

                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                currentState = SkillUIState.SelectSkill;
                currentSkillIndex = 0;
                UpdateVisuals();
            }

            // 취소: 메뉴 닫기
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                menuController.CloseSkillUI();
            }
        }

        // =================================================================================
        // 2. 스킬 선택 (SelectSkill) - 기존 수직 리스트 유지
        // =================================================================================
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

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                AttemptSelectSkill();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
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
            if (skillInfo == null) return;
            string skillId = currentSkillIds[currentSkillIndex];
            SkillData skillData = DatabaseManager.Instance.GetSkill(skillId);
            skillInfo.UpdateInfo(skillData);
        }

        private void AttemptSelectSkill()
        {
            string skillId = currentSkillIds[currentSkillIndex];
            selectedSkillData = DatabaseManager.Instance.GetSkill(skillId);
            currentCaster = partyControllers[currentCasterIndex];

            if (selectedSkillData == null) return;  

            // 조건 1: 사용 타입 체크
            if (selectedSkillData.useType != UseType.All && selectedSkillData.useType != UseType.Exploration)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 조건 2: Cost 체크
            bool isMpCost = !selectedSkillData.useHpCost;
            int cost = selectedSkillData.costValue;

            if (isMpCost && currentCaster.currentMp < cost)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }
            else if (!isMpCost && currentCaster.currentHp <= cost)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 조건 3: 부활 스킬 체크 (죽은 아군 없음)
            if (selectedSkillData.targetScope == TargetScope.Dead_Ally || selectedSkillData.targetScope == TargetScope.All_Dead_Allies)
            {
                if (GetFirstDeadMemberIndex() == -1)
                {
                    menuController.ShowAlertPopup("되살릴 대상이 없습니다.");
                    return;
                }
            }

            // 타겟 선택 모드 진입
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            currentState = SkillUIState.SelectTarget;
            
            InitializeTargetCursor();
            UpdateVisuals();
        }

        // =================================================================================
        // 3. 타겟 선택 (SelectTarget) - 3x2 그리드 방식 적용
        // =================================================================================
        private void HandleTargetSelection()
        {
            TargetScope scope = selectedSkillData.targetScope;
            bool canMove = (scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally);

            if (canMove)
            {
                bool moved = false;

                // [좌/우]
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    if (currentTargetIndex % 3 > 0) 
                    { 
                        currentTargetIndex--; 
                        moved = true; 
                    }
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    if (currentTargetIndex % 3 < 2) 
                    { 
                        currentTargetIndex++; 
                        moved = true; 
                    }
                }
                // [상/하]
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    if (currentTargetIndex >= 3) 
                    { 
                        currentTargetIndex -= 3; 
                        moved = true; 
                    }
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    if (currentTargetIndex < 3) 
                    { 
                        currentTargetIndex += 3; 
                        moved = true; 
                    }
                }

                if (moved)
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                    UpdateVisuals();
                }
            }

            // 확인: 스킬 사용 시도
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // 타겟 유효성 검사 (빈 슬롯, 이미 죽은/산 대상 등)
                if (IsValidTarget(currentTargetIndex))
                {
                    UseSkill();
                }
                else
                {
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                }
            }

            // 취소
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                currentState = SkillUIState.SelectSkill;
                UpdateVisuals();
            }
        }

        // 선택한 타겟 인덱스가 유효한지 검사
        private bool IsValidTarget(int index)
        {
            if (partyControllers[index].IsEmpty) return false;

            TargetScope scope = selectedSkillData.targetScope;
            PlayerController target = partyControllers[index];

            // 부활 스킬인데 대상이 살아있음
            if ((scope == TargetScope.Dead_Ally) && target.currentHp > 0) return false;
            
            // 회복/버프 스킬인데 대상이 죽어있음
            if ((scope == TargetScope.One_Ally) && target.currentHp <= 0) return false;

            return true;
        }

        private void UseSkill()
        {
            bool success = false;
            TargetScope scope = selectedSkillData.targetScope;

            // 1. 효과 적용
            if (scope == TargetScope.All_Allies)
            {
                foreach (var pc in partyControllers) if (!pc.IsEmpty) ApplyEffect(pc);
                success = true;
            }
            else if (scope == TargetScope.Self)
            {
                ApplyEffect(currentCaster);
                success = true;
            }
            else if (scope == TargetScope.All_Dead_Allies)
            {
                foreach (var pc in partyControllers) if (!pc.IsEmpty && pc.currentHp <= 0) ApplyEffect(pc);
                success = true;
            }
            else // 단일 타겟
            {
                if (!partyControllers[currentTargetIndex].IsEmpty)
                {
                    ApplyEffect(partyControllers[currentTargetIndex]);
                    success = true;
                }
            }

            // 2. 비용 차감 및 UI 갱신
            if (success)
            {
                SoundManager.Instance.PlaySFX(SfxID.Attack_Magic); 

                if (selectedSkillData.useHpCost)
                    currentCaster.Recover(-selectedSkillData.costValue, 0);
                else
                    currentCaster.Recover(0, -selectedSkillData.costValue);

                currentCaster.sourceData.currentHp = currentCaster.currentHp;
                currentCaster.sourceData.currentMp = currentCaster.currentMp;

                RefreshSkillList(currentCasterIndex); 
                currentState = SkillUIState.SelectSkill; 
                UpdateVisuals();
            }
        }

        private void ApplyEffect(PlayerController target)
        {
            int val = selectedSkillData.effectValue;
            switch(selectedSkillData.effectType)
            {
                case EffectType.Recover_HP: 
                    target.Recover(val, 0); 
                    break;
                case EffectType.Recover_MP: 
                    target.Recover(0, val); 
                    break;
                case EffectType.Revive_Empty:
                case EffectType.Revive_Fully:
                    if(target.currentHp <= 0) {
                        target.Revive(val);
                        target.sourceData.currentHp = target.currentHp;
                    }
                    break;
            }
            target.sourceData.currentHp = target.currentHp;
            target.sourceData.currentMp = target.currentMp;
        }

        // =================================================================================
        // 시각적 처리 및 헬퍼 함수
        // =================================================================================
        private void UpdateVisuals()
        {
            // 1. 모든 하이라이트 초기화
            foreach (var pc in partyControllers) pc.ResetHighlightColor();

            // 2. 시전자 하이라이트
            if (currentState == SkillUIState.SelectCaster || currentState == SkillUIState.SelectSkill || currentState == SkillUIState.SelectTarget)
            {
                if (!partyControllers[currentCasterIndex].IsEmpty)
                {
                    partyControllers[currentCasterIndex].SetHighlightColor(casterHighlightColor);
                }
            }

            // 3. 스킬 리스트 버튼 Focus
            var buttons = skillContent.GetComponentsInChildren<Button>();
            if (currentState == SkillUIState.SelectSkill && buttons.Length > currentSkillIndex)
            {
                buttons[currentSkillIndex].Select();
                UpdateSkillInfo();
            }
            else
            {
                if (skillInfo) skillInfo.ResetText();
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            // 4. 타겟 하이라이트
            if (currentState == SkillUIState.SelectTarget)
            {
                HighlightTargets();
            }
        }

        private void HighlightTargets()
        {
            TargetScope scope = selectedSkillData.targetScope;
            Color blinkColor = Color.Lerp(Color.clear, targetHighlightColor, Mathf.PingPong(Time.time * 5f, 1f));

            if (scope == TargetScope.All_Allies)
            {
                foreach (var pc in partyControllers) 
                    if (!pc.IsEmpty) pc.SetHighlightColor(blinkColor);
            }
            else if (scope == TargetScope.All_Dead_Allies)
            {
                foreach (var pc in partyControllers) 
                    if (!pc.IsEmpty && pc.currentHp <= 0) pc.SetHighlightColor(blinkColor);
            }
            else if (scope == TargetScope.Self)
            {
                partyControllers[currentCasterIndex].SetHighlightColor(blinkColor);
            }
            else // 단일 타겟
            {
                // 현재 커서가 가리키는 대상 점멸 (빈 슬롯도 커서가 가면 점멸은 하되 선택 불가)
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
                var slot = go.GetComponent<SimpleListItemController>();
                
                string costStr = sData.useHpCost ? $"{sData.costValue}HP" : $"{sData.costValue}MP";
                if(slot) slot.SetData(sData.dataName + $" <size=70%>({costStr})</size>", 0);

                bool isUsableType = (sData.useType == UseType.All || sData.useType == UseType.Exploration);
                bool hasResource = sData.useHpCost ? (currentCaster.currentHp > sData.costValue) : (currentCaster.currentMp >= sData.costValue);
                bool isUsable = isUsableType && hasResource;

                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                foreach(var t in texts) t.color = isUsable ? enabledTextColor : disabledTextColor;
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

                if (member != null) partyControllers[i].Initialize(member, null);
                else partyControllers[i].InitializeEmpty(i);
            }
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
                // 죽은 아군이 있으면 그곳으로, 없으면 0번 (어차피 UseSkill에서 막힘)
                int deadIdx = GetFirstDeadMemberIndex();
                currentTargetIndex = (deadIdx != -1) ? deadIdx : 0;
            }
            else
            {
                // 기본적으로 0번(전열 왼쪽) 혹은 첫 번째 유효 멤버
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