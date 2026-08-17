using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class SoundManager : MonoBehaviour
    {
        [Header("Library")]
        [SerializeField] private AudioLibrary AudioLibrary;
        
        [Header("Settings")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private int sfxPoolSize = 10;

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSource;
        
        private List<SfxSourceWrapper> sfxWrappers;
        
        private Coroutine bgmFadeCoroutine;

        private BgmID currentBgmId;
        public BgmID CurrentBgmID => currentBgmId;

        private class SfxSourceWrapper
        {
            public AudioSource Source { get; private set; }
            public SfxID CurrentId { get; set; }

            public SfxSourceWrapper(AudioSource source)
            {
                Source = source;
                CurrentId = SfxID.None;
            }
        }

        private void Awake()
        {
            InitializeSFXPool();
            if (AudioLibrary != null) AudioLibrary.Initialize();
        }

        private void InitializeSFXPool()
        {
            sfxWrappers = new List<SfxSourceWrapper>();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject obj = new GameObject("SFX_Source_" + i);
                obj.transform.SetParent(this.transform);
                
                AudioSource source = obj.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = sfxGroup;
                source.playOnAwake = false;

                sfxWrappers.Add(new SfxSourceWrapper(source));
            }

            if (bgmSource != null) return;
            
            bgmSource = new GameObject("BGM_Source").AddComponent<AudioSource>();
            bgmSource.gameObject.transform.SetParent(this.transform);
            bgmSource.outputAudioMixerGroup = bgmGroup;
            bgmSource.playOnAwake = false;
        }

        #region SFX Methods

        public void PlaySFX(SfxID sfxId, float volume = 1.0f, float pitch = 1.0f, bool loop = false)
        {
            AudioClip clip = AudioLibrary.GetSfxClip(sfxId);
            if (clip != null) 
            {
                SfxSourceWrapper availableWrapper = GetAvailableWrapper();

                if (availableWrapper != null)
                {
                    availableWrapper.CurrentId = sfxId; 
                    
                    availableWrapper.Source.clip = clip;
                    availableWrapper.Source.volume = volume;
                    availableWrapper.Source.pitch = pitch;
                    availableWrapper.Source.loop = loop; 
                    
                    availableWrapper.Source.Play();
                }
                else
                {
                    Debug.Log("모든 오디오 소스가 사용 중입니다!");
                }
            } 
        }

        private SfxSourceWrapper GetAvailableWrapper()
        {
            foreach (var wrapper in sfxWrappers)
            {
                // 사용 중이지 않은 소스를 찾으면 해당 래퍼 반환
                if (!wrapper.Source.isPlaying) return wrapper;
            }
            return null;
        }

        private SfxSourceWrapper GetPlayingSfxWrapper(SfxID sfxId)
        {
            foreach (var wrapper in sfxWrappers)
            {
                if (wrapper.Source.isPlaying && wrapper.CurrentId == sfxId)
                {
                    return wrapper;
                }
            }
            return null;
        }

        public bool IsSfxPlaying(SfxID sfxId)
        {
            SfxSourceWrapper sfx = GetPlayingSfxWrapper(sfxId);
            return sfx != null;
        }

        public void StopSFX(SfxID sfxId)
        {
            SfxSourceWrapper sfx = GetPlayingSfxWrapper(sfxId);
            if (sfx != null) sfx.Source.Stop();
        }

        public void StopAllSFX(bool useFade = false, float fadeDuration = 0.5f)
        {
            foreach (var wrapper in sfxWrappers)
            {
                if (wrapper.Source.isPlaying)
                {
                    if (useFade)
                    {
                        StartCoroutine(FadeOutAndStop(wrapper.Source, fadeDuration));
                    }
                    else
                    {
                        wrapper.Source.Stop();
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

        /// <summary>
        /// BGM의 볼륨을 변경합니다.
        /// </summary>
        /// <param name="targetVolume">목표 볼륨 (0.0f ~ 1.0f)</param>
        /// <param name="useFade">페이드 효과 사용 여부</param>
        /// <param name="fadeDuration">페이드 진행 시간(초)</param>
        public void SetBgmVolume(float targetVolume, bool useFade = true, float fadeDuration = 1.0f)
        {
            if (bgmSource == null) return;

            // 진행 중인 페이드 코루틴이 있다면 중단
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (useFade)
            {
                bgmFadeCoroutine = StartCoroutine(FadeVolume(bgmSource, targetVolume, fadeDuration));
            }
            else
            {
                bgmSource.volume = targetVolume;
            }
        }

        // 볼륨 변경 전용 페이드 코루틴
        private IEnumerator FadeVolume(AudioSource source, float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                // 시간에 따라 현재 볼륨에서 목표 볼륨으로 부드럽게 변경
                source.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
                yield return null;
            }

            // 정확한 목표 볼륨으로 최종 보정
            source.volume = targetVolume;
            
            // BGM의 경우 코루틴 변수 초기화
            if (source == bgmSource)
                bgmFadeCoroutine = null;
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