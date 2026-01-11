using UnityEngine;
using Data;

namespace Manager
{
    public class DatabaseManager : MonoBehaviour
    {
        public static DatabaseManager Instance;

        [Header("Databases")]
        public WeaponDatabase weaponDB;
        public AmmoDatabase ammoDB;
        public ArmorDatabase armorDB;
        public SkillDatabase skillDB;

        public ConsumableDatabase cosumableDB;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                // 게임 시작 시 딕셔너리 초기화
                weaponDB.Initialize();
                ammoDB.Initialize();
                armorDB.Initialize();
                skillDB.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 사용 예시 함수
        public WeaponData GetWeapon(string id) => weaponDB.GetItem(id);
        public AmmoData GetAmmo(string id) => ammoDB.GetItem(id);
        public ArmorData GetArmor(string id) => armorDB.GetItem(id);
        public SkillData GetSkill(string id) => skillDB.GetItem(id);
        public ConsumableItemData GetConsumable(string id) => cosumableDB.GetItem(id);
    }
}
