using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "New Consumable", menuName = "RPG/Item/Consumable")]
    public class ConsumableItemData : BaseItemData
    {
        // UI 분류를 위한 헬퍼 함수
        public int GetCategoryIndex()
        {
            // 0: Recover, 1: Buff, 2: Attack
            switch (effectType)
            {
                case EffectType.Recover_HP:
                case EffectType.Recover_MP:
                case EffectType.Revive_Empty:
                case EffectType.Revive_Fully:
                    return 0; // Recover
                
                case EffectType.Reflect_Phys:
                case EffectType.Reflect_Magic:
                case EffectType.Absorb_Phys:
                case EffectType.Absorb_Magic:
                case EffectType.Buff_Phys_Atk:
                case EffectType.Buff_Phys_Def:
                case EffectType.Buff_Magic_Atk:
                case EffectType.Buff_Mag_Def:
                case EffectType.Debuff_Phys_Atk:
                case EffectType.Debuff_Phys_Def:
                case EffectType.Debuff_Magic_Atk:
                case EffectType.Debuff_Magic_Def:
                    return 1; // Assistance

                case EffectType.Special_Atk:
                case EffectType.Magic_Atk:
                    return 2; // Attack
                
                default: return 0;
            }
        }
    }
}