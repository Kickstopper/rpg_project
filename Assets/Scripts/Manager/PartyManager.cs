using System.Collections.Generic;
using Data;
using Data.Database;
using UnityEngine;

namespace Manager
{
    public class PartyManager : MonoBehaviour
    {
        public static PartyManager Instance;
        
        [Header("Database")]
        public CharacterDatabase charDB;

        public List<CharacterSaveData> currentPartyData = new List<CharacterSaveData>();

        void Awake()
        {
            if (Instance == null) { 
                Instance = this; 
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject); 
                
                Initialize(); 
            }
            else Destroy(gameObject);
        }

        // 1. 새 게임 시작: DB의 초기 설정(Initial Loadout)을 읽어와서 SaveData 생성
        public void Initialize()
        {
            currentPartyData.Clear();

            // 초기 파티원 ID (기획에 따라 변경)
            string[] starterIds = { "chr000", "chr001", "chr002", "chr003", "chr004", "chr005" };

            foreach (string id in starterIds)
            {
                var entry = charDB.GetEntry(id);
                if (entry == null) continue;

                // Entry 정보를 기반으로 초기 상태 데이터 생성
                CharacterSaveData newData = new CharacterSaveData();
                newData.characterId = entry.id;
                newData.level = entry.stats.level;
                newData.currentHp = entry.maxHp; // 최대 체력으로 시작
                newData.currentMp = entry.maxMp;
                
                // 초기 장비
                newData.weaponId = entry.initialWeaponId;
                newData.gunId = entry.initialGunId;
                newData.ammoId = entry.initialAmmoId;
                newData.armorIds = new List<string>(entry.initialArmorIds);
                
                // 초기 스킬
                newData.learnedSkillIds = new List<string>(entry.initialSkillIds);

                currentPartyData.Add(newData);
            }
            
            Debug.Log("새 게임 파티 초기화 완료");
        }

        // 2. 게임 로드: 저장된 리스트를 그대로 적용
        public void LoadFromSave(List<CharacterSaveData> loadedData)
        {
            if (loadedData == null) return;
            
            currentPartyData = loadedData;
            Debug.Log($"파티 데이터 로드 완료: {currentPartyData.Count}명");
        }

        // 3. 특정 슬롯의 데이터 반환 (CombatManager 등에서 사용)
        public CharacterSaveData GetMemberSaveData(int index)
        {
            if (index < 0 || index >= currentPartyData.Count) return null;
            return currentPartyData[index];
        }

        // ID로 기본 정보(Entry) 찾기 헬퍼
        public CharacterDatabase.CharacterEntry GetEntryById(string id)
        {
            return charDB.GetEntry(id);
        }
    }
}