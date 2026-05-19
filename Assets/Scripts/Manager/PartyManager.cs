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
            partyData.Clear();
            
            foreach(var save in saveDatas)
            {
                var data = new RuntimeCharacterData(save);

                var entry = charDB.GetEntry(data.characterId);
                
                if (entry != null)
                {
                    data.expTable = entry.expTable;
                }
                else
                {
                    Debug.LogError($"ID [{data.characterId}]에 해당하는 캐릭터 데이터를 DB에서 찾을 수 없습니다.");
                }

                partyData.Add(data);
            }
        }

        // 새 게임 시작: 초기 멤버 세팅
        public void SetDefaultCharacterData()
        {
            partyData.Clear();
            string[] starterIds = { 
                PartyID.CHARACTER_00, 
                PartyID.CHARACTER_01, 
                // PartyID.CHARACTER_02,
                // PartyID.CHARACTER_03, PartyID.CHARACTER_04, PartyID.CHARACTER_05 
            };
            
            foreach (var id in starterIds)
            {
                var entry = charDB.GetEntry(id);
                if (entry != null)
                {
                    RuntimeCharacterData newData = new RuntimeCharacterData(entry);
                    partyData.Add(newData);
                }
                else
                {
                    Debug.LogWarning($"초기 캐릭터 ID [{id}]를 DB에서 찾을 수 없습니다.");
                    // 비상용 하드코딩 데이터
                    // partyData.Add(DefaultCharacterData.GetDefaultCharacterData(id));
                }
            }
        }

        // 특정 인덱스의 데이터 반환 (전투나 메뉴에서 호출)
        public RuntimeCharacterData GetMember(int index)
        {
            if (index < 0 || index >= partyData.Count) return null;
            return partyData[index];
        }
        
        // 파티의 전투 대열을 바꿈
        public void SwapMemberPosition(int idxA, int idxB)
        {
            // 각 인덱스에 해당하는 캐릭터 찾기
            RuntimeCharacterData charA = GetCharacterAtSlot(idxA);
            RuntimeCharacterData charB = GetCharacterAtSlot(idxB);

            // 인덱스를 Row/Column으로 변환
            RowType rowA = (idxA < 3) ? RowType.Front : RowType.Back;
            ColumnType colA = (ColumnType)(idxA % 3);

            RowType rowB = (idxB < 3) ? RowType.Front : RowType.Back;
            ColumnType colB = (ColumnType)(idxB % 3);

            // 데이터 적용
            if (charA != null)
            {
                charA.row = rowB;
                charA.column = colB;
            }
            
            if (charB != null)
            {
                charB.row = rowA;
                charB.column = colA;
            }
        }

        // 특정 슬롯에 있는 캐릭터를 찾는 헬퍼 함수
        private RuntimeCharacterData GetCharacterAtSlot(int index)
        {
            RowType r = (index < 3) ? RowType.Front : RowType.Back;
            ColumnType c = (ColumnType)(index % 3);
            
            return partyData.Find(ch => ch.row == r && ch.column == c);
        }

        // id로 캐릭터를 찾는 헬퍼 함수
        public RuntimeCharacterData GetCharacterByID(string characterId)
        {
            return partyData.Find(ch => ch.characterId == characterId);
        }
    }
}