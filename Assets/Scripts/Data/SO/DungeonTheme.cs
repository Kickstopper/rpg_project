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

    [System.Serializable]
    public class DoorAnimConfig
    {
        public int closedTexId;         // 닫혀있을 때의 기본 문 텍스처 ID
        public int[] openFrameTexIds;   // 문이 열릴 때 순서대로 교체될 텍스처 ID 배열
        public float animSpeed = 0.1f;  // 한 프레임당 걸리는 시간
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
        [Tooltip("파일명과 반드시 일치해야 DungeonMapEditor에서 json을 로드할 때 자동으로 연결됨")]
        public string themeID;
        [Header("MUSIC")]
        public BgmID bgmID;                  // 이 레벨에서 재생될 BGM
        
        [Header("Entry Effect")]
        public bool useWakeUpEffect = false;

        [Header("Battle Settings")]
        public List<string> monsterList;     // 출현하는 몬스터의 ID 목록
        public int maxEnemyCount = 2;        // 매 전투에 출현하는 적의 최대 수
        public EncounterMode encounterMode = EncounterMode.Symbol;
        public int maxSpawnCount = 3;        // 심볼 인카운터 최대 스폰 수
        public int spawnDelay = 5;           // 심볼 인카운터 스폰 간격
        
        public Texture2D background; // 던전 아래에 깔릴 스카이박스
        public Texture2D[] texture;
        
        public int ceilingTexIdx = 1; // 천장 텍스처 인덱스
        public int floorTexIdx = 2;   // 바닥 텍스처 인덱스

        [Header("특수 벽 설정")]
        [Tooltip("텍스처는 보이지만 통과 가능한 텍스처 ID 목록")]
        public List<int> passableWallTexIDs = new List<int>();

        [Header("Door Settings")]
        public List<DoorAnimConfig> doorAnimations; // 맵에 존재하는 문들의 세팅 리스트

        [Header("Animations")]
        public List<WallAnimConfig> wallAnimations; // 테마별 애니메이션 설정
        
        public ObjectSpriteData[] objectSprites;

        [Header("Environment Options")]
        public Color fogColor = Color.black; // 심도 표현을 위한 안개의 색
        public float lightingIntensity = 3.5f;
        public bool useGridLighting = true;
        public bool moduleEnable = true;     // 모듈을 사용할 수 있는지 여부

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

        [Header("WaterEffect")]
        public bool isUnderwater;

        [Header("Anaglyph")]
        [Range(0.03f, 0.07f)] public float stereoSeparation = 0.05f;
    }
}
