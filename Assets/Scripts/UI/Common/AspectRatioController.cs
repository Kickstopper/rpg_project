using UnityEngine;
namespace UI.Common
{
    public class AspectRatioController : MonoBehaviour
    {
        void Start()
        {
            // 목표로 하는 화면 비율 (16:9 = 1.777...)
            float targetAspectRatio = 16.0f / 9.0f;
            
            // 현재 실행 중인 기기의 화면 비율
            float currentAspectRatio = (float)Screen.width / (float)Screen.height;
            
            // 현재 기기 비율과 목표 비율의 차이 계산
            float scaleHeight = currentAspectRatio / targetAspectRatio;

            Camera camera = GetComponent<Camera>();

            // 기기 화면이 16:9보다 세로로 길거나 가로로 좁을 때 (상하 블랙바)
            if (scaleHeight < 1.0f)
            {
                Rect rect = camera.rect;
                rect.width = 1.0f;
                rect.height = scaleHeight;
                rect.x = 0;
                rect.y = (1.0f - scaleHeight) / 2.0f;
                camera.rect = rect;
            }
            // 기기 화면이 16:9보다 가로로 길 때 (좌우 블랙바 - 최신 스마트폰 대부분 해당)
            else
            {
                float scaleWidth = 1.0f / scaleHeight;
                Rect rect = camera.rect;
                rect.width = scaleWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - scaleWidth) / 2.0f;
                rect.y = 0;
                camera.rect = rect;
            }
        }
    }
}

