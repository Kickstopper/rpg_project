using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Data;
using System.Collections;
using UnityEngine.Networking;

namespace Manager
{
    public class DungeonManager : MonoBehaviour
    {
        public static DungeonManager Instance;

        [Header("맵 데이터 리스트 (json)")]
        public List<TextAsset> mapJsonFiles; // 인스펙터에서 할당
        
        [Header("테마 리스트 (DungeonTheme)")]
        [SerializeField] private List<DungeonTheme> allDungeonThemes; // 인스펙터에서 할당

        [Header("맵 데이터 리스트")]
        private Dictionary<string, TextAsset> mapAssetDict = new Dictionary<string, TextAsset>();

        private Dictionary<string, DungeonTheme> dungeonThemes;


        // 로드된 맵 데이터 캐싱 (ID -> MapData)
        private Dictionary<string, MapData> loadedDungeons = new Dictionary<string, MapData>();

        public MapData CurrentDungeonData { get; private set; }
        public DungeonMapState CurrentDungeonState { get; private set; }
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                InitializeDungeonThemes();
                InitializeMapAssets();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 던전 데이터 사전 초기화
        private void InitializeMapAssets()
        {
            mapAssetDict.Clear();
            foreach (var jsonAsset in mapJsonFiles)
            {
                if (jsonAsset != null && !mapAssetDict.ContainsKey(jsonAsset.name))
                    mapAssetDict.Add(jsonAsset.name, jsonAsset); // TextAsset의 name은 파일명과 정확히 일치. 확장자를 제외
            }
        }

        // 던전 테마 사전 초기화
        private void InitializeDungeonThemes()
        {
            dungeonThemes = new Dictionary<string, DungeonTheme>();
            foreach (var theme in allDungeonThemes)
            {
                if (!dungeonThemes.ContainsKey(theme.dungeonID))
                    dungeonThemes.Add(theme.dungeonID, theme);
            }
        }

        public void LoadDungeonFromJson(string fileName)
        {
            if (loadedDungeons.ContainsKey(fileName))
            {
                SetCurrentDungeonLevel(loadedDungeons[fileName]);
                return;
            }

            // 딕셔너리에서 파일명으로 에셋을 찾습니다.
            if (mapAssetDict.TryGetValue(fileName, out TextAsset asset))
            {
                string json = asset.text;
                MapData data = JsonUtility.FromJson<MapData>(json);

                if (data != null)
                {
                    loadedDungeons[fileName] = data;
                    SetCurrentDungeonLevel(data);
                }
            }
            else
            {
                Debug.LogError($"[DungeonManager] 맵 에셋을 찾을 수 없습니다: {fileName}");
            }
        }
        
        // 레벨 적용 로직
        private void SetCurrentDungeonLevel(MapData data)
        {
            CurrentDungeonData = data;

            // 맵 상태(방문 여부, 아이템 획득 등) 관리
            string mapIDKey = data.mapID; 

            DungeonMapState mapState = DungeonMapStateManager.Instance.GetMapState(mapIDKey);

            if (mapState != null)
            {
                CurrentDungeonState = mapState;
            }
            else
            {
                CurrentDungeonState = new DungeonMapState(CurrentDungeonData.width, CurrentDungeonData.height, mapIDKey);
                
                DungeonMapStateManager.Instance.RegisterDungeonMapState(CurrentDungeonState);
            }
            
            Debug.Log($"레벨 로드 완료: ID {data.mapID}, Theme {data.themeName}");
        }

        public DungeonTheme GetCurrentDungeonTheme()
        {
            if (CurrentDungeonData == null || string.IsNullOrEmpty(CurrentDungeonData.themeName)) return null;
            return GetDungeonTheme(CurrentDungeonData.themeName);
        }

        // 던전 테마 가져오기
        public DungeonTheme GetDungeonTheme(string dungeonID)
        {
            if (dungeonThemes.TryGetValue(dungeonID, out DungeonTheme theme))
            {
                return theme;
            }
            Debug.LogWarning($"테마를 찾을 수 없습니다: {dungeonID}, 기본 테마를 반환합니다.");
            return allDungeonThemes.Count > 0 ? allDungeonThemes[0] : null;
        }

        // 던전 내의 시작 좌표 업데이트
        public void UpdateDungeonStartPosition(int px, int py, Direction dir)
        {
            if (CurrentDungeonData == null) return;
            CurrentDungeonData.startX = px;
            CurrentDungeonData.startY = py;
            CurrentDungeonData.startDirection = dir;
        }
    }
}