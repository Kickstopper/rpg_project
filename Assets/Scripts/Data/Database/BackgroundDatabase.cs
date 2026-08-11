using UnityEngine;
using System.Collections.Generic;

namespace Data.Database
{
    [CreateAssetMenu(fileName = "BackgroundDatabase", menuName = "Game Data/Background Database")]
    public class BackgroundDatabase : ScriptableObject
    {
        [System.Serializable]
        public class BackgroundEntry
        {
            public string id;
            public Sprite bgImage;
        }

        public List<BackgroundEntry> entries = new List<BackgroundEntry>();
        private Dictionary<string, BackgroundEntry> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, BackgroundEntry>();
            foreach (var entry in entries)
            {
                if (!lookupTable.ContainsKey(entry.id))
                {
                    lookupTable.Add(entry.id, entry);
                }
            }
        }

        public BackgroundEntry GetEntry(string id)
        {
            if (lookupTable == null) Initialize();
            
            if (lookupTable.ContainsKey(id)) return lookupTable[id];
            
            return null;
        }
    }
}