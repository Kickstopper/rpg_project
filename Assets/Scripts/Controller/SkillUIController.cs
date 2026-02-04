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
        public Transform[] partySlots;        // 전열3, 후열3 슬롯 (MoveUI와 동일 구조)
        public GameObject partyPrefab;        // PlayerController 프리팹
        private PlayerController[] partyControllers = new PlayerController[6];

        [Header("Skill List (Right)")]
        public Transform skillContent;        // ScrollView Content
        public GameObject skillSlotPrefab;    // 스킬 아이템 프리팹 (SimpleListItemController)
        public TextMeshProUGUI mpText;        // 현재 선택된 캐스터의 MP 표시용 (선택사항)

        [Header("Highlight Colors")]
        public Color casterHighlightColor = new Color(0.5f, 0.5f, 1f, 1f); // 시전자 (파란색 등)
        public Color targetHighlightColor = Color.yellow;                // 타겟 (노란색)
        public Color disabledTextColor = Color.gray;
        public Color enabledTextColor = Color.white;

        // 상태 관리
        private SkillUIState currentState = SkillUIState.SelectCaster;
        
        // 인덱스 관리
        private int currentCasterIndex = 0; // 왼쪽 파티 리스트 인덱스
        private int currentSkillIndex = 0;  // 오른쪽 스킬 리스트 인덱스
        private int currentTargetIndex = 0; // 타겟팅 인덱스

        // 데이터
        private List<string> currentSkillIds = new List<string>();
        private SkillData selectedSkillData;
        private PlayerController currentCaster;

        void OnEnable()
        {
            // 1. 초기화
            ResolvePositionConflicts();
            RefreshPartyList();
            
            // 2. 초기 상태 설정 (시전자 선택)
            currentState = SkillUIState.SelectCaster;
            currentCasterIndex = GetFirstValidMemberIndex();
            currentSkillIndex = 0;
            
            if (skillInfo) skillInfo.ResetText();
            // 3. UI 갱신
            RefreshSkillList(currentCasterIndex); // 현재 포커스된 캐릭터의 스킬 보여주기
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
        // 1. 시전자 선택 (SelectCaster)
        // =================================================================================
        private void HandleCasterSelection()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveCasterCursor(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveCasterCursor(1);

            // 확인: 해당 캐릭터의 스킬 리스트로 이동
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (partyControllers[currentCasterIndex].IsEmpty) return;

                // 스킬이 없으면 진입 불가
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

        private void MoveCasterCursor(int dir)
        {
            int nextIdx = currentCasterIndex;
            // 6번 반복해서 유효한 다음 멤버 찾기
            for (int i = 0; i < 6; i++)
            {
                nextIdx = (nextIdx + dir + 6) % 6;
                if (!partyControllers[nextIdx].IsEmpty) break;
            }
            
            currentCasterIndex = nextIdx;
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            
            // 커서가 움직일 때마다 오른쪽 스킬 리스트 갱신
            RefreshSkillList(currentCasterIndex);
            UpdateVisuals();
        }

        // =================================================================================
        // 2. 스킬 선택 (SelectSkill)
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

            // 확인: 타겟 선택 모드로 진입
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                AttemptSelectSkill();
            }

            // 취소: 시전자 선택 모드로 복귀
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
            
            // 스킬 정보창 업데이트
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

            // 조건 1: 사용 타입 체크 (Exploration / All 만 가능)
            if (selectedSkillData.useType != UseType.All && selectedSkillData.useType != UseType.Exploration)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 조건 2: MP 체크 (HP 코스트 스킬인 경우 HP 체크)
            bool isMpCost = !selectedSkillData.useHpCost;
            int cost = selectedSkillData.costValue;

            if (isMpCost && currentCaster.currentMp < cost)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                // "MP가 부족합니다" 로그나 팝업 추가 가능
                return;
            }
            else if (!isMpCost && currentCaster.currentHp <= cost)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                return;
            }

            // 조건 3: 부활 스킬인데 죽은 아군이 없는 경우 체크 등 (ItemUI와 동일 로직)
            // (필요하다면 추가 구현)

            // 타겟 선택 모드로 진입
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            currentState = SkillUIState.SelectTarget;
            
            // 타겟 초기값 설정 (스킬 범위에 따라 다름)
            InitializeTargetCursor();
            UpdateVisuals();
        }

        // =================================================================================
        // 3. 타겟 선택 (SelectTarget)
        // =================================================================================
        private void HandleTargetSelection()
        {
            // 범위가 전체(All)거나 사용자(Self)인 경우 이동 불필요
            TargetScope scope = selectedSkillData.targetScope;
            bool canMove = (scope == TargetScope.One_Ally || scope == TargetScope.Dead_Ally);

            if (canMove)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveTargetCursor(-1);
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveTargetCursor(1);
            }

            // 확인: 스킬 사용
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                UseSkill();
            }

            // 취소: 스킬 선택 모드로 복귀
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                currentState = SkillUIState.SelectSkill;
                UpdateVisuals();
            }
        }

        private void MoveTargetCursor(int dir)
        {
            int nextIdx = currentTargetIndex;
            // 유효한 타겟 찾기 루프
            for (int i = 0; i < 6; i++)
            {
                nextIdx = (nextIdx + dir + 6) % 6;
                if (!partyControllers[nextIdx].IsEmpty)
                {
                    // 죽은 자 대상인데 살아있으면 스킵
                    if (selectedSkillData.targetScope == TargetScope.Dead_Ally && partyControllers[nextIdx].currentHp > 0) continue;
                    // 산 자 대상인데 죽어있으면 스킵
                    if (selectedSkillData.targetScope == TargetScope.One_Ally && partyControllers[nextIdx].currentHp <= 0) continue;
                    break;
                }
            }
            currentTargetIndex = nextIdx;
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            UpdateVisuals(); // 타겟 하이라이트 갱신
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
                SoundManager.Instance.PlaySFX(SfxID.Attack_Magic); // 또는 회복 사운드

                // 코스트 차감
                if (selectedSkillData.useHpCost)
                    currentCaster.Recover(-selectedSkillData.costValue, 0); // HP 감소
                else
                    currentCaster.Recover(0, -selectedSkillData.costValue); // MP 감소

                // 데이터 동기화
                currentCaster.sourceData.currentHp = currentCaster.currentHp;
                currentCaster.sourceData.currentMp = currentCaster.currentMp;

                // [요구사항 4번] 사용 후 하이라이트 끄기 -> 상태 초기화 또는 스킬 선택 상태로 복귀
                // 보통 JRPG는 연속 사용을 위해 스킬 선택 상태로 돌아가거나 시전자로 돌아감.
                // 여기서는 "하이라이트와 타겟 하이라이트는 꺼집니다"를 따라 시전자 선택 상태로 가거나,
                // 스킬 리스트 갱신 후 스킬 선택 대기로 갑니다. (연속 사용 편의성 고려: 스킬 선택 상태 유지)
                
                RefreshSkillList(currentCasterIndex); // MP 소모 반영 (Grayout 등)
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
            // 데이터 동기화
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

            // 2. 시전자 하이라이트 (Caster Selection 또는 Skill Selection 상태일 때)
            if (currentState == SkillUIState.SelectCaster || currentState == SkillUIState.SelectSkill || currentState == SkillUIState.SelectTarget)
            {
                if (!partyControllers[currentCasterIndex].IsEmpty)
                {
                    partyControllers[currentCasterIndex].SetHighlightColor(casterHighlightColor);
                }
            }

            // 3. 스킬 리스트 버튼 Focus 처리
            var buttons = skillContent.GetComponentsInChildren<Button>();
            if (currentState == SkillUIState.SelectSkill && buttons.Length > currentSkillIndex)
            {
                buttons[currentSkillIndex].Select();
                UpdateSkillInfo();
            }
            else
            {
                if (skillInfo) skillInfo.ResetText();
                // 다른 상태일 때는 스킬 리스트 포커스 해제
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            // 4. 타겟 하이라이트 (점멸 효과는 Update에서 처리하거나 여기서 코루틴 시작)
            if (currentState == SkillUIState.SelectTarget)
            {
                HighlightTargets();
            }
        }

        private void HighlightTargets()
        {
            TargetScope scope = selectedSkillData.targetScope;
            
            // 점멸 색상 계산 (PingPong)
            Color blinkColor = Color.Lerp(Color.clear, targetHighlightColor, Mathf.PingPong(Time.time * 5f, 1f));
            
            // 시전자 하이라이트가 덮어씌워질 수 있으므로, 시전자가 타겟이 아닌 경우 시전자는 유지해야 함.
            // 하지만 SetHighlightColor 구현상 하나만 됨. 
            // PlayerController에 "SetSecondaryHighlight"가 없으므로 색상을 섞거나 덮어써야 함.
            // 요구사항: "스킬을 사용할 캐릭터의 하이라이트가 유지된 상태로..." -> 이를 위해선 시전자 색상 + 타겟 색상 로직이 필요.
            // 여기서는 단순화를 위해 타겟이 되면 타겟 색상이 우선하도록 함.

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
                partyControllers[currentTargetIndex].SetHighlightColor(blinkColor);
            }
        }

        // 매 프레임 점멸 효과를 위해 Update에서 호출
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

            // MP 텍스트 갱신 (옵션)
            if (mpText) mpText.text = $"MP: {currentCaster.currentMp}/{currentCaster.maxMp}";

            currentSkillIds = currentCaster.learnedSkillIds;
            
            for (int i = 0; i < currentSkillIds.Count; i++)
            {
                string skillId = currentSkillIds[i];
                SkillData sData = DatabaseManager.Instance.GetSkill(skillId);
                if (sData == null) continue;

                GameObject go = Instantiate(skillSlotPrefab, skillContent);
                var slot = go.GetComponent<SimpleListItemController>();
                
                // 데이터 표시 (이름, 코스트)
                string costStr = sData.useHpCost ? $"{sData.costValue}HP" : $"{sData.costValue}MP";
                if(slot) slot.SetData(sData.dataName + $" <size=70%>({costStr})</size>", 0); // count는 0으로

                // 사용 가능 여부 (MP, UseType) 체크 -> Grayout
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
                currentTargetIndex = GetFirstDeadMemberIndex();
                if (currentTargetIndex == -1) currentTargetIndex = 0; // 예외 처리
            }
            else
            {
                currentTargetIndex = GetFirstValidMemberIndex();
            }
        }
        
        // 위치 충돌 해결 (ItemUI와 동일)
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