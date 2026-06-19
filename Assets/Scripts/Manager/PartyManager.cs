using System.Collections.Generic;
using System.Linq;
using Data;
using Data.Database;
using UnityEngine;

namespace Manager
{
    public class PartyManager : MonoBehaviour
    {
        public const int MAX_PARTY_SIZE = 6; // 파티 최대 인원 설정

        public static PartyManager Instance;
        
        // ID 목록 대신 실제 데이터 리스트 사용
        public List<RuntimeCharacterData> partyData = new List<RuntimeCharacterData>();
        
        // 한 번이라도 영입된 적 있는 모든 캐릭터의 원본 데이터를 보관하는 사전
        public Dictionary<string, RuntimeCharacterData> unlockedRoster = new Dictionary<string, RuntimeCharacterData>();

        void Awake()
        {
            if (Instance == null) 
            { 
                Instance = this; 
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject); 
                
            }
            else Destroy(gameObject);
        }

        void Start()
        {
            if (partyData.Count == 0)
            {
                Debug.Log("테스트 모드: 파티 데이터를 초기화합니다.");
                SetDefaultCharacterData();
            }
        }

        // 저장 시, SaveManager가 호출할 메서드
        public void SaveToData(SaveData data)
        {
            data.partyMembers = new List<CharacterSaveData>();
            data.unlockedRoster = new List<CharacterSaveData>();

            // 현재 파티 데이터 저장 
            foreach (var member in partyData)
            {
                data.partyMembers.Add(member.ToSaveData()); 
            }

            // unlockedRoster 저장
            foreach (var kvp in unlockedRoster)
            {
                data.unlockedRoster.Add(kvp.Value.ToSaveData());
            }
        }

        public void LoadFromSave(SaveData data)
        {
            partyData.Clear();
            unlockedRoster.Clear();

            // unlockedRoster부터 생성
            if (data.unlockedRoster != null)
            {
                foreach (var save in data.unlockedRoster)
                {
                    var rosterMember = new RuntimeCharacterData(save);
                    unlockedRoster.Add(rosterMember.characterId, rosterMember);
                }
            }

            // 현재 파티 복구. 여기서 절대 new RuntimeCharacterData(save)를 또 하면 안 됨
            if (data.partyMembers != null)
            {
                foreach (var save in data.partyMembers)
                {
                    // 저장된 ID를 키값으로 삼아, 이미 생성된 명부에서 객체의 참조를 그대로 끌어옴
                    if (unlockedRoster.ContainsKey(save.characterId))
                    {
                        partyData.Add(unlockedRoster[save.characterId]);
                    }
                    else
                    {
                        // 예외 처리. 세이브 파일 조작 등으로 로스터에는 없는데 파티에만 있는 경우
                        var newMember = new RuntimeCharacterData(save);
                        unlockedRoster.Add(newMember.characterId, newMember);
                        partyData.Add(newMember);
                    }
                }
            }

            Debug.Log($"[PartyManager] 파티 {partyData.Count}명 / 로스터 {unlockedRoster.Count}명 로드 완료.");
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
                var entry = DatabaseManager.Instance.charDB.GetEntry(id);
                if (entry != null)
                {
                    RuntimeCharacterData newData = new RuntimeCharacterData(entry);
                    newData.expTable = entry.expTable;
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

        // 특정 ID의 캐릭터를 동료로 받아들임
        public bool AddMember(CharacterDatabase.CharacterEntry entry, bool isMonster = false)
        {
            if (entry == null) return false;

            RuntimeCharacterData memberData;

            // 이미 영입된 적 있는 캐릭터인지 unlockedRoster에서 먼저 확인
            if (unlockedRoster.ContainsKey(entry.id))
            {
                // 이전에 파티에 있었다가 떠난 캐릭터라면, 경험치와 장비가 보존된 데이터를 불러옴
                memberData = unlockedRoster[entry.id];
                Debug.Log($"[PartyManager] {entry.name}의 기존 데이터를 복구하여 영입합니다.");

                // 이미 현재 파티에 있는데 또 영입 이벤트가 발생한 경우 방어
                if (partyData.Contains(memberData))
                {
                    Debug.LogWarning($"[PartyManager] {entry.name}은(는) 이미 파티에 존재합니다.");
                    return false; 
                }
            }
            else
            {
                // 완전 첫 영입이라면 데이터를 새로 생성하고 unlockedRoster에 영구 등록
                memberData = new RuntimeCharacterData(entry);
                memberData.isMonster = isMonster;
                unlockedRoster.Add(entry.id, memberData);
                Debug.Log($"[PartyManager] {entry.name} 첫 영입, 명부에 새로 등록되었습니다.");
            }

            // isRegular (전투 참여 여부) 결정 로직
            if (!isMonster)
            {
                memberData.isRegular = true;
            }
            else
            {
                int currentRegularCount = partyData.Count(c => c.isRegular);
                if (currentRegularCount < MAX_PARTY_SIZE)
                {
                    memberData.isRegular = true;  
                }
                else
                {
                    memberData.isRegular = false; 
                }
            }

            // 현재 동행 리스트에 최종 추가
            partyData.Add(memberData);
            return true;
        }

        public bool RemoveMember(string characterId)
        {
            var targetChar = GetCharacterByID(characterId);
            if (targetChar != null)
            {
                partyData.Remove(targetChar);
                return true;
            }

            Debug.LogWarning($"[DialogueUI - LEAVE] 파티 내에 ID '{characterId}'를 가진 캐릭터가 존재하지 않아 이탈시킬 수 없습니다.");
            return false;
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