using System.Collections.Generic;
using UnityEngine;
using Data;
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
        public List<AppFeature> installedFeatures = new List<AppFeature>();

        // 현재 메모리 사용량 계산
        public int CurrentUsedMemory
        {
            get
            {
                int total = 0;
                foreach (var feature in installedFeatures)
                {
                    if (_appLookup.ContainsKey(feature))
                        total += _appLookup[feature].memoryCost;
                }
                return total;
            }
        }

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

            // 소유 목록 복구
            ownedFeatures.Clear();
            if (data.ownedApps != null)
            {
                ownedFeatures.AddRange(data.ownedApps);
            }

            // 설치 목록 복구
            installedFeatures.Clear();
            if (data.installedApps != null)
            {
                foreach (var feature in data.installedApps)
                {
                    // 세이브 파일에는 있지만, 삭제된 기능일 수도 있으니 유효성 검사
                    if (_appLookup.ContainsKey(feature)) 
                    {
                        installedFeatures.Add(feature);
                    }
                }
            }
            
        }

        // 설치
        public bool TryInstall(AppFeature feature)
        {
            if (!_appLookup.ContainsKey(feature)) return false;
            
            if (installedFeatures.Contains(feature)) return true;

            GameAppData data = _appLookup[feature];

            // 메모리 체크
            if (CurrentUsedMemory + data.memoryCost <= maxMemory)
            {
                installedFeatures.Add(feature);
                Debug.Log($"Installed: {feature} (Mem: {CurrentUsedMemory}/{maxMemory})");
                return true;
            }
            
            Debug.Log("Memory Full!");
            return false;
        }

        // 제거
        public void Uninstall(AppFeature feature)
        {
            if (installedFeatures.Contains(feature))
            {
                installedFeatures.Remove(feature);
            }
        }

        public bool IsInstalled(AppFeature feature)
        {
            return installedFeatures.Contains(feature);
        }
        
        // UI나 다른 곳에서 정보가 필요할 때
        public GameAppData GetAppData(AppFeature feature)
        {
            if (_appLookup.TryGetValue(feature, out GameAppData data))
                return data;
            return null;
        }
    }
}
