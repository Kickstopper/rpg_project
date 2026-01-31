using UnityEngine;
using System.Collections.Generic;
namespace Data.Database
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game Data/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [System.Serializable]
        public class CharacterEntry
        {
            public string id;
            
            [Header("Visual")]
            public Sprite portraitImage;
            public Sprite standingImage;
            
        }

        [Header("캐릭터 이미지 등록")]
        public List<CharacterEntry> entries = new List<CharacterEntry>();

        // 검색 속도를 위해 Entry 자체를 저장하는 딕셔너리
        private Dictionary<string, CharacterEntry> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, CharacterEntry>();
            foreach (var entry in entries)
            {
                if (!lookupTable.ContainsKey(entry.id))
                {
                    lookupTable.Add(entry.id, entry);
                }
            }
        }

        public CharacterEntry GetEntry(string id)
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
