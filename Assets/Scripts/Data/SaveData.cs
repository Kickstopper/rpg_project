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
    public class PlacedModuleData
    {
        public ModuleFeature feature;
        public int x;
        public int y;
        public int rotation; // rotation값 * 90도로 회전함
    }
    
    [Serializable]
    public class QuestProgress
    {
        public string questID;
        // 몬스터ID를 Key로, 현재 토벌한 마릿수를 Value로 저장합니다.
        public Dictionary<string, int> killCounts = new Dictionary<string, int>(); 
        public bool isReadyToReport = false;
    }

    // 시간 진행도를 저장하기 위한 클래스
    [Serializable]
    public class TimeProgress
    {
        public int year = 1;
        public int month = 1;
        public int day = 1;
        public int currentSteps = 0;
    }
    
    [Serializable]
    public class SaveData
    {
        // 기본 정보
        public string saveTime;
        public string sceneName;
        
        // 플레이어 위치
        public int playerPosX;
        public int playerPosY;
        public Direction playerDirection;

        // 3D 던전
        public string dungeonId; // 현재 맵 ID
        public List<DungeonMapState> dungeonMapStates = new List<DungeonMapState>();

        // 월드맵
        public WorldMapState worldMapState;
        
        // 자산 및 인벤토리
        public int money;
         // 아이템 ID 리스트
        public List<ItemSaveEntry> inventory = new List<ItemSaveEntry>();

        // 파티원 정보 리스트
        public List<CharacterSaveData> partyMembers = new List<CharacterSaveData>();
        // 해금된(한 번이라도 영입한) 전체 동료 명부
        public List<CharacterSaveData> unlockedRoster = new List<CharacterSaveData>();

        // 세이브 파일에 포함될 시간 데이터
        public TimeProgress timeProgress = new TimeProgress();

        // 퀘스트 저장용 리스트
        public List<string> completedQuestIDs = new List<string>();
        public List<QuestProgress> activeQuests = new List<QuestProgress>();

        // 이벤트 플래그
        public List<string> eventFlags = new List<string>();
        
        // 대화 이벤트 종료 
        public List<string> completedDialogues = new List<string>();

        // 가진 모듈과 인스톨된 모듈
        public List<ModuleFeature> ownedModules;
        public List<PlacedModuleData> mountedModules;
        public int maxBlockSize;
    }

    [Serializable]
    public class CharacterSaveData
    {
        public string characterId; // DB에서 원본 데이터를 찾기 위한 ID (예: "char_warrior")
        public string name;
        public string race;
        public string gender;
        public int level;
        public string align;
        public bool isCommander;
        public bool isMonster;

        public string resonanceId;
        public string persistentStatusId; // 지속되는 상태 이상

        public StatData stats;
        public ResistanceData resistances;

        public string row;
        public string column;

        public int currentHp;
        public int maxHp;
        public int currentMp;
        public int maxMp;
        public int exp;
        public ExpTable expTable;
        
        // 장비 상태
        public string weaponId;
        public string gunId;
        public string ammoId;

        public string basicAttackVfxID;
        public List<string> armorIds;

        // 배운 스킬
        public List<string> learnedSkillIds;
    }
}