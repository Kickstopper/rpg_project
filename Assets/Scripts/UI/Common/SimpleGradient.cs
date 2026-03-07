using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace UI
{
    [AddComponentMenu("UI/Effects/Simple Gradient")]
    public class SimpleGradient : BaseMeshEffect
    {
        public Color colorLeft = Color.red;   // 그라데이션 시작 색상
        public Color colorRight = Color.blue; // 그라데이션 끝 색상

        public Color ColorLeft 
        { 
            get => colorLeft; 
            set { if(colorLeft != value) { colorLeft = value; graphic?.SetVerticesDirty(); } } 
        }

        public Color ColorRight 
        { 
            get => colorRight; 
            set { if(colorRight != value) { colorRight = value; graphic?.SetVerticesDirty(); } } 
        }

        [SerializeField]
        [Range(-180f, 180f)]
        private float m_Angle = 0f;

        public float angle
        {
            get => m_Angle;
            set
            {
                // 값이 같으면 갱신하지 않음 (최적화)
                if (m_Angle == value) return;
                
                m_Angle = value;
                
                // 그래픽 컴포넌트에 메쉬를 다시 그려야 한다고 알림
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        private readonly List<UIVertex> m_VertexList = new List<UIVertex>(); // 재사용을 위한 리스트

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            m_VertexList.Clear();
            vh.GetUIVertexStream(m_VertexList);

            int count = m_VertexList.Count;
            if (count == 0) return;

            float rad = m_Angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            float minV = float.MaxValue;
            float maxV = float.MinValue;

            // 1차 루프: Min/Max 찾기
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = m_VertexList[i].position;
                float val = (pos.x * cos) + (pos.y * sin);
                if (val > maxV) maxV = val;
                if (val < minV) minV = val;
            }

            float range = maxV - minV;
            // 나눗셈을 곱셈으로 치환하기 위해 역수 계산
            float invRange = (range > 0.001f) ? 1.0f / range : 0f;

            // 2차 루프: 색상 적용
            for (int i = 0; i < count; i++)
            {
                UIVertex v = m_VertexList[i];
                float val = (v.position.x * cos) + (v.position.y * sin);
                
                float t = (val - minV) * invRange;

                v.color = Color.Lerp(colorLeft, colorRight, t);
                m_VertexList[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(m_VertexList);
        }
        
        // 인스펙터에서 값을 바꿀 때 즉시 갱신되도록 처리 (에디터 편의성)
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // 그래픽 컴포넌트에 변경 사항이 있음을 알림
            if (GetComponent<Graphic>() != null)
            {
                GetComponent<Graphic>().SetVerticesDirty();
            }
        }
#endif
    }
}