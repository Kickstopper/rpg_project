using UnityEngine;
using System.Collections.Generic;

namespace Data
{
    [CreateAssetMenu(fileName = "NewSkillTree", menuName = "RPG/Skill Tree Data")]
    public class SkillTreeData : ScriptableObject
    {
        public List<SkillUnlockNode> unlockNodes = new List<SkillUnlockNode>();
    }
}