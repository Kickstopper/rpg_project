using RPGProject.Infrastructure.DataAccess;
using UnityEngine;
namespace RPGProject.Feature.Skills
{
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "Game Data/Database/Skill Database")]
    public class SkillDatabase : BaseDatabase<SkillData> { }
}

