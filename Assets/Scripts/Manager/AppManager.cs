using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Manager
{
    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance;

        [Header("Configuration")]
        public int maxMemory = 10;

        [Header("Reference Data (Drag All SOs here)")]
        public List<GameAppData> appDatabase; 

        private Dictionary<AppFeature, GameAppData> _appLookup;

        [Header("Player State (Save This!)")]
        public List<AppFeature> ownedFeatures = new List<AppFeature>();
        public List<PlacedAppData> installedApps = new List<PlacedAppData>();

        public int gridWidth = 5;
        public int gridHeight = 5;
        private AppFeature[,] memoryBoard;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                _appLookup = new Dictionary<AppFeature, GameAppData>();
                foreach (var app in appDatabase)
                {
                    if (!_appLookup.ContainsKey(app.feature))
                        _appLookup.Add(app.feature, app);
                }

                memoryBoard = new AppFeature[gridWidth, gridHeight];

                // 인스펙터의 기본 앱들을 검증하며 배치. 인스펙터에 지정된 리스트를 임시 복사하고 원본은 비움
                List<PlacedAppData> defaultApps = new List<PlacedAppData>(installedApps);
                installedApps.Clear();

                foreach (var app in defaultApps)
                {
                    if (app.feature == AppFeature.None) continue;

                    // 인스펙터에 지정된 좌표에 설치가 가능한지 확인
                    if (CanPlaceApp(app.feature, app.x, app.y))
                    {
                        PlaceApp(app.feature, app.x, app.y);
                    }
                    else
                    {
                        // 겹친다면 자동으로 빈 공간을 찾음 (전부 x=0, y=0 인 경우)
                        if (FindAutoPlaceCoordinate(app.feature, out Vector2Int newPos))
                        {
                            PlaceApp(app.feature, newPos.x, newPos.y);
                        }
                        else
                        {
                            Debug.LogWarning($"[AppManager] {app.feature}를 설치할 메모리 공간이 부족하여 기본 배치에서 제외되었습니다.");
                        }
                    }
                }

                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 빈 공간을 찾아 좌표를 반환하는 함수
        private bool FindAutoPlaceCoordinate(AppFeature feature, out Vector2Int pos)
        {
            pos = Vector2Int.zero;
            GameAppData data = GetAppData(feature);
            if (data == null) return false;

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (CanPlaceApp(feature, x, y))
                    {
                        pos = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            return false;
        }

        public void LoadGame(SaveData data)
        {
            maxMemory = data.maxAppMemory;

            ownedFeatures.Clear();
            if (data.ownedApps != null) ownedFeatures.AddRange(data.ownedApps);

            installedApps.Clear();
            memoryBoard = new AppFeature[gridWidth, gridHeight];

            if (data.placedApps != null)
            {
                foreach (var app in data.placedApps)
                {
                    if (_appLookup.ContainsKey(app.feature)) 
                    {
                        PlaceApp(app.feature, app.x, app.y, app.rotation); 
                    }
                }
            }
        }

        // 지정된 좌표에 앱을 설치할 수 있는지 검사
        public bool CanPlaceApp(AppFeature feature, int startX, int startY, int rotation = 0)
        {
            GameAppData data = GetAppData(feature);
            if (data == null) return false;

            // 회전된 블럭 오프셋을 가져와서 검사
            List<Vector2Int> blocksToCheck = data.GetRotatedBlocks(rotation);
            
            foreach (Vector2Int blockOffset in blocksToCheck)
            {
                int checkX = startX + blockOffset.x;
                int checkY = startY + blockOffset.y;

                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                    return false;

                if (memoryBoard[checkX, checkY] != AppFeature.None)
                    return false;
            }
            return true;
        }

        // 앱 설치
        public void PlaceApp(AppFeature feature, int startX, int startY, int rotation = 0)
        {
            if (!CanPlaceApp(feature, startX, startY, rotation)) return;

            GameAppData data = GetAppData(feature);
            List<Vector2Int> blocksToPlace = data.GetRotatedBlocks(rotation);

            foreach (Vector2Int blockOffset in blocksToPlace)
            {
                memoryBoard[startX + blockOffset.x, startY + blockOffset.y] = feature;
            }
            
            installedApps.Add(new PlacedAppData { feature = feature, x = startX, y = startY, rotation = rotation });
        }

        public void Uninstall(AppFeature feature)
        {
            var appToRemove = installedApps.FirstOrDefault(a => a.feature == feature);
            if (appToRemove != null)
            {
                installedApps.Remove(appToRemove);
                
                GameAppData data = GetAppData(feature);
                
                // 설치 당시의 rotation 값을 가져와 회전된 블록 배열을 기준으로 보드를 비움
                foreach (Vector2Int blockOffset in data.GetRotatedBlocks(appToRemove.rotation))
                {
                    memoryBoard[appToRemove.x + blockOffset.x, appToRemove.y + blockOffset.y] = AppFeature.None;
                }
            }
        }
        public bool IsInstalled(AppFeature feature)
        {
            return installedApps.Any(app => app.feature == feature);
        }

        public List<PlacedAppData> GetPlacedApps()
        {
            return installedApps; 
        }
        
        public GameAppData GetAppData(AppFeature feature)
        {
            if (_appLookup.TryGetValue(feature, out GameAppData data)) return data;
            return null;
        }

        // 인스펙터 우클릭 메뉴에 자동 배치 버튼 생성
        [ContextMenu("빈 공간에 자동 배치 (Auto Arrange)")]
        public void AutoArrangeDefaultApps()
        {
            AppFeature[,] tempBoard = new AppFeature[gridWidth, gridHeight];
            Dictionary<AppFeature, GameAppData> tempLookup = new Dictionary<AppFeature, GameAppData>();
            
            foreach (var appData in appDatabase)
            {
                if (appData != null && !tempLookup.ContainsKey(appData.feature))
                {
                    tempLookup.Add(appData.feature, appData);
                }
            }

            // 인스펙터의 installedApps를 순회하며 자리 찾기
            for (int i = 0; i < installedApps.Count; i++)
            {
                PlacedAppData currentApp = installedApps[i];
                
                if (currentApp.feature == AppFeature.None) continue;
                
                if (!tempLookup.ContainsKey(currentApp.feature))
                {
                    Debug.LogWarning($"[AppManager] '{currentApp.feature}' 데이터를 appDatabase에서 찾을 수 없습니다.");
                    continue;
                }

                GameAppData data = tempLookup[currentApp.feature];
                bool isPlaced = false;

                // 상단 왼쪽부터 우측 하단 방향으로 스캔
                for (int y = 0; y < gridHeight; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        if (CanPlaceAppInTempBoard(data, x, y, tempBoard))
                        {
                            // 위치 지정
                            currentApp.x = x;
                            currentApp.y = y;
                            installedApps[i] = currentApp; // 변경된 좌표 적용

                            // 다음 앱이 겹치지 않도록 임시 보드에 블록 채우기
                            foreach (Vector2Int block in data.shapeBlocks)
                            {
                                tempBoard[x + block.x, y + block.y] = currentApp.feature;
                            }
                            
                            isPlaced = true;
                            break;
                        }
                    }
                    if (isPlaced) break;
                }

                if (!isPlaced)
                {
                    Debug.LogError($"[AppManager] '{currentApp.feature}'를 배치할 공간이 부족합니다! (Grid 한도 초과)");
                }
            }

            Debug.Log("[AppManager] 기본 앱 자동 배치 완료!");

            // 씬을 저장할 때 함께 저장되도록 마킹
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        // 임시 보드용 충돌 검사 함수
        private bool CanPlaceAppInTempBoard(GameAppData data, int startX, int startY, AppFeature[,] tempBoard)
        {
            foreach (Vector2Int blockOffset in data.shapeBlocks)
            {
                int checkX = startX + blockOffset.x;
                int checkY = startY + blockOffset.y;

                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                    return false;

                if (tempBoard[checkX, checkY] != AppFeature.None)
                    return false;
            }
            return true;
        }

        // 현재 installedApps 리스트를 바탕으로 memoryBoard 배열을 강제로 재작성
        public void SyncMemoryBoardWithInstalledApps()
        {
            for (int y = 0; y < gridHeight; y++)
                for (int x = 0; x < gridWidth; x++)
                    memoryBoard[x, y] = AppFeature.None;

            foreach (var placedApp in installedApps)
            {
                GameAppData data = GetAppData(placedApp.feature);
                if (data == null) continue;

                List<Vector2Int> blocksToSync = data.GetRotatedBlocks(placedApp.rotation);

                foreach (Vector2Int blockOffset in blocksToSync)
                {
                    int targetX = placedApp.x + blockOffset.x;
                    int targetY = placedApp.y + blockOffset.y;

                    if (targetX >= 0 && targetX < gridWidth && targetY >= 0 && targetY < gridHeight)
                    {
                        memoryBoard[targetX, targetY] = placedApp.feature;
                    }
                }
            }
        }
    }
}
