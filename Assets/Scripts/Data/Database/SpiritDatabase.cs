using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "SpiritDatabase", menuName = "Game Data/Database/Spirit Database")]
    public class SpiritDatabase : ScriptableObject
    {
        public List<SpiritData> db = new List<SpiritData>();

        // 검색 속도를 위해 Entry 자체를 저장하는 딕셔너리
        private Dictionary<string, SpiritData> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, SpiritData>();
            foreach (var data in db)
            {
                if (!lookupTable.ContainsKey(data.id))
                {
                    lookupTable.Add(data.id, data);
                }
            }
        }

        public SpiritData GetData(string id)
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

