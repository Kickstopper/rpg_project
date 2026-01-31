using Data;
using UnityEngine;
namespace Helper
{
    public static class AlignmentSystem
    {
        // 성향을 좌표로 변환 (X: 질서~혼돈, Y: 선~악)
        // X: -1(Chaotic), 0(Neutral), 1(Lawful)
        // Y: -1(Evil), 0(Neutral), 1(Good)
        public static Vector2Int GetAxis(Align align)
        {
            switch (align)
            {
                case Align.Lawful_Good:    return new Vector2Int(1, 1);
                case Align.Neutral_Good:   return new Vector2Int(0, 1);
                case Align.Chaotic_Good:   return new Vector2Int(-1, 1);
                
                case Align.Lawful_Neutral: return new Vector2Int(1, 0);
                case Align.True_Neutral:   return new Vector2Int(0, 0);
                case Align.Chaotic_Neutral:return new Vector2Int(-1, 0);
                
                case Align.Lawful_Evil:    return new Vector2Int(1, -1);
                case Align.Neutral_Evil:   return new Vector2Int(0, -1);
                case Align.Chaotic_Evil:   return new Vector2Int(-1, -1);
                
                default: return Vector2Int.zero; // None
            }
        }

        // 두 성향 간의 시너지 점수 계산 (높을수록 좋음)
        // 1.0: 완벽 일치, 0.5: 한 축만 일치, 0.0: 완전 반대 등
        public static float CalculateSynergy(Align a, Align b)
        {
            if (a == Align.None || b == Align.None) return 0.5f; // 기본값

            Vector2Int posA = GetAxis(a);
            Vector2Int posB = GetAxis(b);

            // 거리 계산 (Manhattan Distance 방식이 RPG 직관에 맞음)
            // 거리가 0이면 일치, 1이면 인접, 2는 멈, 4는 정반대
            int dist = Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.y - posB.y);

            // 점수로 변환 (최대 거리 4)
            // 거리 0 -> 1.5배 (완벽)
            // 거리 1 -> 1.2배 (양호)
            // 거리 2 -> 1.0배 (보통)
            // 거리 3 -> 0.8배 (나쁨)
            // 거리 4 -> 0.5배 (최악)
            
            if (dist == 0) return 1.5f;
            if (dist == 1) return 1.2f;
            if (dist == 2) return 1.0f;
            if (dist == 3) return 0.8f;
            return 0.5f;
        }

        // 상성 데미지 계산 (공격자 vs 방어자)
        // 예: 선(Good)은 악(Evil)에게 더 강함
        public static float GetDamageModifier(Align attacker, Align defender)
        {
            Vector2Int att = GetAxis(attacker);
            Vector2Int def = GetAxis(defender);

            float modifier = 1.0f;

            // [상성 규칙 예시]
            // 1. 선 vs 악 : 서로 20% 추가 데미지
            if (att.y * def.y == -1) modifier += 0.2f; // 1 * -1 = -1 (서로 반대)

            // 2. 질서 vs 혼돈 : 서로 10% 추가 데미지
            if (att.x * def.x == -1) modifier += 0.1f;

            return modifier;
        }
    }
}