using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace Manager
{
    public class FusionManager : MonoBehaviour
    {
        [Header("Data Source")]
        public MonsterDatabase monsterDB;

        [Header("Sound")]
        public AudioClip typingSound;
        private AudioSource audioSource;

        // 씬 간 데이터 전달을 위한 정적 변수 (외부에서 값을 넣고 씬을 로드함)
        public static string pendingLeftId = "128";
        public static string pendingRightId = "117";
        public static string pendingResultId = "205";

        [Header("Animation Settings")]
        public AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Monsters Renderers")]
        public SpriteRenderer leftMonster;
        public SpriteRenderer rightMonster;
        public SpriteRenderer resultMonster;

        [Header("Effects")]
        public ParticleSystem leftParticles;
        public ParticleSystem rightParticles;
        public ParticleSystem centerParticles;
        public Image flashPanel; // 전체 화면 흰색 패널
        public float particleDuration = 3.0f; // 파티클이 중앙으로 모이는 시간

        [Header("UI")]
        public TextMeshProUGUI messageText;
        public string resultMessage = "Fuck You!";
        public float typingSpeed = 0.05f;

        void Start()
        {
            ManagerRoot.Sound.PlayBGM(Data.BgmID.Fusion);
            // 몬스터 이미지 세팅 (시퀀스 시작 전 필수)
            SetupMonstersFromDatabase();

            // 초기화 (투명도 등)
            SetAlpha(leftMonster, 1);
            SetAlpha(rightMonster, 1);
            resultMonster.gameObject.SetActive(false);
            SetImageAlpha(flashPanel, 0);
            messageText.text = "";
            messageText.transform.parent.gameObject.SetActive(false);

            // AudioSource 컴포넌트 가져오기 또는 추가하기
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // 시퀀스 시작
            StartCoroutine(ProcessFusionSequence());
        }

        // DB에서 정보를 가져와 스프라이트에 적용하는 핵심 함수
        void SetupMonstersFromDatabase()
        {
            if (monsterDB == null)
            {
                Debug.LogError("MonsterDatabase가 할당되지 않았습니다!");
                return;
            }

            // DB 초기화 (안전장치)
            monsterDB.Initialize();

            // 데이터 조회 및 적용
            ApplySprite(leftMonster, pendingLeftId, "Left Monster");
            ApplySprite(rightMonster, pendingRightId, "Right Monster");
            ApplySprite(resultMonster, pendingResultId, "Result Monster");
        }

        void ApplySprite(SpriteRenderer renderer, string id, string debugName)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{debugName}의 ID가 비어있습니다.");
                return;
            }

            MonsterDatabase.MonsterEntry entry = monsterDB.GetEntry(id);

            if (entry != null && entry.image != null && entry.image.Length > 0)
            {
                // 배열의 첫 번째 이미지를 사용 (애니메이션이 필요하면 별도 로직 추가)
                renderer.sprite = entry.image[0];
                
                // 만약 결과 몬스터라면, 이름도 UI에 띄울 수 있도록 저장 가능
                if (renderer == resultMonster)
                {
                    // 예: resultMessage = $"{entry.name}가 탄생했다!";
                }
            }
            else
            {
                Debug.LogError($"ID [{id}]에 해당하는 몬스터 데이터를 찾을 수 없거나 이미지가 없습니다.");
            }
        }

        // 전체 합체 시퀀스 코루틴
        IEnumerator ProcessFusionSequence()
        {

            yield return new WaitForSeconds(2f);
            PlayFusionParticles();
            // 두 몬스터 페이드 아웃
            StartCoroutine(DissolveSprite(leftMonster, 1.5f)); 
            StartCoroutine(DissolveSprite(rightMonster, 1.5f));

            // 파티클이 중앙으로 모이는 시간만큼 대기
            yield return new WaitForSeconds(particleDuration);
            
            // 화면 화이트 아웃 (Flash)
            ManagerRoot.Sound.PlaySFX(Data.SfxID.Explosion);
            yield return StartCoroutine(FadeImage(flashPanel, 0, 1, 0.2f)); // 빠르게 하얗게
            StopFusionParticles();
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(FadeImage(flashPanel, 1, 0, 2f));
            
            resultMonster.gameObject.SetActive(true);
            StartCoroutine(DissolveSprite(resultMonster, 2.0f, true));
            yield return new WaitForSeconds(0.5f);
            //centerParticles.Clear();
            centerParticles.Play();
            messageText.transform.parent.gameObject.SetActive(true);
            
            

            yield return new WaitForSeconds(3f);

            // 메시지 출력 (타자기 효과)
            yield return StartCoroutine(TypeWriterEffect(resultMessage));
        }

        // --- 헬퍼 함수들 ---

        void PlayFusionParticles()
        {
            // 파티클 재생
            leftParticles.Play();
            rightParticles.Play();
        }

        void StopFusionParticles()
        {
            leftParticles.Stop();
            rightParticles.Stop();
        }

        IEnumerator DissolveSprite(SpriteRenderer sprite, float duration, bool reverse = false)
        {
            float elapsed = 0f;
            string propertyName = "_DissolveAmount";
            Material mat = sprite.material;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration; // 0에서 1까지 진행되는 시간 비율

                // 커브에서 현재 시간(t)에 해당하는 값을 가져옵니다.
                // 커브의 Y축 값이 곧 디졸브 수치가 됩니다.
                float curveValue = dissolveCurve.Evaluate(t);

                // reverse일 경우(나타날 때)는 1에서 0으로 가야 하므로 반전시킵니다.
                float finalValue = reverse ? (1f - curveValue) : curveValue;
                
                // 안전장치: 혹시 모를 1.1 오버슈팅을 위해 살짝 곱해줄 수 있습니다.
                // (노이즈 텍스처에 완전 흰색이 있으면 1.0에서 안 지워질 수 있으므로 1.05배)
                mat.SetFloat(propertyName, finalValue * 1.05f);

                yield return null;
            }

            // 끝난 후 확실하게 값 고정
            mat.SetFloat(propertyName, reverse ? 0f : 1.1f);
        }

        // 스프라이트 페이드 처리
        IEnumerator FadeSprite(SpriteRenderer sprite, float start, float end, float duration)
        {
            float elapsed = 0f;
            Color c = sprite.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(start, end, elapsed / duration);
                sprite.color = c;
                yield return null;
            }
            c.a = end;
            sprite.color = c;
        }

        // UI 이미지 페이드 처리
        IEnumerator FadeImage(Image img, float start, float end, float duration)
        {
            float elapsed = 0f;
            Color c = img.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(start, end, elapsed / duration);
                img.color = c;
                yield return null;
            }
            c.a = end;
            img.color = c;
        }

        // 간단한 알파값 설정
        void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            Color c = sprite.color;
            c.a = alpha;
            sprite.color = c;
        }

        void SetImageAlpha(Image img, float alpha)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }

        IEnumerator TypeWriterEffect(string fullText)
        {
            messageText.text = ""; // 텍스트 초기화

            foreach (char letter in fullText.ToCharArray())
            {
                messageText.text += letter; // 한 글자 추가

                // --- 사운드 재생 로직 추가 ---
                // 공백(띄어쓰기)에는 소리를 안 내는 것이 더 자연스러울지도 모른다. 조건을 필요하다면 추가하자(letter != ' ')
                if (typingSound != null) 
                {
                    // 기계적인 느낌을 줄이기 위해 피치를 약간 랜덤하게 조절 (0.9 ~ 1.1)
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(typingSound);
                }
                // -------------------------

                yield return new WaitForSeconds(typingSpeed);
            }
        }
    }
}