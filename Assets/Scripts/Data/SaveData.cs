using System;
using System.Collections.Generic;
namespace Data
{
    [Serializable]
    public class ItemSaveEntry
    {
        public string itemId;
        public int count;

        public ItemSaveEntry(string id, int count)
        {
            this.itemId = id;
            this.count = count;
        }
    }
    [Serializable]
    public class SaveData
    {
        // 1. 기본 정보
        public string saveTime;
        public string sceneName;
        public string dungeonId; // 현재 맵 이름
        
        // 2. 플레이어 위치
        public int playerPosX;
        public int playerPosY;

        public Direction playerDirection;

        // 3. 자산 및 인벤토리
        public int gold;
         // 아이템 ID 리스트
        public List<ItemSaveEntry> inventory = new List<ItemSaveEntry>();

        // 4. 파티원 정보 리스트
        public List<CharacterSaveData> partyMembers = new List<CharacterSaveData>();

        // 5. 이벤트 플래그
        public List<string> eventFlags = new List<string>();
    }

    [Serializable]
    public class CharacterSaveData
    {
        public string characterId; // DB에서 원본 데이터를 찾기 위한 ID (예: "char_warrior")
        public int level;

        public string align;

        public string row;

        public int currentHp;
        public int currentMp;
        public int exp;
        
        // 장비 상태
        public string weaponId;
        public string gunId;
        public string ammoId;
        public List<string> armorIds;

        // 배운 스킬
        public List<string> learnedSkillIds;
    }
}