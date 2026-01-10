using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "New Ammo", menuName = "RPG/Item/Ammo")]
    public class AmmoData : BaseItemData
    {
        [Header("Ammo Stats")]
        public int damageBonus; // 총기 공격력에 추가될 데미지
        public int hitRateBonus;
        public ElementType element = ElementType.Physical; // 특수 탄환(화염탄 등)

        [Header("Effect")]
        // 상태이상을 유발한다면 여기에 추가 (예: 마비탄)
        public float statusEffectChance = 0f; 
    }
    
}