using UnityEngine;
using System.Collections.Generic;
namespace Data
{
    [System.Serializable]
    public struct WallAnimConfig
    {
        public string name;         
        public int[] frameTexIDs;
        public float minInterval;   
        public float maxInterval;   
    }

    public enum EncounterMode { Random, Symbol }

    [System.Serializable]
    public struct ObjectSpriteData
    {
        public int objectID;           // _staticObjects의 ID와 매핑
        public Texture2D texture;      // 기본 텍스처
        public bool isObstacle;        // 통과 가능 여부
    }

    [CreateAssetMenu(fileName = "NewTheme", menuName = "Dungeon/DungeonTheme")]
    public class DungeonTheme : ScriptableObject
    {
        public string dungeonID;
        
        [Header("Entry Effect")]
        public bool useWakeUpEffect = false;

        [Header("Battle Encounter Mode")]
        public EncounterMode encounterMode = EncounterMode.Symbol;
        
        public Texture2D background; // 던전 아래에 깔릴 스카이박스
        public Texture2D[] texture;
        
        public int floorTexIdx = 1;   // 바닥 텍스처 인덱스
        public int ceilingTexIdx = 2; // 천장 텍스처 인덱스
        
        public Sprite[] enemySprites;
        public ObjectSpriteData[] objectSprites;

        public int maxSpawnCount = 3;
        public int spawnDelay = 5;

        [Header("Animations")]
        public List<WallAnimConfig> wallAnimations; // 테마별 애니메이션 설정

        [Header("Environment Options")]
        public Color fogColor = Color.black; // 심도 표현을 위한 안개의 색
        public float lightingIntensity = 3.5f;
        public bool useGridLighting = true;
        public BgmID bgmID;                  // 이 레벨에서 재생될 BGM
        public bool moduleEnable = true;     // 모듈을 사용할 수 있는지 여부
        public List<string> monsterList;     // 출현하는 몬스터의 ID 목록

        [Header("Dust Effect")]
        public bool useDustEffect = false;
        public int dustParticleCount = 300;     
        public float dustSwayAmplitude = 0.01f; 
        public bool dustMovesUp = false; 
        public bool useDustTwinkle = true; 
        public float dustTwinkleSpeed = 10.0f; // 반짝이는 속도 (높을수록 빠르게 깜빡임)
        public Color32 dustColor = new Color32(220, 210, 180, 255);

        [Header("OrganicEffect")]
        public bool useOrganicEffect = false;
        public float organicFreqX = 4f;   // 벽 수평 노이즈 빈도
        public float organicSpeed = 0.3f;   // 꿈틀거림 속도
        [Range(0, 1f)]public float organicBreath    = 0.35f;   // amplitude 자체가 숨쉬는 강도
        [Range(-1f, 1f)] public float organicAmplitude = 0.2f;  // 최대 거리 왜곡 강도
        
        [Header("MeltEffect")]
        public bool useMeltEffect = false;
        public float meltEdgeBump = 1f;
        public float meltEdgeSpeed = 0.3f;   // 흘러내림 속도

        [Header("WallDistortionEffect")]
        public bool useWallDistortion = false;
        public float distortionFreq = 0.5f;
        public float distortionAmp = 2.0f;

        [Header("CylinderEffect")]
        public bool useCylinderEffect = false;
        [Range(-10f, 10f)] public float cylinderStrength = 3.0f;

        [Header("Anaglyph")]
        [Range(0.03f, 0.07f)] public float stereoSeparation = 0.05f;
    }
}
