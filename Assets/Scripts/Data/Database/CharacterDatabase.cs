using UnityEngine;
using System.Collections.Generic;
namespace Data.Database
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game Data/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [System.Serializable]
        public class CharacterEntry
        {
            public string id;
            public string name;
            public Race race;
            public Gender gender;

            public string resonanceId;

            public ExpTable expTable;
            public int exp;

            public bool isCommander;
            public bool isRegular;
            public bool isMonster;
            
            [Header("Visual")]
            public Sprite portraitImage;
            public Sprite battlePortraitImg;
            public Sprite standingImage;
            
            [Header("Default Attack VFX")]
            public VfxID basicAttackVfxId;

            [Header("Battle Stats")]
            public Align align;
            public StatData stats;
            public ResistanceData resistances;
            
            // ----------------------------------------------------------
            // 초기 세팅 및 아이템/스킬 참조 ID
            // ----------------------------------------------------------
            [Header("Initial Skills")]
            public List<SkillData> initialSkills = new(); // 이 캐릭터가 처음 생성될 때 가지고 있을 스킬 ID 목록
            [Header("Skill Tree")]
            public SkillTreeData skillTree; // 스킬 트리

            [Header("Initial Equipments")]
            // 초기 장비 ID (없으면 비워둠)
            public string initialWeaponId;
            public string initialGunId;
            public string initialAmmoId;
            public List<string> initialArmorIds = new List<string>(); // 투구, 갑옷 등 여러 개일 수 있으므로 리스트

        }

        public List<CharacterEntry> entries = new List<CharacterEntry>();

        // 검색 속도를 위해 Entry 자체를 저장하는 딕셔너리
        private Dictionary<string, CharacterEntry> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, CharacterEntry>();
            foreach (var entry in entries)
            {
                if (!lookupTable.ContainsKey(entry.id))
                {
                    lookupTable.Add(entry.id, entry);
                }
            }
        }

        public CharacterEntry GetEntry(string id)
        {
            if (lookupTable == null) Initialize();

            if (lookupTable.ContainsKey(id))
            {
                return lookupTable[id];
            }
            
            return null;
        }
    }
}
