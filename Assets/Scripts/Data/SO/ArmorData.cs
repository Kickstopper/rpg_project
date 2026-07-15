using UnityEngine;


namespace Data
{
    public enum ArmorSlot { Helmet, Body, Gloves, Boots, Accessory }
    
    [CreateAssetMenu(fileName = "New Armor", menuName = "RPG/Item/Armor")]
    public class ArmorData : BaseItemData
    {
        public ArmorData() { itemCategory = ItemCategory.Armor; }
        [Header("Defense Only")]
        public ArmorSlot slot;
        public int defense;
        public int evasionMod; // 회피율 보정

        [Header("Bonus")]
        public StatData statBonus; // 착용 시 힘, 마력 등 증가
        
        [Header("Resistances")]
        // 0이면 변경 없음. 덮어쓰기 로직 혹은 합연산 로직 필요.
        // 여기서는 간단히 '착용 시 적용할 내성 계수'로 가정
        public ResistanceData resistanceMod; 

        
    }
}
