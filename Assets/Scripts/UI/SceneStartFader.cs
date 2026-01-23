using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace UI
{
    public class SceneStartFader : MonoBehaviour
    {
        [Header("UI 연결")]
        public Image fadePanel;      // 검은색 막 이미지
        public float fadeDuration = 1.0f; // 밝아지는 데 걸리는 시간

        void Start()
        {
            // 씬이 시작되면 바로 코루틴 실행
            StartCoroutine(FadeInRoutine());
        }

        IEnumerator FadeInRoutine()
        {
            float timer = fadeDuration;
            Color color = fadePanel.color;

            // 1. 시작할 때 강제로 완전 검은색으로 설정 (깜빡임 방지)
            fadePanel.color = new Color(color.r, color.g, color.b, 1f);
            fadePanel.raycastTarget = true; // 페이드 중에는 클릭 방지

            // 2. 시간이 지날수록 알파값(투명도)을 줄임
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeDuration);
                fadePanel.color = new Color(color.r, color.g, color.b, alpha);
                yield return null; // 한 프레임 대기
            }

            // 3. 완전히 투명해지면 마무리
            fadePanel.color = new Color(color.r, color.g, color.b, 0f);
            fadePanel.raycastTarget = false; // 이제 UI 클릭 가능하게 허용
        }
    }
    
}

