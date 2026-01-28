using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Data;

namespace Manager
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance;

        [Header("테마 리스트 (이름으로 검색용)")]
        [SerializeField] private List<DungeonTheme> allThemes;
        private Dictionary<string, DungeonTheme> themeMap;

        // 로드된 맵 데이터 캐싱 (ID -> MapData)
        private Dictionary<int, MapData> loadedMaps = new Dictionary<int, MapData>();

        public MapData CurrentMapData { get; private set; }
        public DungeonMapState CurrentMapState { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                InitializeThemeMap();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 1. 테마 리스트를 딕셔너리로 변환 (빠른 검색)
        private void InitializeThemeMap()
        {
            themeMap = new Dictionary<string, DungeonTheme>();
            foreach (var theme in allThemes)
            {
                if (!themeMap.ContainsKey(theme.themeName))
                {
                    themeMap.Add(theme.themeName, theme);
                }
            }
        }

        // 2. JSON 파일 로드 함수
        // 파일 위치: Assets/StreamingAssets/Levels/{fileName}.json
        public void LoadLevelFromJson(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Levels", fileName + ".json");

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                
                // JSON -> MapData 변환
                MapData data = JsonUtility.FromJson<MapData>(json);

                if (data != null)
                {
                    SetCurrentLevel(data);
                }
            }
            else
            {
                Debug.LogError($"[LevelManager] JSON 파일을 찾을 수 없습니다: {path}");
            }
        }

        // 3. 레벨 적용 로직
        private void SetCurrentLevel(MapData data)
        {
            CurrentMapData = data;

            // 맵 상태(방문 여부, 아이템 획득 등) 관리
            // ID를 string 대신 int(MapData.mapID)를 쓰거나, string으로 변환해서 사용
            string mapIDKey = data.mapID; 

            var mapState = GameManager.Instance.GetDungeonMapState(mapIDKey);
            
            if (mapState != null) 
            {
                CurrentMapState = mapState;
            }
            else
            {
                // 새 상태 생성
                CurrentMapState = new DungeonMapState(data.width, data.height, mapIDKey);
                GameManager.Instance.AddDungeonMapState(mapIDKey, CurrentMapState);
            }
            
            // 플레이어 시작 위치 설정 (MapData에 있는 정보 활용)
            // GameManager나 PlayerController에 시작 위치 전달
            // 예: GameManager.Instance.SetPlayerStart(data.startX, data.startY, data.startDirection);

            Debug.Log($"레벨 로드 완료: ID {data.mapID}, Theme {data.themeName}");
        }

        // 4. 테마 가져오기 헬퍼 함수
        public DungeonTheme GetTheme(string themeName)
        {
            if (themeMap.TryGetValue(themeName, out DungeonTheme theme))
            {
                return theme;
            }
            Debug.LogWarning($"테마를 찾을 수 없습니다: {themeName}, 기본 테마를 반환합니다.");
            return allThemes.Count > 0 ? allThemes[0] : null;
        }

        public void UpdateStartPosition(int px, int py, Direction dir)
        {
            if (CurrentMapData == null) return;
            CurrentMapData.startX = px;
            CurrentMapData.startY = py;
            CurrentMapData.startDirection = dir;
        }
    }
}