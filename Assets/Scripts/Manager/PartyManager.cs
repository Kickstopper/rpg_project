using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Manager
{
    public class PartyManager : MonoBehaviour
    {
        public static PartyManager Instance;
        
        [Header("Database")]
        public CharacterDatabase charDB;

        // 현재 파티원 ID 목록 (전열 0~2, 후열 3~5)
        public string[] partyMemberIds = new string[6]; 

        void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else Destroy(gameObject);

            // 테스트용: 임시 파티 결성
            partyMemberIds[0] = "chr000"; // 전열 1
            partyMemberIds[1] = "chr001"; // 전열 2
            partyMemberIds[2] = "chr002"; // 전열 3
            partyMemberIds[3] = "chr003"; // 후열 1
            partyMemberIds[4] = "chr004"; // 후열 2
            partyMemberIds[5] = "chr005"; // 후열 3
        }

        public CharacterDatabase.CharacterEntry GetMemberData(int index)
        {
            string id = partyMemberIds[index];
            if (string.IsNullOrEmpty(id)) return null;
            return charDB.GetEntry(id);
        }
    }
}