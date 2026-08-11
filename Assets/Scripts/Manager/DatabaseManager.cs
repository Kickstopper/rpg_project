using UnityEngine;
using Data;
using Data.Database;

namespace Manager
{
    public class DatabaseManager : MonoBehaviour
    {
        [Header("Global Settings")]
        public ExpTable globalExpTable;

        [Header("Databases")]
        public BackgroundDatabase bgDB;
        public CharacterDatabase charDB;
        public MonsterDatabase monsterDB;
        public NpcDatabase npcDB;
        public WeaponDatabase weaponDB;
        public AmmoDatabase ammoDB;
        public ArmorDatabase armorDB;
        public SkillDatabase skillDB;
        public ConsumableDatabase cosumableDB;
        public ResonanceDatabase spiritDB;
        public QuestDatabase questDB;
        public StatusEffectDatabase statusEffectDB;

        void Awake()
        {
            // 게임 시작 시 딕셔너리 초기화
            bgDB.Initialize();
            charDB.Initialize();
            monsterDB.Initialize();
            npcDB.Initialize();
            weaponDB.Initialize();
            ammoDB.Initialize();
            armorDB.Initialize();
            skillDB.Initialize();
            spiritDB.Initialize();
            questDB.Initialize();
        }
        
        // ID 하나로 모든 아이템 DB를 검색하여 반환하는 통합 함수
        public BaseItemData GetItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var weapon = GetWeapon(id);
            if (weapon != null) return weapon;

            var armor = GetArmor(id);
            if (armor != null) return armor;

            var ammo = GetAmmo(id);
            if (ammo != null) return ammo;

            var consumable = GetConsumable(id);
            if (consumable != null) return consumable;

            return null;
        }

        public StatusEffectData GetStatusEffect(StatusEffectID id) => statusEffectDB.GetData(id);
        public WeaponData GetWeapon(string id) => weaponDB.GetItem(id);
        public AmmoData GetAmmo(string id) => ammoDB.GetItem(id);
        public ArmorData GetArmor(string id) => armorDB.GetItem(id);
        public SkillData GetSkill(string id) => skillDB.GetItem(id);
        public ConsumableItemData GetConsumable(string id) => cosumableDB.GetItem(id);
        public ResonanceData GetResonance(string id) => spiritDB.GetData(id);
    }
}
