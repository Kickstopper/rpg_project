using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "RPG/Skill")]
    public class SkillData : BaseRootData 
    {
        [Header("Cost")]
        public bool useHpCost; 
        public int costValue;

        [Header("Effect Settings")]
        public int hitRate;

        public int GetCategoryIndex()
        {
            // 0: Magic, 1: Recover, 2: Assistance, 3: Special
            switch (effectType)
            {
                case EffectType.Magic_Atk:
                    return 0; // Magic
                
                case EffectType.Recover_HP:
                case EffectType.Recover_MP:
                case EffectType.Revive_Empty:
                case EffectType.Revive_Fully:
                    return 1; // Recover

                case EffectType.Buff_Phys_Atk:
                case EffectType.Buff_Magic_Atk:
                case EffectType.Debuff_Phys_Atk:
                case EffectType.Debuff_Magic_Def:
                case EffectType.Reflect_Phys:
                case EffectType.Reflect_Magic:
                case EffectType.Absorb_Phys:
                case EffectType.Absorb_Magic:
                    return 2; // Assistance

                case EffectType.Special_Atk:
                    return 3;
                
                default: return 0;
            }
        }
    }
}
