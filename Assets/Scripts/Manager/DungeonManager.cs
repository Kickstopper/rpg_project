using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Data;

namespace Manager
{
    public class DungeonManager : MonoBehaviour
    {
        public static DungeonManager Instance;

        [Header("테마 리스트 (이름으로 검색용)")]
        [SerializeField] private List<DungeonTheme> allDungeons;
        
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
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 테마 리스트를 딕셔너리로 변환 (빠른 검색)
        private void InitializeDungeonThemes()
        {
            dungeonThemes = new Dictionary<string, DungeonTheme>();
            foreach (var theme in allDungeons)
            {
                if (!dungeonThemes.ContainsKey(theme.dungeonID))
                {
                    dungeonThemes.Add(theme.dungeonID, theme);
                }
            }
        }

        // JSON 파일 로드 함수
        // 파일 위치: Assets/StreamingAssets/Levels/{fileName}.json
        public void LoadDungeonFromJson(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Levels", fileName + ".json");

            if (loadedDungeons.ContainsKey(path))
            {
                SetCurrentDungeonLevel(loadedDungeons[path]);
                return;
            }
                
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                
                // JSON -> MapData 변환
                MapData data = JsonUtility.FromJson<MapData>(json);

                if (data != null)
                {
                    loadedDungeons[path] = data;
                    SetCurrentDungeonLevel(data);
                }
            }
            else
            {
                Debug.LogError($"[DungeonManager] JSON 파일을 찾을 수 없습니다: {path}");
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

        // 던전 테마 가져오기
        public DungeonTheme GetDungeonTheme(string dungeonID)
        {
            if (dungeonThemes.TryGetValue(dungeonID, out DungeonTheme theme))
            {
                return theme;
            }
            Debug.LogWarning($"테마를 찾을 수 없습니다: {dungeonID}, 기본 테마를 반환합니다.");
            return allDungeons.Count > 0 ? allDungeons[0] : null;
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