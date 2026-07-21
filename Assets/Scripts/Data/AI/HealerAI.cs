using System.Collections.Generic;
using System.Linq; 
using Helper;
using UI.Battle;
using UI.DungeonMapScene;
using UnityEngine;

namespace Data.AI
{
    [CreateAssetMenu(menuName = "Monster AI/Healer (회복형)")]
    public class HealerAI : MonsterAIProfile
    {
        public override BattleAction DecideAction(MonsterController self, BattleContext context)
        {
            // 상태이상 확인 (침묵)
            bool isSilenced = self.activeEffects.Exists(e => e.data.restrictionType == RestrictionType.Silence);

            if (!isSilenced)
            {
                // 정렬은 그대로 유지하여 0번 인덱스에 가장 위급한 아군이 오게 함.
                var dyingAllies = context.activeMonsters
                    .Where(m => m != null && m.currentHp > 0 && m.currentHp < m.maxHp * 0.5f)
                    .OrderBy(m => (float)m.currentHp / m.maxHp)
                    .ToList();

                if (dyingAllies.Count > 0)
                {
                    // 모든 힐 스킬 필터링
                    List<SkillData> availableHeals = self.sourceData.skills
                        .Where(skill => skill.effectType == EffectType.Recover_HP && skill.costValue <= self.CurrentMp)
                        .ToList();

                    if (availableHeals.Count > 0)
                    {
                        SkillData selectedSkill = null;
                        GameObject targetObj = null;

                        // 위급한 아군이 2명 이상일 경우, 광역 힐 우선 검색
                        if (dyingAllies.Count >= 2)
                        {
                            var aoeHeals = availableHeals.Where(s => s.targetScope == TargetScope.All_Allies).ToList();
                            if (aoeHeals.Count > 0)
                            {
                                selectedSkill = aoeHeals[Random.Range(0, aoeHeals.Count)];
                                targetObj = self.gameObject; // 광역 스킬은 시전자 자신이나 파티원 아무나 타겟으로 잡아도 무방함
                            }
                        }

                        // 위급한 아군이 1명이거나, 광역 힐 스킬이 없는 경우 단일 힐 타겟팅
                        if (selectedSkill == null)
                        {
                            // 가급적 단일 힐 스킬을 찾고, 정 없다면 아무 힐 스킬이나 고름
                            var singleHeals = availableHeals.Where(s => s.targetScope == TargetScope.One_Ally).ToList();
                            selectedSkill = singleHeals.Count > 0 
                                ? singleHeals[Random.Range(0, singleHeals.Count)] 
                                : availableHeals[Random.Range(0, availableHeals.Count)];
                            
                            // 타겟은 정렬해둔 리스트의 가장 체력 비율이 낮은 몬스터
                            targetObj = dyingAllies[0].gameObject; 
                        }

                        // 액션 생성 및 반환
                        var action = new BattleAction(self.gameObject, targetObj, UI.ActionType.Skill, self.GetTotalAgi() + 10);
                        action.actionData = selectedSkill;
                        return action;
                    }
                }
            }

            // 힐을 할 수 없는 경우 (침묵, 대상 없음, MP 부족 등) 평타 공격
            var livingPlayers = context.activePlayers.Where(p => p != null && p.currentHp > 0).ToList();
            
            if (livingPlayers.Count > 0)
            {
                var target = livingPlayers[Random.Range(0, livingPlayers.Count)];
                return new BattleAction(self.gameObject, target.gameObject, UI.ActionType.Attack, self.GetTotalAgi());
            }

            // 공격할 플레이어마저 없는 경우 방어 
            return new BattleAction(self.gameObject, self.gameObject, UI.ActionType.Guard, self.GetTotalAgi() + 2000);
        }
    }
}