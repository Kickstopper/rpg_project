using UnityEngine;
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance;
        public bool isLoadGame = false;
        public Vector3 loadedPosition;

        public Transform currentPlayerTransform;

        [Header("테마 리스트 (이름으로 검색용)")]
        [SerializeField] private List<RegionTheme> allRegions;
        
        private Dictionary<string, RegionTheme> regionThemes;

        public RegionTheme currentRegionTheme { get; private set;}

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                InitializeRegionThemes();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 테마 리스트를 딕셔너리로 변환 (빠른 검색)
        private void InitializeRegionThemes()
        {
            regionThemes = new Dictionary<string, RegionTheme>();
            foreach(var theme in allRegions)
            {
                if (!regionThemes.ContainsKey(theme.regionID))
                {
                    regionThemes.Add(theme.regionID, theme);
                }
            }
        }

        public void SetCurrentRegionTheme(string regionID)
        {
            if (regionThemes.TryGetValue(regionID, out RegionTheme theme))
            {
                currentRegionTheme = theme;
            }
            else
            {
                currentRegionTheme = allRegions.Count > 0 ? allRegions[0] : null;
            }
        }
    }
}