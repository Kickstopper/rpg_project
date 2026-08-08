using System.Collections.Generic;
using Helper;
using Manager;
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

    public class PartyID 
    { 
        public const string CHARACTER_00 = "chr_00";
        public const string CHARACTER_01 = "chr_01";
        public const string CHARACTER_02 = "chr_02";
        public const string CHARACTER_03 = "chr_03";
        public const string CHARACTER_04 = "chr_04";
        public const string CHARACTER_05 = "chr_05";
        public const string CHARACTER_06 = "chr_06"; 
    }

    [System.Serializable]
    public class RuntimeCharacterData
    {
        // 원본 데이터 참조 (이름, 기본 스탯 등)
        public string characterId;
        public int workedDays;
        public bool isRegular; // 전투에 참여하는 멤버인지 아닌지
        public bool isCommander; // ITEM을 사용할 수 있고 죽었을 경우 게임 오버 되는 멤버인지
        public bool isMonster;

        public string name;
        public Race race;         // 인간
        public Gender gender;
        public Align align;

        public string resonanceId;

        public StatData stats;
        public ResistanceData resistances;
        
        // 변하는 상태 값
        public int currentHp;
        public int maxHp;
        public int currentMp;
        public int maxMp;
        public int currentExp; // 현재 누적 경험치
        
        public RowType row;
        public ColumnType column;
        
        // 장비 상태
        public string equippedWeaponId;
        public string equippedGunId;
        public string equippedAmmoId;

        public VfxID basicAttackVfxId;    // 기본 공격 이펙트 ID

        public List<string> equippedArmorIds = new();

        // 이미 보상을 획득하여 완료 처리된 노드의 ID 목록 (이중 수령 방지용)
        public List<string> claimedSkillNodes = new List<string>(); 
        
        // 습득한 스킬 목록
        public List<string> learnedSkills = new();
        
        // 전투 외에서도 유지되는 상태이상의 ID     
        public StatusEffectID persistentStatusId; 
        // UI나 필드 로직에서 쉽게 데이터에 접근하기 위한 프로퍼티
        public StatusEffectData CurrentStatusEffect
        {
            get {
                if (persistentStatusId == StatusEffectID.None) return null;
                return ManagerRoot.Database.GetStatusEffect(persistentStatusId); 
            }
        }

        public RuntimeCharacterData(CharacterSaveData save)
        {
            characterId = save.characterId;
            name = save.name;
            workedDays = save.workedDays;
            
            if (System.Enum.TryParse(save.race, out Race parsedRace)) race = parsedRace;
            if (System.Enum.TryParse(save.align, out Align parsedAlign)) align = parsedAlign;
            if (System.Enum.TryParse(save.row, out RowType parsedRow)) row = parsedRow;
            if (System.Enum.TryParse(save.column, out ColumnType parsedCol)) column = parsedCol;
            if (System.Enum.TryParse(save.gender, out Gender parsedGender)) gender = parsedGender;
            if (System.Enum.TryParse(save.basicAttackVfxID, out VfxID parseBasicAtkVfx)) basicAttackVfxId = parseBasicAtkVfx;
            if (System.Enum.TryParse(save.persistentStatusId, out StatusEffectID parseStatusEffectID)) persistentStatusId = parseStatusEffectID;
            resonanceId = save.resonanceId;

            stats = save.stats;
            resistances = save.resistances;
            isCommander = save.isCommander;
            isMonster = save.isMonster;

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
            workedDays = 0;
            race = entry.race;
            gender = entry.gender;
            align = entry.align;
            resonanceId = entry.resonanceId;
            stats = entry.stats;
            resistances = entry.resistances;
            isCommander = entry.isCommander;
            isRegular = entry.isRegular;
            isMonster = entry.isMonster;
            basicAttackVfxId = entry.basicAttackVfxId;

            currentHp = maxHp = BattleCalculator.GetMaxHP(stats.level, stats.str, stats.vit);
            currentMp = maxMp = BattleCalculator.GetMaxMP(stats.level, stats.mag, stats.intel);

            currentExp = 0;

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
            data.race = this.race.ToString();
            data.align = this.align.ToString();
            data.gender = this.gender.ToString();
            data.isCommander = this.isCommander;
            data.isMonster = this.isMonster;
            data.workedDays = this.workedDays;

            data.resonanceId = this.resonanceId;
            
            data.weaponId = this.equippedWeaponId;
            data.gunId = this.equippedGunId;
            data.ammoId = this.equippedAmmoId;
            data.armorIds = this.equippedArmorIds;
            data.basicAttackVfxID = this.basicAttackVfxId.ToString();
            
            data.maxHp = this.maxHp;
            data.maxMp = this.maxMp;
            data.currentHp = this.currentHp;
            data.currentMp = this.currentMp;
            
            data.exp = this.currentExp;
            data.row = this.row.ToString();
            data.column = this.column.ToString();

            data.persistentStatusId = this.persistentStatusId.ToString();
            
            data.resistances = this.resistances;
            data.stats = this.stats;
            data.learnedSkillIds = this.learnedSkills;
            
            return data;
        }

        // UI나 전투 로직은 이 함수만 호출하면 됨
        public int GetRequiredExpForNextLevel() 
        {
            return BattleCalculator.GetMaxExpForLevel(stats.level, race, gender);
        }
    }
}