using UnityEngine;
namespace UI.Common
{
    public static class GameInput
    {
        // 전역에서 사용할 수 있는 취소/메뉴 입력 체크 함수
        public static bool GetCancelDown()
        {
            // PC 마우스 우클릭
            if (Input.GetMouseButtonDown(1))
            {
                return true;
            }

            // 모바일 두 손가락 탭
            if (Input.touchCount == 2)
            {
                Touch touch1 = Input.GetTouch(0);
                Touch touch2 = Input.GetTouch(1);

                if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            return false;
        }
    }
}