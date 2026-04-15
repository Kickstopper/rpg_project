using UnityEngine;
using System.Collections.Generic;
namespace Data
{
    [System.Serializable]
    public struct WallAnimConfig
    {
        public string name;         
        public int baseTexId;       // 기본 텍스처 ID
        public int altTexId;        // 바뀔 텍스처 ID
        public float minInterval;   
        public float maxInterval;   
    }

    [CreateAssetMenu(fileName = "NewTheme", menuName = "Dungeon/DungeonTheme")]
    public class DungeonTheme : ScriptableObject
    {
        [Header("텍스처 설정")]
        public string dungeonID;
        
        [Header("Entry Effect")]
        public bool useWakeUpEffect = false;
        
        public Texture2D background; // 던전 아래에 깔릴 스카이박스
        public Texture2D[] texture;
        
        public int floorTexIdx = 1;   // 바닥 텍스처 인덱스
        public int ceilingTexIdx = 2; // 천장 텍스처 인덱스
        
        public Sprite[] spriteTextures;
        public int maxSpawnCount = 3;
        public int spawnDelay = 5;

        [Header("Animations")]
        public List<WallAnimConfig> wallAnimations; // 테마별 애니메이션 설정

        [Header("환경 설정")]
        public Color fogColor = Color.black; // 심도 표현을 위한 안개의 색
        public float lightingIntensity = 3.5f;
        public bool useGridLighting = true;
        public BgmID bgmID;                  // 이 레벨에서 재생될 BGM
        public bool moduleEnable = true;     // 모듈을 사용할 수 있는지 여부
        public List<string> monsterList;     // 출현하는 몬스터의 ID 목록
    }
}
