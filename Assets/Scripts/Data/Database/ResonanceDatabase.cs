using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "ResonanceDatabase", menuName = "Game Data/Database/Spirit Database")]
    public class ResonanceDatabase : ScriptableObject
    {
        public List<ResonanceData> db = new List<ResonanceData>();

        // 검색 속도를 위해 Entry 자체를 저장하는 딕셔너리
        private Dictionary<string, ResonanceData> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, ResonanceData>();
            foreach (var data in db)
            {
                if (!lookupTable.ContainsKey(data.id))
                {
                    lookupTable.Add(data.id, data);
                }
            }
        }

        public ResonanceData GetData(string id)
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

