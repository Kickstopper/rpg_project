using UnityEngine;

namespace Manager
{
    public class GameSettingManager : MonoBehaviour
    {
        public static GameSettingManager Instance;
        public bool useAnaglyph = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

    }

}
