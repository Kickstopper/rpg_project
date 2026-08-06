using UnityEngine;
using System.Collections.Generic;

namespace Data.Database
{
    [CreateAssetMenu(fileName = "NpcDatabase", menuName = "Game Data/NPC Database")]
    public class NpcDatabase : ScriptableObject
    {
        [System.Serializable]
        public class NpcEntry
        {
            public string id;
            public string name;     // UI에 표시될 이름
            public Gender gender;

            [Header("Visual")]
            public Sprite portraitImage;
            public Sprite standingImage;
        }

        public List<NpcEntry> entries = new List<NpcEntry>();
        private Dictionary<string, NpcEntry> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, NpcEntry>();
            foreach (var entry in entries)
            {
                if (!lookupTable.ContainsKey(entry.id))
                {
                    lookupTable.Add(entry.id, entry);
                }
            }
        }

        public NpcEntry GetEntry(string id)
        {
            if (lookupTable == null) Initialize();
            
            if (lookupTable.ContainsKey(id)) return lookupTable[id];
            
            return null;
        }
    }
}