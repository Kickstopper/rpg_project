using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "RPG/Skill")]
    public class SkillData : BaseRootData 
    {
        [Header("Cost")]
        public bool useHpCost; 
        public int costValue;

        [Header("Effect")]
        public ElementType element;
        public TargetScope target;
        public int basePower;
        public int hitRate;
    }
}
