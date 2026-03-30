using UnityEngine;
using Data;
using System;

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
        public bool useOrganicEffect = false;
        public float organicFreqX = 4f;   // 벽 수평 노이즈 빈도
        public float organicSpeed = 0.3f;   // 꿈틀거림 속도
        [Range(0, 1f)]public float organicBreath    = 0.35f;   // amplitude 자체가 숨쉬는 강도
        [Range(-1f, 1f)] public float organicAmplitude = 0.2f;  // 최대 거리 왜곡 강도
        
        public bool useMeltEffect = false;
        public float meltEdgeBump = 1f;
        public float meltEdgeSpeed = 0.3f;   // 흘러내림 속도

        [NonSerialized] public float animTime; // Time.time

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

        [Header("Floor")]
        public float voidDepthScale = 3.0f; // 클수록 구멍이 더 밝고 얕아 보임
        public float voidWallHeightScale = 1f; // 구멍 벽 높이 비율 (낮을수록 깊어 보임)
        public int voidWallTexIdx = 0; // 
    }

    // 스프라이트 정렬용 구조체
    public struct SpriteSortInfo
    {
        public int index;
        public float distance;
    }
}