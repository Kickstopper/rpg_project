using UnityEngine;
using System.Linq;
using Controller;
using Helper;
using UI.DungeonMapScene;

namespace Data.AI
{
    [CreateAssetMenu(menuName = "Monster AI/Basic Attack (기본 공격형)")]
    public class BasicAttackAI : MonsterAIProfile
    {
        public override BattleAction DecideAction(MonsterController self, BattleContext context)
        {
            // 살아있는 플레이어 목록 추출
            var livingPlayers = context.activePlayers.Where(p => p.currentHp > 0).ToList();
            if (livingPlayers.Count == 0) return null; // 공격할 대상이 없음

            // 랜덤 타겟 선정
            BattleEntity target = livingPlayers[Random.Range(0, livingPlayers.Count)];

            // 행동 생성 및 반환
            int speed = self.sourceData.stats.agi + Random.Range(0, 5);
            return new BattleAction(self.gameObject, target.gameObject, UI.ActionType.Attack, speed);
        }
    }
}