using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Helper;
using UI.DungeonMapScene;
using UI.Battle;

namespace Data.AI
{
    [CreateAssetMenu(menuName = "Monster AI/Basic Attack (가까운 적 우선 공격형)")]
    public class BasicAttackAI : MonsterAIProfile
    {
        public override BattleAction DecideAction(MonsterController self, BattleContext context)
        {
            // 살아있는 플레이어 목록 추출
            var livingPlayers = context.activePlayers.Where(p => p.currentHp > 0).ToList();
            if (livingPlayers.Count == 0) return null; // 공격할 대상이 없음

            // 1. 몬스터의 현재 위치를 숫자(좌표)로 변환
            int monsterRow = (self.currentRow == RowType.Front) ? 0 : 1;
            int monsterCol = (int)self.currentColumn;

            int minDistance = int.MaxValue;
            List<BattleEntity> closestPlayers = new();

            // 모든 아군과의 거리를 측정하여 가장 가까운 타겟들 수집
            foreach (var player in livingPlayers)
            {
                // 플레이어의 위치를 숫자(좌표)로 변환
                int playerRow = (player.columnIndex < 3) ? 0 : 1; // 0~2는 전열, 3~5는 후열
                int playerCol = player.columnIndex % 3;           // 0, 1, 2 (Left, Center, Right)

                // 거리 공식 계산. 마주보는 전열(0)끼리는 0칸, 전열-후열은 1칸, 후열(1)끼리는 가장 먼 2칸으로 계산
                int rowDist = monsterRow + playerRow; 
                int colDist = Mathf.Abs(monsterCol - playerCol);

                int totalDistance = rowDist + colDist;

                // 최단 거리 갱신
                if (totalDistance < minDistance)
                {
                    minDistance = totalDistance;
                    closestPlayers.Clear();
                    closestPlayers.Add(player);
                }

                // 거리가 같은 타겟이 여러 명일 경우 리스트에 추가 (랜덤 선택용)
                else if (totalDistance == minDistance)
                {
                    closestPlayers.Add(player);
                }
            }

            // 가장 가까운 타겟(들) 중 하나를 무작위로 선택하여 예측 불가능성 부여
            BattleEntity target = closestPlayers[Random.Range(0, closestPlayers.Count)];

            // 행동 생성 및 반환
            int speed = self.sourceData.stats.agi + Random.Range(0, 5);
            return new BattleAction(self.gameObject, target.gameObject, UI.ActionType.Attack, speed);
        }
        
    }
}