using UnityEngine;

namespace RPGProject.Shared.Input
{
    public static class GameInput
    {
        private static int confirmConsumedFrame = -1;
        public static bool IsConfirmConsumed => confirmConsumedFrame == Time.frameCount;
        public static void ConsumeConfirmThisFrame() => confirmConsumedFrame = Time.frameCount;
        public static bool GetConfirmDown()
        {
            if (IsConfirmConsumed) return false;
            if (UnityEngine.Input.touchCount == 1 && UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began) return true;
            return UnityEngine.Input.GetButtonDown("Submit") || UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return);
        }

        public static bool GetSelectDown()
        {
            if (IsConfirmConsumed) return false;
            return UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        public static bool GetCancelDown()
        {
            if (UnityEngine.Input.GetButtonDown("Cancel") || UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.LeftShift))
                return true;
            
            if (UnityEngine.Input.GetMouseButtonDown(1)) return true;

            if (UnityEngine.Input.touchCount == 2 && 
               (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began || UnityEngine.Input.GetTouch(1).phase == TouchPhase.Began))
                return true;

            return false;
        }

        // 플레이어 메뉴 열기 전용 조작
        public static bool GetMenuDown()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return true;
            
            if (UnityEngine.Input.GetMouseButtonDown(1)) return true;

            if (UnityEngine.Input.touchCount == 2 && 
               (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began || UnityEngine.Input.GetTouch(1).phase == TouchPhase.Began))
                return true;

            return false;
        }

        // 전체 맵 토글 전용 조작
        public static bool GetMapToggleDown()
        {
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift) && 
               (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return)))
                return true;

            if (UnityEngine.Input.GetMouseButtonDown(2)) return true;

            if (UnityEngine.Input.touchCount == 3 && 
               (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began || 
                UnityEngine.Input.GetTouch(1).phase == TouchPhase.Began || 
                UnityEngine.Input.GetTouch(2).phase == TouchPhase.Began))
                return true;

            return false;
        }
    }
}