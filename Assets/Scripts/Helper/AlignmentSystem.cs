using Data;
using UnityEngine;

namespace Helper
{
    public static class AlignmentSystem
    {
        // 기존 함수: 성향 -> 좌표 변환
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

        // 좌표 -> 성향 변환 (GetAxis의 역함수)
        public static Align GetAlignFromAxis(Vector2Int axis)
        {
            // 범위를 -1 ~ 1로 안전하게 고정
            int x = Mathf.Clamp(axis.x, -1, 1);
            int y = Mathf.Clamp(axis.y, -1, 1);

            // X축 (질서-혼돈) 판별
            if (x == 1) // Lawful
            {
                if (y == 1) return Align.Lawful_Good;
                if (y == 0) return Align.Lawful_Neutral;
                return Align.Lawful_Evil; // y == -1
            }
            else if (x == -1) // Chaotic
            {
                if (y == 1) return Align.Chaotic_Good;
                if (y == 0) return Align.Chaotic_Neutral;
                return Align.Chaotic_Evil; // y == -1
            }
            else // Neutral (x == 0)
            {
                if (y == 1) return Align.Neutral_Good;
                if (y == -1) return Align.Neutral_Evil;
                return Align.True_Neutral; // y == 0
            }
        }

        // 두 성향의 평균값 계산
        public static Align GetAverageAlign(Align a, Align b)
        {
            if (a == Align.None) return b;
            if (b == Align.None) return a;

            Vector2Int posA = GetAxis(a);
            Vector2Int posB = GetAxis(b);

            // 좌표 평균 계산 (반올림하여 가장 가까운 성향으로 매핑)
            int avgX = Mathf.RoundToInt((posA.x + posB.x) / 2f);
            int avgY = Mathf.RoundToInt((posA.y + posB.y) / 2f);

            return GetAlignFromAxis(new Vector2Int(avgX, avgY));
        }

        // 두 성향 간의 시너지 점수 계산 (높을수록 좋음)
        public static float CalculateSynergy(Align a, Align b)
        {
            if (a == Align.None || b == Align.None) return 0.5f;

            Vector2Int posA = GetAxis(a);
            Vector2Int posB = GetAxis(b);

            int dist = Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.y - posB.y);
            
            if (dist == 0) return 1.5f;
            if (dist == 1) return 1.2f;
            if (dist == 2) return 1.0f;
            if (dist == 3) return 0.8f;
            return 0.5f;
        }

        public static float GetDamageModifier(Align attacker, Align defender)
        {
            Vector2Int att = GetAxis(attacker);
            Vector2Int def = GetAxis(defender);

            float modifier = 1.0f;
            if (att.y * def.y == -1) modifier += 0.2f; 
            if (att.x * def.x == -1) modifier += 0.1f;

            return modifier;
        }

        public static string GetAlignString(Align align)
        {
            switch(align)
            {
                case Align.Chaotic_Evil:
                    return "C/E";
                case Align.Chaotic_Neutral:
                    return "C/N";
                case Align.Chaotic_Good:
                    return "C/G";

                case Align.Lawful_Evil:
                    return "L/E";
                case Align.Lawful_Neutral:
                    return "L/N";
                case Align.Lawful_Good:
                    return "L/G";

                case Align.Neutral_Evil:
                    return "N/E";
                case Align.True_Neutral:
                    return "T.N.";
                case Align.Neutral_Good:
                    return "N/G";
                
                default:
                    return "None";
            }
        }
    }
}