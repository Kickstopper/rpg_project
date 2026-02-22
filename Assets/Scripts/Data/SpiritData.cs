using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Data
{
    [CreateAssetMenu(fileName = "Spirit", menuName = "RPG/Spirit")]
    public class SpiritData : ScriptableObject
    {
        public Sprite portraitImage;
        public string id;
        public string entityName;
        public Align align;
        
        public StatData stats;
        public ResistanceData resistances;
        public List<SkillData> skills = new List<SkillData>();

        private void Reset()
        {
            // 기본 스탯 초기화
            stats = new StatData()
            {
                level = 1,
                str = 3, mag = 3, intel = 3, vit = 3, agi = 3, luc = 3
            };
            resistances = new ResistanceData();
            align = Align.True_Neutral;

            // [자동 ID 생성 로직]
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:SpiritData");
            
            int nextCount = guids.Length; 

            // ID 포맷팅. 숫자를 3자리로 맞춤
            id = $"spirit_{nextCount:D3}";
            
            entityName = $"Spirit {nextCount}";
#endif
        }
    }
}