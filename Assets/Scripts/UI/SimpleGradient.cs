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

        [Range(-180f, 180f)]
        public float angle = 0f; // 그라데이션 각도 (-180 ~ 180도)

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            List<UIVertex> vertexList = new List<UIVertex>();
            vh.GetUIVertexStream(vertexList);

            int count = vertexList.Count;
            if (count == 0) return;

            // 1. 각도를 라디안으로 변환 및 방향 벡터 계산
            // angle이 0이면 (1, 0) -> 가로 방향 (기존과 동일)
            // angle이 90이면 (0, 1) -> 세로 방향
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            // 2. 회전된 축을 기준으로 최소/최대값 찾기 (Min/Max Projection)
            float minV = float.MaxValue;
            float maxV = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = vertexList[i].position;
                
                // 내적(Dot Product)을 통해 그라데이션 방향 축에서의 위치값 계산
                // 공식: x * cos(θ) + y * sin(θ)
                float val = (pos.x * cos) + (pos.y * sin);

                if (val > maxV) maxV = val;
                if (val < minV) minV = val;
            }

            float range = maxV - minV;

            // 3. 각 버텍스에 색상 적용
            for (int i = 0; i < count; i++)
            {
                UIVertex v = vertexList[i];
                
                // 현재 버텍스의 회전된 축 상의 위치
                float val = (v.position.x * cos) + (v.position.y * sin);

                // 0 ~ 1 사이 값으로 정규화 (t)
                float t = (range == 0) ? 0 : (val - minV) / range;

                v.color = Color.Lerp(colorLeft, colorRight, t);
                vertexList[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(vertexList);
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