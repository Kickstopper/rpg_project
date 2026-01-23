using UnityEngine;
using Manager;
namespace Controller
{
    public class SceneBgmStarter : MonoBehaviour
    {
        [Header("재생할 배경음악")]
        public Data.BgmID bgmID; 

        [Range(0f, 1f)]
        public float volume = 1.0f;

        void Start()
        {
            // SoundManager가 존재하는지 안전하게 확인
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBGM(bgmID, volume);
            }
            else
            {
                Debug.LogWarning("SoundManager가 씬에 없습니다!");
            }
        }
    }
    
}
