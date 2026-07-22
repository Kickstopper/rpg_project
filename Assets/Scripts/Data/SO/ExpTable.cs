using UnityEngine;
namespace Data
{
    // 프로젝트 창에서 우클릭 -> Create -> RPG -> ExpTable 로 생성 가능
    [CreateAssetMenu(fileName = "New Exp Table", menuName = "RPG/Exp Table")]
    public class ExpTable : ScriptableObject
    {
        [Header("설정")]
        public int maxLevel = 99;
        public float baseExp = 100f;
        [Range(1f, 5f)] public float exponent = 2.2f;

        // 이 함수는 어디서든 이 에셋을 참조해서 호출 가능
        public int GetRequiredExp(int level)
        {
            if (level >= maxLevel) return 0;
            return Mathf.FloorToInt(baseExp * Mathf.Pow(level, exponent));
        }
    }
}
