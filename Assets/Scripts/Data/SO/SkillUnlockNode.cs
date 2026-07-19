using System.Collections.Generic;

namespace Data
{
    [System.Serializable]
    public class SkillUnlockNode
    {
        public string nodeId; // 각 노드를 구별할 고유 ID (예: "node_warrior_01")
        public int reqLevel;
        public int reqStr, reqMag, reqInt, reqVit, reqAgi, reqLuc;
        public List<string> rewardSkillChoices; 
    }
}