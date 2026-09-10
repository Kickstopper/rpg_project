using UnityEngine;
using RPGProject.Core;

namespace RPGProject.Infrastructure.Audio
{
    public class SceneBgmStarter : MonoBehaviour
    {
        [Header("재생할 배경음악")]
        public BgmID bgmID; 

        [Range(0f, 1f)]
        public float volume = 1.0f;

        void Start()
        {
            if (ManagerRoot.Sound != null)
            {
                ManagerRoot.Sound.PlayBGM(bgmID, volume);
            }
            else
            {
                Debug.LogWarning("SoundManager가 씬에 없습니다!");
            }
        }
    }
    
}
