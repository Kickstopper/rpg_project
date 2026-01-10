using UnityEngine;
using UnityEngine.Audio; // 오디오 믹서 사용을 위해 필요
using System.Collections.Generic;
using Data;

namespace Manager
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;
        [Header("Library")]
        [SerializeField] private AudioLibrary AudioLibrary; // 에디터에서 할당
        
        [Header("Settings")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private int sfxPoolSize = 10; // 동시에 낼 수 있는 최대 효과음 수

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSource; // BGM은 하나면 충분 (Loop)
        private List<AudioSource> sfxSources; // SFX용 스피커 리스트

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                InitializeSFXPool(); // 풀 생성
                // 게임 시작 시 도서관 초기화 (딕셔너리 생성)
                if (AudioLibrary != null)
                    AudioLibrary.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 1. 시작할 때 스피커를 미리 10개 만들어 둠.
        private void InitializeSFXPool()
        {
            sfxSources = new List<AudioSource>();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject obj = new GameObject("SFX_Source_" + i);
                obj.transform.SetParent(this.transform);
                
                AudioSource source = obj.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = sfxGroup; // 믹서 연결
                source.playOnAwake = false;

                sfxSources.Add(source);
            }

            if (bgmSource != null) return;
            
            bgmSource = new GameObject("BGM_Source").AddComponent<AudioSource>();
            bgmSource.gameObject.transform.SetParent(this.transform);
            bgmSource.outputAudioMixerGroup = bgmGroup;
            bgmSource.playOnAwake = false;
        }

        // 2. 효과음 재생 요청이 오면 '노는 스피커'를 찾는다.
        public void PlaySFX(SfxID sfxId, float volume = 1.0f)
        {
            AudioClip clip = AudioLibrary.GetSfxClip(sfxId);
            
            if (clip != null)
            {
                PlaySFX(clip, volume); 
            }
        }

        public void PlaySFX(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            AudioSource availableSource = GetAvailableSource();

            if (availableSource != null)
            {
                availableSource.clip = clip;
                availableSource.volume = volume;
                availableSource.pitch = pitch; // 피치 조절 (타격감 변화 등에 사용)
                availableSource.Play();
            }
            else
            {
                Debug.Log("모든 오디오 소스가 사용 중입니다! (Pool Size를 늘리세요)");
                // 선택 사항: 가장 오래된 소리를 끄고 이걸 재생할 수도 있음
            }
        }

        // '놀고 있는(재생 중이 아닌)' 소스를 찾는 함수
        private AudioSource GetAvailableSource()
        {
            foreach (var source in sfxSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            return null; // 남는 게 없음
        }

        public void PlayBGM(BgmID bgmId, float volume = 1.0f)
        {
            AudioClip clip = AudioLibrary.GetBgmClip(bgmId);
            
            if (clip != null)
            {
                PlayBGM(clip, volume); 
            }
        }
        
        // BGM 재생 (이전과 동일)
        public void PlayBGM(AudioClip clip, float volume = 1.0f)
        {
            if (bgmSource == null || bgmSource.clip == clip) return;
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = volume;
            bgmSource.Play();
        }
    }
}