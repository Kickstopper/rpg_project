using System.Linq;
using Helper;
using UI.Battle;
using UI.DungeonMapScene;
using UnityEngine;

namespace Data.AI
{
    [CreateAssetMenu(menuName = "Monster AI/Smart Judge (점수 기반 지능형)")]
    public class SmartJudgeAI : MonsterAIProfile
    {
        public override BattleAction DecideAction(MonsterController self, BattleContext context)
        {
            // 전장 상황 파악
            var livingPlayers = context.activePlayers.Where(p => p != null && p.currentHp > 0).ToList();
            var livingMonsters = context.activeMonsters.Where(m => m != null && m.currentHp > 0).ToList();
            
            bool isSilenced = self.activeEffects.Exists(e => e.data.restrictionType == RestrictionType.Silence);

            // 점수 평가용 변수
            BattleAction bestAction = null;
            int highestScore = -1;

            // 1차 체크. 기본 공격
            if (livingPlayers.Count > 0)
            {
                var target = livingPlayers[Random.Range(0, livingPlayers.Count)];
                var action = new BattleAction(self.gameObject, target.gameObject, UI.ActionType.Attack, self.GetTotalAgi());
                
                // 기본 공격은 항상 50점의 기본 가치를 가짐 (약간의 랜덤성을 더해 예측 불가능하게 만듦)
                int score = 50 + Random.Range(0, 10); 
                
                UpdateBestAction(action, score, ref bestAction, ref highestScore);
            }

            // 2차 체크. 체력이 25% 이하인 만만한 플레이어가 있는지 확인해 토도메
            var weakPlayer = livingPlayers.OrderBy(p => (float)p.currentHp / p.maxHp).FirstOrDefault();
            if (weakPlayer != null && (float)weakPlayer.currentHp / weakPlayer.maxHp <= 0.25f)
            {
                var action = new BattleAction(self.gameObject, weakPlayer.gameObject, UI.ActionType.Attack, self.GetTotalAgi() + 5);
                
                // 마무리를 지을 수 있다면 기본 공격보다 훨씬 높은 120점 부여
                int score = 120 + Random.Range(0, 10);
                
                UpdateBestAction(action, score, ref bestAction, ref highestScore);
            }

            // 이하 스킬 사용 평가는 침묵 상태가 아닐 때만 계산
            if (!isSilenced)
            {
                // 3차 체크. 회복
                var dyingAlly = livingMonsters.OrderBy(m => (float)m.currentHp / m.maxHp).FirstOrDefault();
                if (dyingAlly != null && (float)dyingAlly.currentHp / dyingAlly.maxHp <= 0.4f)
                {
                    SkillData healSkill = self.sourceData.skills.FirstOrDefault(s => s.effectType == EffectType.Recover_HP && s.costValue <= self.CurrentMp);
                    
                    if (healSkill != null)
                    {
                        var action = new BattleAction(self.gameObject, dyingAlly.gameObject, UI.ActionType.Skill, self.GetTotalAgi() + 10);
                        action.actionData = healSkill;
                        
                        // 체력이 40% 이하면 100점, 20% 이하면 150점으로 상황이 급할수록 점수가 급상승
                        int score = 100;
                        if ((float)dyingAlly.currentHp / dyingAlly.maxHp <= 0.2f) score = 150;
                        
                        UpdateBestAction(action, score, ref bestAction, ref highestScore);
                    }
                }

                // 4차 체크. 광역 공격
                SkillData aoeSkill = self.sourceData.skills.FirstOrDefault(s => s.targetScope == TargetScope.All_Enemies && s.costValue <= self.CurrentMp);
                if (aoeSkill != null)
                {
                    var action = new BattleAction(self.gameObject, self.gameObject, UI.ActionType.Skill, self.GetTotalAgi());
                    action.actionData = aoeSkill;
                    
                    // 살아있는 플레이어가 많을수록 광역기의 효율(점수)이 올라감
                    int score = 40 + (livingPlayers.Count * 20) + Random.Range(0, 10);
                    // 예: 4명 살아있으면 40 + 80 = 120점. 평타(50점)보다 압도적으로 우선순위가 높아짐
                    UpdateBestAction(action, score, ref bestAction, ref highestScore);
                }
            }

            // 가장 점수가 높은 행동 반환. 만약 모두 실패했다면 방어
            if (bestAction != null)
            {
                return bestAction;
            }

            // 폴백 설정
            return new BattleAction(self.gameObject, self.gameObject, UI.ActionType.Guard, self.GetTotalAgi() + 2000);
        }

        // 점수 비교 및 갱신용
        private void UpdateBestAction(BattleAction newAction, int newScore, ref BattleAction bestAction, ref int highestScore)
        {
            if (newScore > highestScore)
            {
                highestScore = newScore;
                bestAction = newAction;
            }
        }
    }
}