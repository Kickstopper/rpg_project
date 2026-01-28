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

        private string SavePath => Path.Combine(Application.persistentDataPath, "savefile.json");

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

        // =========================================================
        // 저장 (Save)
        // =========================================================
        public void SaveGame()
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
            // CombatManager의 activePlayers나 PartyManager의 리스트를 참조
            foreach (var entity in CombatManager.Instance.activePlayers)
            {
                if (entity is PlayerController pc)
                {
                    data.partyMembers.Add(pc.ToSaveData()); 
                }
            }

            // 4. 이벤트 플래그 저장 (FlagManager가 있다면 가져옴)
            data.eventFlags = FlagManager.Instance.GetSaveData();

            // 파일 쓰기
            string json = JsonUtility.ToJson(data, true); // true는 보기 좋게 들여쓰기
            File.WriteAllText(SavePath, json);
            
            Debug.Log($"게임 저장 완료: {SavePath}");
        }

        // =========================================================
        // 불러오기 (Load)
        // =========================================================
        public void LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("저장된 파일이 없습니다.");
                return;
            }

            string json = File.ReadAllText(SavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // 1. 골드 및 인벤토리 복구
            InventoryManager.Instance.SetGold(data.gold);
            InventoryManager.Instance.LoadFromSaveData(data.inventory);

            FlagManager.Instance.LoadFromSaveData(data.eventFlags);

            // 2. 파티원 복구
            // 기존 파티 클리어 후 재생성 로직 필요
            PartyManager.Instance.LoadFromSave(data.partyMembers);

            // 3. 씬 이동 및 위치 복구
            if (data.sceneName == GameScene.DUNGEON_MAP_SCENE)
            {
                LevelManager.Instance.LoadLevelFromJson(data.dungeonId);
                LevelManager.Instance.UpdateStartPosition(data.playerPosX, data.playerPosY, data.playerDirection);
            }
            
            SceneManager.LoadScene(data.sceneName);

            Debug.Log("게임 불러오기 완료");
        }
    }
    
}
