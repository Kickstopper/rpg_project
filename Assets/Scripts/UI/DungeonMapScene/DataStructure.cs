using UnityEngine;
using Data;

namespace UI.DungeonMapScene
{
    // 타일 애니메이션 상태
    public class TileAnimState
    {
        public bool isAnimating;    // 애니메이션 대상인가?
        public bool showAlt;        // 현재 B 텍스처를 보여줄 차례인가?
        public float timer;         // 다음 변경까지 남은 시간
        public WallAnimConfig config; // 참조할 설정
    }

    // 렌더링 설정 (인스펙터 정리용)
    [System.Serializable]
    public class RenderSettings
    {
        [Header("Display")]
        public Material screenMaterial;
        public int screenWidth = 512;
        public int screenHeight = 256;
        public Vector2 screenScale = new Vector2(2.5f, 2.8125f);
        
        [Header("Visual Effects")]
        public bool useWallDistortion = false;
        public float distortionFreq = 0.5f;
        public float distortionAmp = 2.0f;
        public bool useCylinderEffect = false;
        [Range(-10f, 10f)] public float cylinderStrength = 3.0f;
        [Range(0.03f, 0.07f)] public float stereoSeparation = 0.05f;

        [Header("Lighting")]
        public float lightingIntensity = 3.5f;
        public bool useGridLighting = true;

        [Header("Scanner Effect")]
        public Color32 wireframeColor = Color.green;
        public Color32 floorWireframeColor = new Color(0f, 0.5f, 0f);
        public Color32 pulseColor = Color.white;
        public float scanSpeed = 15.0f;
        public float maxScanDistance = 20.0f;
        public float pulseWidth = 0.5f;
        public float scanWaitTime = 2.0f;
        public float returnSpeedMultiplier = 1.5f;
    }

    // 스프라이트 정렬용 구조체
    public struct SpriteSortInfo
    {
        public int index;
        public float distance;
    }
}