using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle
{
    [RequireComponent(typeof(Image))]
    public class BattleVFXAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct FrameData
        {
            public Sprite frameSprite;
            [Tooltip("이 프레임이 화면에 유지되는 시간 (초)")]
            public float duration;
        }

        [Header("Quick Setup (자동 생성)")]
        [Tooltip("여기에 여러 장의 스프라이트를 한 번에 드래그 앤 드롭하세요.")]
        public Sprite[] images;
        
        [Tooltip("자동 생성 시 적용될 기본 시간")]
        public float defaultDuration = 0.1f;
        
        [Tooltip("이 체크박스를 누르면 images의 스프라이트들을 바탕으로 Frames 배열이 자동 생성됩니다.")]
        public bool generateFramesNow = false;

        [Header("Animation Settings")]
        public FrameData[] frames;
        
        [Header("UI Settings")]
        [Tooltip("프레임이 바뀔 때마다 이미지 원본 크기로 맞출지 여부")]
        public bool useNativeSize = true;

        private Image uiImage;

        void Awake()
        {
            uiImage = GetComponent<Image>();
        }

        void Start()
        {
            if (frames != null && frames.Length > 0)
            {
                StartCoroutine(PlayAnimation());
            }
            else
            {
                Debug.LogWarning("마법 효과 애니메이션 프레임이 설정되지 않았습니다.");
                Destroy(gameObject);
            }
        }

        private IEnumerator PlayAnimation()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                uiImage.sprite = frames[i].frameSprite;
                
                if (useNativeSize)
                {
                    uiImage.SetNativeSize();
                }
                
                yield return YieldCache.WaitForSeconds(frames[i].duration);
            }

            Destroy(gameObject);
        }

    #if UNITY_EDITOR
        // 인스펙터에서 값이 변경될 때마다 에디터에서 자동으로 호출되는 함수
        private void OnValidate()
        {
            // 'generateFramesNow' 체크박스를 클릭했을 때만 작동하도록
            if (generateFramesNow)
            {
                // 체크박스를 다시 원래 상태(false)로 되돌려 버튼처럼 작동하게 만듦
                generateFramesNow = false; 

                if (images != null && images.Length > 0)
                {
                    // images 배열의 크기만큼 frames 배열을 새로 할당
                    frames = new FrameData[images.Length];
                    
                    for (int i = 0; i < images.Length; i++)
                    {
                        // 각 프레임에 스프라이트와 기본 시간을 넣어줌
                        frames[i] = new FrameData 
                        { 
                            frameSprite = images[i], 
                            duration = defaultDuration 
                        };
                    }
                    
                    Debug.Log($"[UIMagicEffect] 총 {images.Length}개의 프레임 데이터가 {defaultDuration}초 간격으로 자동 생성되었습니다!");
                }
                else
                {
                    Debug.LogWarning("[UIMagicEffect] Images 배열이 비어있어 프레임을 생성할 수 없습니다.");
                }
            }
        }
    #endif
    }
}