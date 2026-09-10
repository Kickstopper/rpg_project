using RPGProject.Shared.Gameplay;
using UnityEngine;

namespace RPGProject.Feature.Inventory
{
    [CreateAssetMenu(fileName = "New Ammo", menuName = "RPG/Item/Ammo")]
    public class AmmoData : BaseItemData
    {
        [Header("Ammo Stats")]
        public int damageBonus; // 총기 공격력에 추가될 데미지
        public int hitRateBonus;
    }
}