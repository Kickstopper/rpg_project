using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Game Data/Database/Quest Database")]
    public class QuestDatabase : ScriptableObject
    {
        public List<QuestData> db = new List<QuestData>();
        private Dictionary<string, QuestData> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, QuestData>();
            foreach (var data in db)
            {
                if (!lookupTable.ContainsKey(data.QuestID))
                {
                    lookupTable.Add(data.QuestID, data);
                }
            }
        }

        public QuestData GetData(string id)
        {
            if (lookupTable == null) Initialize();

            if (lookupTable.ContainsKey(id))
            {
                return lookupTable[id];
            }
            
            return null;
        }
    }
}