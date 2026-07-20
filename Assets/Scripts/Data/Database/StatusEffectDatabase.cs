using System.Collections.Generic;
using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "StatusEffectDatabase", menuName = "Game Data/Database/StatusEffect Database")]
    public class StatusEffectDatabase : ScriptableObject
    {
        public List<StatusEffectData> db = new List<StatusEffectData>();

        // 검색 속도를 위해 Entry 자체를 저장하는 딕셔너리
        private Dictionary<StatusEffectID, StatusEffectData> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<StatusEffectID, StatusEffectData>();
            foreach (var data in db)
            {
                if (!lookupTable.ContainsKey(data.id))
                {
                    lookupTable.Add(data.id, data);
                }
            }
        }

        public StatusEffectData GetData(StatusEffectID id)
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

