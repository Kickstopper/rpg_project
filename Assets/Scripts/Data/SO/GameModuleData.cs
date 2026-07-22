using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    public enum ModuleFeature
    {
        None,
        MobSensor,        // 생체 신호 감지
        AutoMapper,       // 오토맵
        LocalRadar,       // 국지 레이더
        GyroCompass,      // 자이로 나침반
        GeoScanner,       // 지형 스캐너
        KillSwitch,       // 킬 스위치 (즉시 처형)
        WeatherWidget,    // 기상 관측 모듈
        Calendar,         // 달력
    }

    [CreateAssetMenu(fileName = "NewGameModule", menuName = "Game System/Game Module")]
    public class GameModuleData : ScriptableObject
    {
        public string moduleName;
        public string description;
        public Sprite icon;
        public ModuleFeature feature;
        public Color blockColor = Color.white;
        
        [Header("Puzzle Shape Settings")]
        public List<Vector2Int> shapeBlocks = new List<Vector2Int> { new Vector2Int(0, 0) };
        
        public int blockCount => shapeBlocks.Count; 

        // 특정 회전 상태(0~3)일 때의 블럭 오프셋들을 계산하여 반환
        public List<Vector2Int> GetRotatedBlocks(int rotationState)
        {
            List<Vector2Int> rotated = new List<Vector2Int>();
            foreach (Vector2Int block in shapeBlocks)
            {
                switch (rotationState % 4)
                {
                    case 1: rotated.Add(new Vector2Int(block.y, -block.x)); break;
                    case 2: rotated.Add(new Vector2Int(-block.x, -block.y)); break;
                    case 3: rotated.Add(new Vector2Int(-block.y, block.x)); break;
                    default: rotated.Add(block); break;
                }
            }
            return rotated;
        }
    }
}