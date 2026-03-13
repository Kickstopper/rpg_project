using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    public enum AppFeature
    {
        None,
        MobSensor,        // 생체 신호 감지
        AutoMapper,       // Auto-map
        LocalRadar,       // Mini-map
        GyroCompass,      // 나침반
        GeoScanner,       // 지형 스캐너
        KillSwitch,       // 즉시 처형
        WeatherWidget,    // 날씨 위젯
    }
    
    [CreateAssetMenu(fileName = "NewGameApp", menuName = "Game System/Game App")]
    public class GameAppData : ScriptableObject
    {
        public string appName;
        public string description;
        public Sprite icon;
        public AppFeature feature;
        
        [Header("Puzzle Shape Settings")]
        [HideInInspector]
        public List<Vector2Int> shapeBlocks = new List<Vector2Int> { new Vector2Int(0, 0) };
        public Color blockColor = Color.white;
        public int memoryCost => shapeBlocks.Count; 
    }
}