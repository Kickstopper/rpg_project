using System.Collections.Generic;
using Data.Database;

namespace Data
{
    [System.Serializable]
    public class RuntimeCharacterData
    {
        // 원본 데이터 참조 (이름, 기본 스탯 등)
        public string characterId;

        public string name;

        public Align align;

        public StatData stats;
        public ResistanceData resistances;
        
        // 변하는 상태 값
        public int currentHp;
        public int maxHp;
        public int currentMp;
        public int maxMp;
        public int level;
        public int currentExp; // 현재 누적 경험치

        public string row;
        
        // 장비 상태
        public string equippedWeaponId;
        public string equippedGunId;
        public string equippedAmmoId;

        public List<string> equippedArmorIds = new();
        
        // 배운 스킬
        public List<string> learnedSkills = new();
        public bool isCommander;

        public RuntimeCharacterData(CharacterDatabase.CharacterEntry entry)
        {
            characterId = entry.id;
            name = entry.name;
            align = entry.align;
            stats = entry.stats;
            resistances = entry.resistances;
            isCommander = entry.isCommander;

            level = entry.stats.level;
            currentHp = maxHp = entry.maxHp;
            currentMp = maxMp = entry.maxMp;
            currentExp = 0;

            equippedWeaponId = entry.initialWeaponId;
            equippedGunId = entry.initialGunId;
            equippedAmmoId = entry.initialAmmoId;
            learnedSkills = new List<string>(entry.initialSkillIds);
        }

        public CharacterSaveData ToSaveData()
        {
            CharacterSaveData data = new();

            data.characterId = this.characterId;
            data.name = this.name;
            data.level = this.level;
            data.align = this.align.ToString();
            
            data.learnedSkillIds = this.learnedSkills;
            
            data.weaponId = this.equippedWeaponId;
            data.gunId = this.equippedGunId;
            data.ammoId = this.equippedAmmoId;
            data.armorIds = this.equippedArmorIds;
            data.currentHp = this.currentHp;
            data.maxHp = this.maxHp;
            data.maxMp = this.maxMp;
            data.currentMp = this.currentMp;
            data.exp = this.currentExp;
            data.row = this.row;

            return data;
        }

        public RuntimeCharacterData() { }
    }
}