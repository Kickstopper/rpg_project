using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "NewQuest", menuName = "RPG/Quest Data")]
    public class QuestData:ScriptableObject
    {
        public string QuestID;
        public string QuestName;
        public string QuestType;
        public string TargetMonster;
        public string Location;
        public int Risk;
        public int Reward;
    }
}