using UnityEngine;
// 아이템 효과 타입 정의
public enum ItemEffectType 
{ 
    // [Recover 탭]
    RecoverHP, RecoverMP, Revive,
    
    // [Buff 탭]
    Buff_ReflectPhys, Buff_ReflectMagic,  // 물리 및 마법 공격 반사
    Buff_AbsorbPhys, Buff_AbsorbMagic,    // 물리 및 마법 공격 흡수 
    Buff_AttackUp, Buff_DefenseUp,        // 공격력 및 방어력 상승
    
    // [Attack 탭]
    Attack_Physical, Attack_Magic
}

namespace Data
{
    [CreateAssetMenu(fileName = "New Consumable", menuName = "RPG/Item/Consumable")]
    public class ConsumableItemData : BaseItemData
    {
        [Header("Effect Settings")]
        public ItemEffectType effectType;
        public int effectValue; // 회복량 또는 데미지

        [Header("Targeting")]
        public TargetScope targetScope; // 아군 1명, 적 전체 등
        public ElementType element = ElementType.None; // 공격 아이템일 경우 속성

        [Header("Visual")]
        public GameObject effectPrefab; // 사용 시 이펙트 (옵션)

        // UI 분류를 위한 헬퍼 함수
        public int GetCategoryIndex()
        {
            // 0: Recover, 1: Buff, 2: Attack
            switch (effectType)
            {
                case ItemEffectType.RecoverHP:
                case ItemEffectType.RecoverMP:
                case ItemEffectType.Revive:
                    return 0; // Recover
                
                case ItemEffectType.Buff_ReflectPhys:
                case ItemEffectType.Buff_ReflectMagic:
                case ItemEffectType.Buff_AbsorbPhys:
                case ItemEffectType.Buff_AbsorbMagic:
                case ItemEffectType.Buff_AttackUp:
                case ItemEffectType.Buff_DefenseUp:
                    return 1; // Buff

                case ItemEffectType.Attack_Physical:
                case ItemEffectType.Attack_Magic:
                    return 2; // Attack
                
                default: return 0;
            }
        }
    }
}