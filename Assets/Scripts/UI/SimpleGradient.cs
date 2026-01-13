using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
namespace UI
{
    [AddComponentMenu("UI/Effects/Simple Gradient")]
    public class SimpleGradient : BaseMeshEffect
    {
        public Color colorLeft = Color.red;
        public Color colorRight = Color.blue;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            List<UIVertex> vertexList = new List<UIVertex>();
            vh.GetUIVertexStream(vertexList);

            int count = vertexList.Count;
            if (count == 0) return;

            // UI 요소의 너비 구하기 (Left/Right 계산을 위해)
            float leftX = vertexList[0].position.x;
            float rightX = vertexList[0].position.x;

            for (int i = 1; i < count; i++)
            {
                float x = vertexList[i].position.x;
                if (x > rightX) rightX = x;
                if (x < leftX) leftX = x;
            }

            float width = rightX - leftX;

            // 각 버텍스의 위치에 따라 색상 보간(Lerp)
            for (int i = 0; i < count; i++)
            {
                UIVertex v = vertexList[i];
                
                // 너비가 0이면 분모가 0이 되는 것 방지
                float t = (width == 0) ? 0 : (v.position.x - leftX) / width;
                
                v.color = Color.Lerp(colorLeft, colorRight, t);
                vertexList[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(vertexList);
        }
    }
    
}
