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
        KillSwitch        // 즉시 처형
    }
    
    [CreateAssetMenu(fileName = "NewGameApp", menuName = "Game System/Game App")]
    public class GameAppData : ScriptableObject
    {
        [Header("Basic Info")]
        public string appName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("System Settings")]
        public AppFeature feature;
        [Range(1, 20)]
        public int memoryCost; // 차지하는 메모리 블럭 수
    }
}