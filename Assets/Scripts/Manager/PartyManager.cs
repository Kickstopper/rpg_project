using System.Collections.Generic;
using Data;
using Data.Database;
using UnityEngine;
using static Data.Database.CharacterDatabase;

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
                    StartNewGame();
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
                var data = new RuntimeCharacterData(charDB.GetEntry(save.characterId));
                data.name = save.name;
                data.level = save.level;
                data.maxHp = save.maxHp;
                data.maxMp = save.maxMp;
                data.currentHp = save.currentHp;
                data.currentMp = save.currentMp;
                data.currentExp = save.exp;
                data.equippedWeaponId = save.weaponId;
                data.equippedGunId = save.gunId;
                data.equippedAmmoId = save.ammoId;
                data.equippedArmorIds = save.armorIds;
                data.learnedSkills = save.learnedSkillIds;
                data.row = save.row;
                if (System.Enum.TryParse(save.align, out Align parsedAlign)) data.align = parsedAlign;
                
                partyData.Add(data);
            }
        }

        // 새 게임 시작: 초기 멤버 세팅
        public void StartNewGame()
        {
            partyData.Clear();
            string[] starterIds = { "chr000", "chr001", "chr002", "chr003" }; // 기획에 따라 변경

            foreach (var id in starterIds)
            {
                var entry = charDB.GetEntry(id);
                if (entry != null)
                {
                    partyData.Add(new RuntimeCharacterData(entry));
                }
            }
        }

        // 특정 인덱스의 데이터 반환 (전투나 메뉴에서 호출)
        public RuntimeCharacterData GetMember(int index)
        {
            if (index < 0 || index >= partyData.Count) return null;
            return partyData[index];
        }

        // ID로 원본 DB 데이터 찾기 헬퍼
        public CharacterEntry GetOriginalEntry(string id)
        {
            return charDB.GetEntry(id);
        }

        // 전투 종료 후 상태 업데이트
        public void UpdateMemberStatus(int index, int hp, int mp, int exp, int level)
        {
            if (index < 0 || index >= partyData.Count) return;

            var member = partyData[index];
            member.currentHp = hp;
            member.currentMp = mp;
            member.currentExp = exp;
            member.level = level;
            // 필요 시 장비나 스킬 변경 사항도 여기서 업데이트
        }
    }
}