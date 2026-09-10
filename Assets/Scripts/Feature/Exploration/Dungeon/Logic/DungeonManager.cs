using UnityEngine;
using System.Collections.Generic;
using RPGProject.Core;

namespace RPGProject.Feature.Exploration
{
    public class DungeonManager : MonoBehaviour
    {
        [Header("맵 에디터 카탈로그 (선택)")]
        [SerializeField] private DungeonMapCatalog mapCatalog;

        [Header("맵 데이터 리스트 (json)")]
        public List<TextAsset> mapJsonFiles; // 인스펙터에서 할당
        
        [Header("엘리베이터 세팅")]
        public List<ElevatorData> elevatorList = new List<ElevatorData>();

        // ID로 엘리베이터 데이터를 찾는 헬퍼 메서드
        public ElevatorData GetElevatorData(string id)
        {
            return elevatorList.Find(e => e.id == id);
        }
        
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
            InitializeDungeonThemes();
            InitializeMapAssets();
        }

        // 던전 데이터 사전 초기화
        private void InitializeMapAssets()
        {
            mapAssetDict.Clear();
            // 기존 Inspector 설정을 유지하고 카탈로그의 새 맵을 추가합니다.
            AddMapAssets(mapJsonFiles);
            if (mapCatalog != null) AddMapAssets(mapCatalog.maps);
        }

        private void AddMapAssets(IEnumerable<TextAsset> assets)
        {
            if (assets == null) return;
            foreach (var asset in assets)
            {
                if (asset == null) continue;
                if (mapAssetDict.TryGetValue(asset.name, out var existing))
                {
                    if (existing != asset)
                        Debug.LogError($"[DungeonManager] 중복 맵 파일명: {asset.name}");
                    continue;
                }
                mapAssetDict.Add(asset.name, asset);
            }
        }

        private void InitializeDungeonThemes()
        {
            dungeonThemes = new Dictionary<string, DungeonTheme>();
            AddThemes(allDungeonThemes);
            if (mapCatalog != null) AddThemes(mapCatalog.themes);
        }

        private void AddThemes(IEnumerable<DungeonTheme> themes)
        {
            if (themes == null) return;
            foreach (var theme in themes)
            {
                if (theme == null || string.IsNullOrWhiteSpace(theme.themeID)) continue;
                if (dungeonThemes.TryGetValue(theme.themeID, out var existing))
                {
                    if (existing != theme)
                        Debug.LogError($"[DungeonManager] 중복 테마 ID: {theme.themeID}");
                    continue;
                }
                dungeonThemes.Add(theme.themeID, theme);
            }
        }
        
        // JSON 파일을 읽지 않고, 코드에서 생성된 MapData를 메모리에 직접 로드
        public void LoadDynamicDungeon(MapData dynamicMapData)
        {
            if (dynamicMapData == null)
            {
                Debug.LogError("[DungeonManager] 전달된 다이내믹 맵 데이터가 Null입니다.");
                return;
            }

            // 현재 던전 데이터를 동적 생성된 데이터로 덮어쓰기
            CurrentDungeonData = dynamicMapData;

            // 던전 진행 상태 동기화
            if (ManagerRoot.DungeonMapState != null)
            {
                // 이미 생성된 MapState가 있는지 확인
                if (!ManagerRoot.DungeonMapState.HasState(dynamicMapData.mapID))
                {
                    // 없다면 랜덤 맵의 ID와 크기에 맞추어 새로운 MapState를 등록
                    ManagerRoot.DungeonMapState.CreateNewState(dynamicMapData.mapID, dynamicMapData.width, dynamicMapData.height);
                }
                
                // 현재 상태를 방금 생성한 랜덤 MapState로 연결
                CurrentDungeonState = ManagerRoot.DungeonMapState.GetMapState(dynamicMapData.mapID);
            }

            Debug.Log($"[DungeonManager] 다이내믹 맵 로드 완료: {dynamicMapData.mapID} ({dynamicMapData.width}x{dynamicMapData.height})");
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

            DungeonMapState mapState = ManagerRoot.DungeonMapState.GetMapState(mapIDKey);

            if (mapState != null)
            {
                CurrentDungeonState = mapState;
            }
            else
            {
                CurrentDungeonState = new DungeonMapState(CurrentDungeonData.width, CurrentDungeonData.height, mapIDKey);
                
                ManagerRoot.DungeonMapState.RegisterDungeonMapState(CurrentDungeonState);
            }
            
            Debug.Log($"레벨 로드 완료: ID {data.mapID}, Theme {data.themeID}");
        }

        public DungeonTheme GetCurrentDungeonTheme()
        {
            if (CurrentDungeonData == null || string.IsNullOrEmpty(CurrentDungeonData.themeID)) return null;
            return GetDungeonTheme(CurrentDungeonData.themeID);
        }

        // 던전 테마 가져오기
        public DungeonTheme GetDungeonTheme(string themeID)
        {
            if (dungeonThemes.TryGetValue(themeID, out DungeonTheme theme))
            {
                return theme;
            }
            Debug.LogWarning($"테마를 찾을 수 없습니다: {themeID}, 기본 테마를 반환합니다.");
            if (allDungeonThemes != null)
                foreach (var fallback in allDungeonThemes) if (fallback != null) return fallback;
            if (mapCatalog != null && mapCatalog.themes != null)
                foreach (var fallback in mapCatalog.themes) if (fallback != null) return fallback;
            return null;
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