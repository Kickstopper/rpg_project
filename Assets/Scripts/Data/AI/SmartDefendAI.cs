using UnityEngine;
using System.Linq;
using Controller;
using Helper;
using UI.DungeonMapScene;

namespace Data.AI
{
    [CreateAssetMenu(menuName = "Monster AI/Smart Defend (신중형)")]
    public class SmartDefendAI : MonsterAIProfile
    {
        [Range(0, 1f)]
        public float defendHpThreshold = 0.3f; // 에디터에서 방어할 체력 비율

        public override BattleAction DecideAction(MonsterController self, BattleContext context)
        {
            // 내 체력이 30% 이하면 무조건 방어
            if (self.currentHp <= self.maxHp * defendHpThreshold)
            {
                int guardSpeed = self.sourceData.stats.agi + 2000;
                return new BattleAction(self.gameObject, self.gameObject, UI.ActionType.Guard, guardSpeed);
            }

            // 아니면 전열에 있는 플레이어만 우선 공격
            var frontPlayers = context.activePlayers.Where(p => p.currentHp > 0 && ((PlayerController)p).columnIndex < 3).ToList();
            
            // 전열이 다 죽었으면 후열 포함 전체에서 고름
            var targets = (frontPlayers.Count > 0) ? frontPlayers : context.activePlayers.Where(p => p.currentHp > 0).ToList();
            
            BattleEntity target = targets[Random.Range(0, targets.Count)];
            int speed = self.sourceData.stats.agi + Random.Range(0, 5);
            
            return new BattleAction(self.gameObject, target.gameObject, UI.ActionType.Attack, speed);
        }
    }
}