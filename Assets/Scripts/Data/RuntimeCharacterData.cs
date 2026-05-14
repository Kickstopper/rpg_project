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

    public enum StatType { STR, MAG, INT, VIT, AGI, LUC }

    public enum StatusEffect { None, Burn, Freeze, Shock, Dry, Wet, Confuse, Sleep, Blind, High, Poison, Paralyze, Curse }
    [System.Serializable]
    public class RuntimeCharacterData
    {
        // 원본 데이터 참조 (이름, 기본 스탯 등)
        public string characterId;
        public bool isRegular;

        public string name;
        public Race race;         // 인간
        public Gender gender;
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
        
        [System.NonSerialized] 
        public ExpTable expTable;
        
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
            if (System.Enum.TryParse(save.column, out ColumnType parsedCol)) column = parsedCol;

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
            isRegular = entry.isRegular;

            currentHp = maxHp = entry.maxHp;
            currentMp = maxMp = entry.maxMp;
            currentExp = 0;
            expTable = entry.expTable;

            row = RowType.Front;
            column = ColumnType.Center;

            equippedWeaponId = entry.initialWeaponId;
            equippedGunId = entry.initialGunId;
            equippedAmmoId = entry.initialAmmoId;
            equippedArmorIds = new List<string>(entry.initialArmorIds);
            
            var skillIds = entry.initialSkills.ConvertAll(s => s.id);
            learnedSkills = new List<string>(skillIds);
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
            data.column = this.column.ToString();
            
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
        public int GetMagicEffect() {return 0; }

        // UI나 전투 로직은 이 함수만 호출하면 됨
        public int GetRequiredExpForNextLevel() {
            if (expTable != null) {
                return expTable.GetRequiredExp(stats.level);
            }
            return 999999; // 안전장치
        }

        // 경험치 퍼센트 (UI용 헬퍼 함수)
        public float GetExpPercent() {
            int prevReq = (stats.level == 1) ? 0 : expTable.GetRequiredExp(stats.level - 1);
            int nextReq = GetRequiredExpForNextLevel();
            
            // 분모가 0이 되는 것을 방지
            if (nextReq - prevReq <= 0) return 0f;

            return (float)(currentExp - prevReq) / (nextReq - prevReq);
        }

        public RuntimeCharacterData() { }
    }
}