using UnityEngine;
using System.IO;
using Controller;
using Data;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
namespace Manager
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance;

        public const int SUSPEND_SLOT_INDEX = -1;

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

        // 슬롯 번호에 따른 파일명 분기
        private string GetSavePath(int slotIndex)
        {
            if (slotIndex == SUSPEND_SLOT_INDEX)
            {
                // 중단 저장용 파일명 고정
                return Path.Combine(Application.persistentDataPath, "save_suspend.json");
            }
            return Path.Combine(Application.persistentDataPath, $"save_{slotIndex}.json");
        }

        
        // 저장 (Save)
        
        public void SaveGame(int slotIndex)
        {
            SaveData data = new SaveData();

            data.saveTime = System.DateTime.Now.ToString();
            data.sceneName = SceneManager.GetActiveScene().name;

            // 플레이어 위치 저장
            data.dungeonId = DungeonMapStateManager.Instance.currentDungeonId;
            data.playerPosX = DungeonMapStateManager.Instance.currentPx;
            data.playerPosY = DungeonMapStateManager.Instance.currentPy;
            data.playerDirection = DungeonMapStateManager.Instance.currentDirection;
            // 던전 탐색 상태 저장
            data.dungeonMapStates = DungeonMapStateManager.Instance.GetAllMapStates();

            if (data.sceneName == GameScene.WORLD_MAP_SCENE)
            {
                data.worldMapState = new WorldMapState();
                
                // 현재 지역 ID 저장
                if (WorldManager.Instance.currentRegionTheme != null)
                    data.worldMapState.regionId = WorldManager.Instance.currentRegionTheme.regionID;

                // 플레이어 3D 위치 저장
                Transform player = WorldManager.Instance.currentPlayerTransform;
                if (player != null)
                {
                    data.worldMapState.x = player.position.x;
                    data.worldMapState.y = player.position.y;
                    data.worldMapState.z = player.position.z;
                }
            }
            
            // 인벤토리 & 골드 저장
            data.money = InventoryManager.Instance.GetMoney();
            data.inventory = InventoryManager.Instance.GetSaveData(); 

            // 파티원 정보 저장
            foreach(var i in PartyManager.Instance.partyData)
            {
                data.partyMembers.Add(i.ToSaveData());
            }

            // 이벤트 플래그 저장
            data.eventFlags = FlagManager.Instance.GetSaveData();

            // 모듈과 메모리 정보
            data.maxBlockSize = ModuleManager.Instance.maxBlockSize;
            data.ownedModules = new List<ModuleFeature>(ModuleManager.Instance.ownedModules);
            data.mountedModules = new List<PlacedModuleData>(ModuleManager.Instance.GetMountedModules());

            // 파일 쓰기
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSavePath(slotIndex), json);
            
            Debug.Log($"[Slot {slotIndex}] 게임 저장 완료");
        }

        
        // 불러오기
        public void LoadGame(int slotIndex)
        {
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // 골드 및 인벤토리 복구
            InventoryManager.Instance.SetMoney(data.money);
            InventoryManager.Instance.LoadFromSaveData(data.inventory);

            FlagManager.Instance.LoadFromSaveData(data.eventFlags);

            // 파티원 복구
            // 기존 파티 클리어 후 재생성 로직 필요
            PartyManager.Instance.LoadFromSave(data.partyMembers);
            
            // 맵 매니저 설정
            if (DungeonMapStateManager.Instance != null)
            {
                if (data.dungeonMapStates != null && data.dungeonMapStates.Count > 0)
                {
                    DungeonMapStateManager.Instance.LoadMapStates(data.dungeonMapStates);
                }
                DungeonMapStateManager.Instance.UpdatePlayerPosition(data.playerPosX, data.playerPosY, data.playerDirection, data.dungeonId);
            }

            // 모듈과 메모리 정보
            ModuleManager.Instance.LoadGame(data);
            
            if (data.sceneName == GameScene.WORLD_MAP_SCENE)
            {
                if (data.worldMapState != null)
                {
                    // 저장된 지역 테마 복원
                    WorldManager.Instance.SetCurrentRegionTheme(data.worldMapState.regionId);
                    
                    // 불러오기를 통해 진입했음을 알리고 좌표를 전달
                    WorldManager.Instance.isLoadGame = true;
                    WorldManager.Instance.loadedPosition = new Vector3(data.worldMapState.x, data.worldMapState.y, data.worldMapState.z);
                }
            }
            else //if (data.sceneName == GameScene.DUNGEON_MAP_SCENE)
            {
                // 던전 탐색 상태 복구
                DungeonManager.Instance.LoadDungeonFromJson(data.dungeonId);
                DungeonManager.Instance.UpdateDungeonStartPosition(data.playerPosX, data.playerPosY, data.playerDirection);
            }
            

            // 중단 데이터라면 로드 후 즉시 삭제
            if (slotIndex == SUSPEND_SLOT_INDEX)
            {
                File.Delete(path);
                Debug.Log("중단 저장 데이터가 로드되어 삭제되었습니다.");
            }
            
            SceneManager.LoadScene(data.sceneName);
            GameStateManager.Instance.ChangeState(GameState.Exploration);
            Debug.Log("게임 불러오기 완료");
        }

        // 해당 슬롯의 데이터 미리보기
        public SaveData GetSaveDataHeader(int slotIndex)
        {
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                return null;
            }
        }

        // 중단 저장 데이터가 존재하는지 확인 (타이틀 화면용)
        public bool HasSuspendData()
        {
            return File.Exists(GetSavePath(SUSPEND_SLOT_INDEX));
        }
    }
    
}
