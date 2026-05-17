using UnityEngine;
using UnityEngine.Audio;
using System.Collections; // 코루틴 사용을 위해 필요
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;
        [Header("Library")]
        [SerializeField] private AudioLibrary AudioLibrary;
        
        [Header("Settings")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private int sfxPoolSize = 10;

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSource;
        private List<AudioSource> sfxSources;
        
        // 페이드 아웃 코루틴을 제어하기 위한 변수
        private Coroutine bgmFadeCoroutine;

        private BgmID currentBgmId;
        public BgmID CurrentBgmID => currentBgmId;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                InitializeSFXPool();
                if (AudioLibrary != null)
                    AudioLibrary.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSFXPool()
        {
            sfxSources = new List<AudioSource>();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject obj = new GameObject("SFX_Source_" + i);
                obj.transform.SetParent(this.transform);
                
                AudioSource source = obj.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = sfxGroup;
                source.playOnAwake = false;

                sfxSources.Add(source);
            }

            if (bgmSource != null) return;
            
            bgmSource = new GameObject("BGM_Source").AddComponent<AudioSource>();
            bgmSource.gameObject.transform.SetParent(this.transform);
            bgmSource.outputAudioMixerGroup = bgmGroup;
            bgmSource.playOnAwake = false;
        }

        #region SFX Methods

        public void PlaySFX(SfxID sfxId, float volume = 1.0f)
        {
            AudioClip clip = AudioLibrary.GetSfxClip(sfxId);
            if (clip != null) PlaySFX(clip, volume); 
        }

        public void PlaySFX(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            AudioSource availableSource = GetAvailableSource();

            if (availableSource != null)
            {
                availableSource.clip = clip;
                availableSource.volume = volume;
                availableSource.pitch = pitch;
                availableSource.Play();
            }
            else
            {
                Debug.Log("모든 오디오 소스가 사용 중입니다!");
            }
        }

        private AudioSource GetAvailableSource()
        {
            foreach (var source in sfxSources)
            {
                if (!source.isPlaying) return source;
            }
            return null;
        }

        /// <summary>
        /// 모든 효과음을 중단합니다.
        /// </summary>
        /// <param name="useFade">페이드 아웃 사용 여부</param>
        /// <param name="fadeDuration">페이드 아웃 시간(초)</param>
        public void StopAllSFX(bool useFade = false, float fadeDuration = 0.5f)
        {
            foreach (var source in sfxSources)
            {
                if (source.isPlaying)
                {
                    if (useFade)
                    {
                        StartCoroutine(FadeOutAndStop(source, fadeDuration));
                    }
                    else
                    {
                        source.Stop();
                    }
                }
            }
        }

        #endregion

        #region BGM Methods

        public void PlayBGM(BgmID bgmId, float volume = 1.0f, bool loop = true)
        {
            AudioClip clip = AudioLibrary.GetBgmClip(bgmId);
            if (clip != null)
            {
                currentBgmId = bgmId;
                PlayBGM(clip, volume, loop);
            } 
        }
        
        public void PlayBGM(AudioClip clip, float volume = 1.0f, bool loop = true)
        {
            if (bgmSource == null) return;
            
            // 만약 이전에 페이드 아웃 중이었다면 중단하고 즉시 볼륨 복구
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            // 같은 곡이면 재생하지 않음 (단, 멈춰있었다면 재생)
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = volume;
            bgmSource.Play();
        }

        /// <summary>
        /// 배경음악을 중단
        /// </summary>
        /// <param name="useFade">페이드 아웃 사용 여부</param>
        /// <param name="fadeDuration">페이드 아웃 시간(초)</param>
        public void StopBGM(bool useFade = true, float fadeDuration = 1.0f)
        {
            currentBgmId = BgmID.None;
            
            if (bgmSource == null || !bgmSource.isPlaying) return;
            
            // 이미 페이드 아웃 중이라면 중복 실행 방지
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);

            if (useFade)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutAndStop(bgmSource, fadeDuration));
            }
            else
            {
                bgmSource.Stop();
            }
        }

        #endregion

        #region Helper Methods

        // 공용 페이드 아웃 코루틴
        private IEnumerator FadeOutAndStop(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                // 시간에 따라 볼륨을 0으로 줄임
                source.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
                yield return null;
            }

            source.Stop();
            source.volume = startVolume; // 다음 재생을 위해 볼륨 원상복구
            
            // BGM의 경우 코루틴 변수 초기화
            if (source == bgmSource)
                bgmFadeCoroutine = null;
        }

        #endregion
    }
}