using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;
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

        public int gridWidth = 4;
        public int gridHeight = 4;
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
            
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            
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
                        PlaceApp(app.feature, app.x, app.y); 
                    }
                }
            }
        }

        // 지정된 좌표에 앱을 설치할 수 있는지 검사
        public bool CanPlaceApp(AppFeature feature, int startX, int startY)
        {
            GameAppData data = GetAppData(feature);
            
            foreach (Vector2Int blockOffset in data.shapeBlocks)
            {
                int checkX = startX + blockOffset.x;
                int checkY = startY + blockOffset.y;

                // 보드 경계 체크
                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                    return false;

                // 해당 위치에 이미 설치된 앱이 있는지 체크
                if (memoryBoard[checkX, checkY] != AppFeature.None)
                    return false;
            }
            return true;
        }

        // 앱 설치
        public void PlaceApp(AppFeature feature, int startX, int startY)
        {
            if (!CanPlaceApp(feature, startX, startY)) return;

            GameAppData data = GetAppData(feature);
            foreach (Vector2Int blockOffset in data.shapeBlocks)
            {
                memoryBoard[startX + blockOffset.x, startY + blockOffset.y] = feature;
            }
            
            installedApps.Add(new PlacedAppData { feature = feature, x = startX, y = startY });
        }

        public void Uninstall(AppFeature feature)
        {
            var appToRemove = installedApps.FirstOrDefault(a => a.feature == feature);
            if (appToRemove != null)
            {
                installedApps.Remove(appToRemove);
                
                // 보드에서 해당 앱의 모든 블록 지우기
                GameAppData data = GetAppData(feature);
                foreach (Vector2Int blockOffset in data.shapeBlocks)
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
    }
}
