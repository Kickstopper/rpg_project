using UnityEngine;

namespace UI.Common
{
    public static class GameInput
    {
        public static bool GetConfirmDown()
        {
            return Input.GetButtonDown("Submit") || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        }

        public static bool GetSelectDown()
        {
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        }

        public static bool GetCancelDown()
        {
            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
                return true;
            
            if (Input.GetMouseButtonDown(1)) return true;

            if (Input.touchCount == 2 && 
               (Input.GetTouch(0).phase == TouchPhase.Began || Input.GetTouch(1).phase == TouchPhase.Began))
                return true;

            return false;
        }

        // 플레이어 메뉴 열기 전용 조작
        public static bool GetMenuDown()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) return true;
            
            if (Input.GetMouseButtonDown(1)) return true;

            if (Input.touchCount == 2 && 
               (Input.GetTouch(0).phase == TouchPhase.Began || Input.GetTouch(1).phase == TouchPhase.Began))
                return true;

            return false;
        }

        // 전체 맵 토글 전용 조작
        public static bool GetMapToggleDown()
        {
            if (Input.GetKey(KeyCode.LeftShift) && 
               (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                return true;

            if (Input.GetMouseButtonDown(2)) return true;

            if (Input.touchCount == 3 && 
               (Input.GetTouch(0).phase == TouchPhase.Began || 
                Input.GetTouch(1).phase == TouchPhase.Began || 
                Input.GetTouch(2).phase == TouchPhase.Began))
                return true;

            return false;
        }
    }
}