using System.Collections.Generic;
namespace Data
{
    // Lifetime: one battle. Repeated death notifications for one combatant count once.
    public sealed class QuestBattleRecord
    {
        private readonly HashSet<int> instances = new HashSet<int>();
        private readonly List<string> kills = new List<string>();
        private bool applied;
        public void Reset() { instances.Clear(); kills.Clear(); applied = false; }
        public void Record(int instanceID, string monsterID)
        {
            if (!applied && !string.IsNullOrEmpty(monsterID) && instances.Add(instanceID)) kills.Add(monsterID);
        }
        public List<string> Consume()
        {
            if (applied) return new List<string>();
            applied = true;
            return new List<string>(kills);
        }
    }
}
