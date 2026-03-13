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

        // 회전 상태일 때의 블럭 오프셋들을 반환
        public List<Vector2Int> GetRotatedBlocks(int rotationState)
        {
            List<Vector2Int> rotated = new List<Vector2Int>();
            
            foreach (Vector2Int block in shapeBlocks)
            {
                // 90도 단위 회전 행렬 공식 적용
                switch (rotationState % 4)
                {
                    case 1: // 90도 시계방향
                        rotated.Add(new Vector2Int(block.y, -block.x)); break;
                    case 2: // 180도
                        rotated.Add(new Vector2Int(-block.x, -block.y)); break;
                    case 3: // 270도 (90도 반시계)
                        rotated.Add(new Vector2Int(-block.y, block.x)); break;
                    default: // 0도 (기본)
                        rotated.Add(block); break;
                }
            }
            return rotated;
        }
    }
}