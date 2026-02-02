using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    public enum RowType { Front, Back }

    public enum ColumnType { Left, Center, Right }

    public enum Align { 
        None,
        Lawful_Good, Neutral_Good, Chaotic_Good,
        Lawful_Neutral, True_Neutral, Chaotic_Neutral,
        Lawful_Evil, Neutral_Evil, Chaotic_Evil 
    }

    public enum StatusEffect { Good, Asleep, Confused, Blind, Poison, Paralyzed, Adamism }
    [System.Serializable]
    public class RuntimeCharacterData
    {
        // 원본 데이터 참조 (이름, 기본 스탯 등)
        public string characterId;
        public bool isParty;

        public string name;

        public Align align;

        public StatusEffect statusEffect;

        public string spiritId;

        public StatData stats;
        public ResistanceData resistances;
        
        // 변하는 상태 값
        public int currentHp;
        public int maxHp;
        public int currentMp;
        public int maxMp;
        public int currentExp; // 현재 누적 경험치
        public int nextExp;

        public RowType row;
        public ColumnType column;
        
        // 장비 상태
        public string equippedWeaponId;
        public string equippedGunId;
        public string equippedAmmoId;

        public List<string> equippedArmorIds = new();
        
        // 배운 스킬
        public List<string> learnedSkills = new();
        public bool isCommander;

        public RuntimeCharacterData(CharacterSaveData save)
        {
            characterId = save.characterId;
            name = save.name;
            
            if (System.Enum.TryParse(save.align, out Align parsedAlign)) align = parsedAlign;
            if (System.Enum.TryParse(save.row, out RowType parsedRow)) row = parsedRow;

            spiritId = save.spiritId;

            stats = save.stats;
            resistances = save.resistances;
            isCommander = save.isCommander;

            currentHp = save.currentHp;
            currentMp = save.currentMp; 
            maxHp = save.maxHp;
            maxMp = save.maxMp;
            currentExp = save.exp;

            equippedWeaponId = save.weaponId;
            equippedGunId = save.gunId;
            equippedAmmoId = save.ammoId;
            learnedSkills = new List<string>(save.learnedSkillIds);
        }

        public RuntimeCharacterData(Database.CharacterDatabase.CharacterEntry entry)
        {
            characterId = entry.id;
            name = entry.name;
            align = entry.align;
            spiritId = entry.spiritId;
            stats = entry.stats;
            resistances = entry.resistances;
            isCommander = entry.isCommander;

            currentHp = maxHp = entry.maxHp;
            currentMp = maxMp = entry.maxMp;
            currentExp = 0;

            row = RowType.Front; 

            equippedWeaponId = entry.initialWeaponId;
            equippedGunId = entry.initialGunId;
            equippedAmmoId = entry.initialAmmoId;
            equippedArmorIds = new List<string>(entry.initialArmorIds);
            
            learnedSkills = new List<string>(entry.initialSkillIds);
        }

        public CharacterSaveData ToSaveData()
        {
            CharacterSaveData data = new();

            data.characterId = this.characterId;
            data.name = this.name;
            data.spiritId = this.spiritId;
            data.align = this.align.ToString();
            
            data.weaponId = this.equippedWeaponId;
            data.gunId = this.equippedGunId;
            data.ammoId = this.equippedAmmoId;
            data.armorIds = this.equippedArmorIds;
            
            data.maxHp = this.maxHp;
            data.maxMp = this.maxMp;
            data.currentHp = this.currentHp;
            data.currentMp = this.currentMp;
            
            data.exp = this.currentExp;
            
            data.row = this.row.ToString();
            
            data.resistances = this.resistances;
            data.stats = this.stats;
            data.learnedSkillIds = this.learnedSkills;
            
            return data;
        }

        public int GetTotalAttack(){ return 0;}
        public int GetHitRate(){ return 0;}
        public int GetGunAttack(){ return 0;}
        public int GetGunHitRate(){ return 0;}
        public int GetTotalDefense(){ return 0;}
        public int GetEvasion(){ return 0;}
        public int GetMagicPower(){ return 0;}

        public RuntimeCharacterData() { }
    }
}