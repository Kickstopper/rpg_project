using UnityEngine;

namespace UI.Common
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

