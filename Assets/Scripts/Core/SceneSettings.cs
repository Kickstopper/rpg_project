using UnityEngine;

namespace RPGProject.Core
{
    public class SceneSettings : MonoBehaviour
    {
        void Awake()
        {
            Screen.SetResolution(1280, 720, true);
            Application.targetFrameRate = 60;
        }
    }
}

