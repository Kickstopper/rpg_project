using UnityEngine;
using System.IO;
using Data;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Manager
{
    public class SaveManager : MonoBehaviour
    {
        public const int SUSPEND_SLOT_INDEX = -1;

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

        
        // 저장
        public void SaveGame(int slotIndex)
        {
            SaveData data = new SaveData();

            data.saveTime = System.DateTime.Now.ToString();
            data.sceneName = SceneManager.GetActiveScene().name;

            // 플레이어 위치 저장
            data.dungeonId = ManagerRoot.DungeonMapState.currentDungeonId;
            data.playerPosX = ManagerRoot.DungeonMapState.currentPx;
            data.playerPosY = ManagerRoot.DungeonMapState.currentPy;
            data.playerDirection = ManagerRoot.DungeonMapState.currentDirection;
            
            // 던전 탐색 상태 저장
            data.dungeonMapStates = ManagerRoot.DungeonMapState.GetAllMapStates();

            if (data.sceneName == GameScene.WORLD_MAP_SCENE)
            {
                data.worldMapState = new WorldMapState();
                
                // 현재 지역 ID 저장
                if (ManagerRoot.World.currentRegionTheme != null)
                    data.worldMapState.regionId = ManagerRoot.World.currentRegionTheme.regionID;

                // 플레이어 3D 위치 저장
                Transform player = ManagerRoot.World.currentPlayerTransform;
                if (player != null)
                {
                    data.worldMapState.x = player.position.x;
                    data.worldMapState.y = player.position.y;
                    data.worldMapState.z = player.position.z;
                }
            }
            
            // 인벤토리 & 골드 저장
            data.money = ManagerRoot.Inventory.GetMoney();
            data.inventory = ManagerRoot.Inventory.GetSaveData(); 

            // 파티와 로스터 정보 저장
            ManagerRoot.Party.SaveToData(data);

            // 이벤트 플래그 저장
            data.eventFlags = ManagerRoot.Flag.GetSaveData();

            // 퀘스트 달성 상태 저장
            ManagerRoot.Quest.Save(data);

            // 게임 내의 시간 저장
            ManagerRoot.Time.Save(data);

            // 대화 이벤트 발생 여부 저장
            data.completedDialogues = ManagerRoot.DungeonEvent.GetCompletedTriggers();
            
            // 모듈과 메모리 정보
            data.maxBlockSize = ManagerRoot.Module.maxBlockSize;
            data.ownedModules = new List<ModuleFeature>(ManagerRoot.Module.ownedModules);
            data.mountedModules = new List<PlacedModuleData>(ManagerRoot.Module.GetMountedModules());

            string json = JsonConvert.SerializeObject(data, Formatting.Indented); // Indented를 사용해 Json 파일이 줄바꿈되게 함
            File.WriteAllText(GetSavePath(slotIndex), json);
            
            Debug.Log($"[Slot {slotIndex}] 게임 저장 완료");
        }

        
        // 불러오기
        public void LoadGame(int slotIndex)
        {
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            
            SaveData data = JsonConvert.DeserializeObject<SaveData>(json);

            // 골드 및 인벤토리 복구
            ManagerRoot.Inventory.SetMoney(data.money);
            ManagerRoot.Inventory.LoadFromSaveData(data.inventory);
            
            // 플래그 복구
            ManagerRoot.Flag.LoadFromSaveData(data.eventFlags);

            // 퀘스트 달성 상태 복구
            ManagerRoot.Quest.Load(data);

            // 게임 내의 시간 복구
            ManagerRoot.Time.Load(data);
            
            // 대화 이벤트 발생 정보 복구
            ManagerRoot.DungeonEvent.ApplyCompletedTriggers(data.completedDialogues);

            // 파티원 복구
            // 기존 파티 클리어 후 재생성 로직 필요
            ManagerRoot.Party.LoadFromSave(data);
            
            // 맵 매니저 설정
            if (ManagerRoot.DungeonMapState != null)
            {
                if (data.dungeonMapStates != null && data.dungeonMapStates.Count > 0)
                {
                    ManagerRoot.DungeonMapState.LoadMapStates(data.dungeonMapStates);
                }
                ManagerRoot.DungeonMapState.UpdatePlayerPosition(data.playerPosX, data.playerPosY, data.playerDirection, data.dungeonId);
            }

            // 모듈과 메모리 정보
            ManagerRoot.Module.LoadGame(data);
            
            if (data.sceneName == GameScene.WORLD_MAP_SCENE)
            {
                if (data.worldMapState != null)
                {
                    // 저장된 지역 테마 복원
                    ManagerRoot.World.SetCurrentRegionTheme(data.worldMapState.regionId);
                    
                    // 불러오기를 통해 진입했음을 알리고 좌표를 전달
                    ManagerRoot.World.isLoadGame = true;
                    ManagerRoot.World.loadedPosition = new Vector3(data.worldMapState.x, data.worldMapState.y, data.worldMapState.z);
                }
            }
            else //if (data.sceneName == GameScene.DUNGEON_MAP_SCENE)
            {
                // 던전 탐색 상태 복구
                ManagerRoot.Dungeon.LoadDungeonFromJson(data.dungeonId);
                ManagerRoot.Dungeon.UpdateDungeonStartPosition(data.playerPosX, data.playerPosY, data.playerDirection);
            }
            
            // 중단 데이터라면 로드 후 즉시 삭제
            // if (slotIndex == SUSPEND_SLOT_INDEX)
            // {
            //     File.Delete(path);
            //     Debug.Log("중단 저장 데이터가 로드되어 삭제되었습니다.");
            // }
            
            SceneManager.LoadScene(data.sceneName);
            ManagerRoot.GameState.ChangeState(GameState.Exploration);
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
                return JsonConvert.DeserializeObject<SaveData>(json);
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