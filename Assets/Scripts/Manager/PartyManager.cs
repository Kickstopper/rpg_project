using System.Collections.Generic;
using Data;
using Data.Database;
using UnityEngine;

namespace Manager
{
    public class PartyManager : MonoBehaviour
    {
        public static PartyManager Instance;
        
        public CharacterDatabase charDB;

        // ID 목록 대신 실제 데이터 리스트 사용
        public List<RuntimeCharacterData> partyData = new List<RuntimeCharacterData>();

        void Awake()
        {
            if (Instance == null) 
            { 
                Instance = this; 
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject); 
                
                if (partyData.Count == 0)
                {
                    Debug.Log("테스트 모드: 파티 데이터를 초기화합니다.");
                    SetDefaultCharacterData();
                }
            }
            else Destroy(gameObject);
        }

        public void LoadFromSave(List<CharacterSaveData> saveDatas)
        {
            // 기존에 남아있던 파티 데이터(초기화 데이터 등)를 비움.
            partyData.Clear();
            
            foreach(var save in saveDatas)
            {
                var data = new RuntimeCharacterData(save);
                partyData.Add(data);
            }
        }

        // 새 게임 시작: 초기 멤버 세팅
        public void SetDefaultCharacterData()
        {
            partyData.Clear();
            string[] starterIds = { PartyID.CHARACTER_00, PartyID.CHARACTER_01, PartyID.CHARACTER_02,
                                    PartyID.CHARACTER_03, PartyID.CHARACTER_04, PartyID.CHARACTER_05 };
            
            foreach (var id in starterIds)
            {
                var data = DefaultCharacterData.GetDefaultCharacterData(id);
                if (data != null)
                {
                    partyData.Add(data);
                }
            }
        }

        // 특정 인덱스의 데이터 반환 (전투나 메뉴에서 호출)
        public RuntimeCharacterData GetMember(int index)
        {
            if (index < 0 || index >= partyData.Count) return null;
            return partyData[index];
        }
    }
}