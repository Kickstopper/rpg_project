using UnityEngine;
using System.IO;
using Controller;
using Data;
using UnityEngine.SceneManagement; // PlayerController 접근용
namespace Manager
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance;

        public const int SUSPEND_SLOT_INDEX = -1; // 중단 저장용 상수 ID

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

        // =========================================================
        // 저장 (Save)
        // =========================================================
        public void SaveGame(int slotIndex)
        {
            SaveData data = new SaveData();

            data.saveTime = System.DateTime.Now.ToString();
            data.sceneName = SceneManager.GetActiveScene().name;

            // 1. 플레이어 위치 저장 (탐험 모드일 때의 플레이어 오브젝트 참조 필요)
            data.dungeonId = MapManager.Instance.currentDungeonId;
            data.playerPosX = MapManager.Instance.currentPx;
            data.playerPosY = MapManager.Instance.currentPy;
            
            // 2. 인벤토리 & 골드 저장
            data.gold = InventoryManager.Instance.GetGold();
            data.inventory = InventoryManager.Instance.GetSaveData(); 

            // 3. 파티원 정보 저장
            foreach(var i in PartyManager.Instance.partyData)
            {
                data.partyMembers.Add(i.ToSaveData());
            }

            // 4. 이벤트 플래그 저장 (FlagManager가 있다면 가져옴)
            data.eventFlags = FlagManager.Instance.GetSaveData();

            // 파일 쓰기
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSavePath(slotIndex), json);
            
            Debug.Log($"[Slot {slotIndex}] 게임 저장 완료");
        }

        // =========================================================
        // 불러오기 (Load)
        // =========================================================
        public void LoadGame(int slotIndex)
        {
            string path = GetSavePath(slotIndex);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // 1. 골드 및 인벤토리 복구
            InventoryManager.Instance.SetGold(data.gold);
            InventoryManager.Instance.LoadFromSaveData(data.inventory);

            FlagManager.Instance.LoadFromSaveData(data.eventFlags);

            // 2. 파티원 복구
            // 기존 파티 클리어 후 재생성 로직 필요
            PartyManager.Instance.LoadFromSave(data.partyMembers);

            
            // 맵 매니저 설정
            if (MapManager.Instance != null)
            {
                MapManager.Instance.UpdatePlayerPosition(data.playerPosX, data.playerPosY, data.playerDirection, data.dungeonId);
            }

            // 3. 씬 이동 및 위치 복구
            if (data.sceneName == GameScene.DUNGEON_MAP_SCENE)
            {
                LevelManager.Instance.LoadLevelFromJson(data.dungeonId);
                LevelManager.Instance.UpdateStartPosition(data.playerPosX, data.playerPosY, data.playerDirection);
            }

            // 휘발성 저장(중단 데이터)이라면 로드 후 즉시 삭제
            if (slotIndex == SUSPEND_SLOT_INDEX)
            {
                File.Delete(path);
                Debug.Log("중단 저장 데이터가 로드되어 삭제되었습니다.");
            }
            
            SceneManager.LoadScene(data.sceneName);

            Debug.Log("게임 불러오기 완료");
        }

        // 해당 슬롯의 데이터 미리보기 (UI 표시용)
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
