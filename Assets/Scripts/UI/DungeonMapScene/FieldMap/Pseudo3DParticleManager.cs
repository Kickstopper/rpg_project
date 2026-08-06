using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class Pseudo3DParticleManager : MonoBehaviour
    {
        class ParticleItem
        {
            public RectTransform rect;
            public Image img;
            public float currentZ;
            
            public float randomX; // 좌우 퍼짐 정도
            public float randomY; // 바닥으로부터의 높이
            public float speedMultiplier; 
            public float rotationSpeed; // 회전 속도 (먼지 모드용)
        }

        [Header("References")]
        public Pseudo3DRoad roadScroller;
        public RectTransform container;
        [Tooltip("흰색 정사각형(또는 먼지 텍스처)이 들어간 UI Image 프리팹")]
        public GameObject particlePrefab;

        [Header("Settings")]
        public int maxParticles = 40;
        public float baseSpeed = 12.0f;
        public float spawnZ = 10.0f;
        public float cullZ = 0.2f;
        
        [Header("Visuals")]
        public float baseScale = 0.5f;
        public float horizonY = 1.0f;
        public Color particleColor = new Color(0.0f, 1.0f, 1.0f, 0.8f); // 기본 컬러. 네온 시안

        [Header("Star Warp Mode")]
        [Tooltip("켜면 소실점을 향해 길게 늘어지는 광속 워프 효과가 됩니다.")]
        public bool isWarpLineMode = true;
        [Tooltip("워프 라인의 길이 비율")]
        public float warpStretchFactor = 4.0f;

        private List<ParticleItem> _particles = new List<ParticleItem>();
        private RawImage _roadImage;

        private void OnEnable()
        {
            if (roadScroller != null) _roadImage = roadScroller.GetComponent<RawImage>();

            // 파티클 생성 및 화면 전체에 랜덤하게(Z축) 미리 흩뿌려놓음
            for (int i = 0; i < maxParticles; i++)
            {
                SpawnParticle(Random.Range(cullZ, spawnZ));
            }
        }

        private void Update()
        {
            if (roadScroller == null || !roadScroller.isMoving || _roadImage == null) return;
            Material currentMat = _roadImage.material;
            if (currentMat == null) return;

            float moveAmount = baseSpeed * Time.deltaTime;
            
            float currentCurve = currentMat.GetFloat("_CurveAmount");
            float currentHill = currentMat.GetFloat("_HillAmount");

            float width = container.rect.width;
            float height = container.rect.height;

            // 소실점 좌표 계산
            float maxCurveOffset = (horizonY * horizonY) * currentCurve;
            Vector2 vanishingPoint = new Vector2(maxCurveOffset * width, horizonY * height);

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                ParticleItem p = _particles[i];
                p.currentZ -= moveAmount * p.speedMultiplier;

                // 카메라를 지나치면 다시 저 멀리 지평선으로 리스폰
                if (p.currentZ <= cullZ)
                {
                    ResetParticleRandoms(p, spawnZ + Random.Range(0f, 2f));
                }

                float depth = 1f / p.currentZ;
                float adjustedY = horizonY * (1f - depth);

                float groundYNorm;
                if (Mathf.Abs(currentHill) < 0.001f) groundYNorm = adjustedY;
                else
                {
                    float discriminant = 1f - 4f * currentHill * adjustedY;
                    if (discriminant < 0f) groundYNorm = 2.0f; 
                    else groundYNorm = (1f - Mathf.Sqrt(discriminant)) / (2f * currentHill);
                }

                float curveOffset = (adjustedY * adjustedY) * currentCurve;
                
                // 원근법을 적용한 최종 X, Y 좌표 계산
                // Z가 가까워질수록(depth가 커질수록) 소실점에서 바깥으로 빠르게 퍼져나감
                float finalX = (curveOffset * width) + (p.randomX * width * depth);
                float finalY = (groundYNorm * height) + (p.randomY * height * depth);

                p.rect.anchoredPosition = new Vector2(finalX, finalY);

                // 스케일 및 페이드인/아웃 적용
                float scale = depth * baseScale;
                float alpha = CalculateAlpha(p.currentZ);
                p.img.color = new Color(particleColor.r, particleColor.g, particleColor.b, particleColor.a * alpha);

                // 연출 모드에 따른 회전 및 스케일 변형
                if (isWarpLineMode)
                {
                    // 파티클 위치에서 소실점을 바라보는 각도를 계산하여 궤적 라인을 만듦
                    Vector2 dir = p.rect.anchoredPosition - vanishingPoint;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    p.rect.localRotation = Quaternion.Euler(0, 0, angle);
                    
                    // Z속도에 비례하여 길게 늘어트림
                    p.rect.localScale = new Vector3(scale * warpStretchFactor, scale * 0.3f, 1f);
                }
                else
                {
                    // 일반 먼지/쓰레기 모드: 회전하며 다가옴
                    p.rect.Rotate(0, 0, p.rotationSpeed * Time.deltaTime);
                    p.rect.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        private void SpawnParticle(float zPos)
        {
            GameObject go = Instantiate(particlePrefab, container);
            ParticleItem p = new ParticleItem
            {
                rect = go.GetComponent<RectTransform>(),
                img = go.GetComponent<Image>()
            };
            
            p.rect.anchorMin = new Vector2(0.5f, 0f);
            p.rect.anchorMax = new Vector2(0.5f, 0f);
            p.rect.pivot = new Vector2(0.5f, 0.5f); // 파티클은 중심을 기준으로 함
            
            ResetParticleRandoms(p, zPos);
            _particles.Add(p);
        }

        private void ResetParticleRandoms(ParticleItem p, float zPos)
        {
            p.currentZ = zPos;
            
            // X축: 도로의 폭을 넘어 화면 전체로 퍼지도록 넓게 설정 (-1.5 ~ 1.5)
            // Y축: 바닥(0.0)부터 하늘(1.5)까지 높이 설정
            p.randomX = Random.Range(-1.5f, 1.5f);
            p.randomY = Random.Range(0.0f, 1.5f);
            
            p.speedMultiplier = Random.Range(0.8f, 1.5f);
            p.rotationSpeed = Random.Range(-300f, 300f);
        }

        // 지평선에서 나타날 때와 카메라에 부딪힐 때 부드럽게 사라지게 하는 투명도 조절
        private float CalculateAlpha(float z)
        {
            if (z > spawnZ * 0.8f) return (spawnZ - z) / (spawnZ * 0.2f); // 스폰 시 페이드 인
            if (z < cullZ + 1.0f) return (z - cullZ) / 1.0f;             // 카메라 근처 페이드 아웃
            return 1.0f;
        }

        private void OnDisable()
        {
            foreach (var p in _particles) Destroy(p.rect.gameObject);
            _particles.Clear();
        }
    }
}