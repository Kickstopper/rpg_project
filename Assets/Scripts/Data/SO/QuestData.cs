using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [System.Serializable]
    public class QuestTarget
    {
        public string monsterID;     // 몬스터 ID (예: "Goblin", "Slime")
        public int requiredCount;    // 요구 토벌 수 (예: 2, 3)
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "RPG/Quest Data")]
    public class QuestData:ScriptableObject
    {
        public string QuestID;
        public string QuestName;
        public string QuestType;
        public List<QuestTarget> Targets = new List<QuestTarget>();
        public string Location;   // 지역의 이름
        public string locationID; // 실제 퀘스트가 진행될 맵들의 공통 ID
        public int Risk;
        public int Reward;
        public string Description;
    }
}