using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Manager;
using Data;

namespace UI.IntroScene
{
    [System.Serializable]
    public class CutsceneData
    {
        public Sprite image;
        public Sprite background;
        [TextArea(3, 5)]
        public string text;
        [Tooltip("글자당 출력 딜레이 (초)")]
        public float lineDelay = 0.05f;
        [Tooltip("텍스트 출력 완료 후 대기 시간 (초)")]
        public float delayAfter = 2.0f;
    }

    public class CutsceneController : MonoBehaviour
    {
        [Header("UI References")]
        public Image cutsceneImage;
        public Image backgroundImage;
        public TextMeshProUGUI cutsceneText;

        [Header("BGM")]
        public BgmID bgmId;
        
        [Header("Fade Settings")]
        public Image fadeOverlay;
        public Color fadeInColor = Color.black;
        public Color fadeOutColor = Color.black;
        public float fadeInDuration = 1.5f;
        public float fadeOutDuration = 1.5f;
        
        [Header("Cutscene Sequence Data")]
        public List<CutsceneData> cutsceneList = new List<CutsceneData>();

        [Header("Scene Transition")]
        [Tooltip("컷신 종료 후 이동할 씬의 이름")]
        public string nextSceneName;
        public string nextSceneParam;

        // 스킵 기능을 위한 상태 변수
        private Coroutine seqCoroutine;
        private bool skipRequested = false;

        private void Start()
        {
            if (ManagerRoot.Sound != null && bgmId != BgmID.None)
            {
                ManagerRoot.Sound.PlayBGM(bgmId, 1, false);
            }
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(fadeInColor.r, fadeInColor.g, fadeInColor.b, fadeInDuration > 0f ? 1f : 0f);
                fadeOverlay.gameObject.SetActive(true);
            }
            if (cutsceneList.Count > 0)
            {
                if (seqCoroutine != null) StopCoroutine(seqCoroutine);
                seqCoroutine = StartCoroutine(PlayCutsceneSequence());
            }
        }

        private void Update()
        {
            // 마우스 좌클릭, 스페이스바, 엔터키 입력 시 스킵 요청
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                skipRequested = true;
            }
            // 컷씬 시퀀스 전체를 스킵
            else if (Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
            {
                SkipCutscene();
            }
        }

        private IEnumerator PlayCutsceneSequence()
        {
            StartCoroutine(FadeIn());

            foreach (CutsceneData data in cutsceneList)
            {
                if (data.background != null)
                {
                    backgroundImage.enabled = true;
                    backgroundImage.sprite = data.background;
                    backgroundImage.SetNativeSize();
                }
                else
                {
                    backgroundImage.enabled = false;
                }
                
                if (data.image != null)
                {
                    cutsceneImage.sprite = data.image;
                    cutsceneImage.color = Color.white;
                    cutsceneImage.SetNativeSize();
                }
                else
                {
                    cutsceneImage.color = Color.clear;
                }

                cutsceneText.text = data.text;
                cutsceneText.maxVisibleCharacters = 0;

                int totalCharacters = data.text.Length;

                skipRequested = false; // 새로운 컷 시작 시 스킵 요청 초기화

                // 1단계 스킵 감지. 텍스트 타이핑 중
                for (int i = 0; i <= totalCharacters; i++)
                {
                    if (skipRequested)
                    {
                        // 스킵이 요청되면 즉시 모든 글자를 보여주고 타이핑 루프 종료
                        cutsceneText.maxVisibleCharacters = totalCharacters;
                        skipRequested = false; 
                        break;
                    }

                    cutsceneText.maxVisibleCharacters = i;
                    yield return YieldCache.WaitForSeconds(data.lineDelay); 
                }

                // 2단계 스킵 감지. 대기 시간 중
                float timer = 0f;
                // 대기 시간 시작 전 스킵 요청 다시 초기화
                skipRequested = false;
                
                while (timer < data.delayAfter)
                {
                    if (skipRequested)
                    {
                        // 대기 시간 중 스킵이 요청되면 즉시 대기 루프 종료 후 다음 컷으로
                        skipRequested = false;
                        break;
                    }
                    timer += Time.deltaTime;
                    yield return null;
                }
            }

            yield return StartCoroutine(FadeOut());

            TransitionToNextScene();
        }

        private IEnumerator FadeIn()
        {
            if (fadeOverlay == null) yield break;

            float timer = 0f;
            Color startColor = new Color(fadeInColor.r, fadeInColor.g, fadeInColor.b, 1);
            Color endColor = new Color(fadeInColor.r, fadeInColor.g, fadeInColor.b, 0);

            while (timer < fadeInDuration)
            {
                timer += Time.deltaTime;
                fadeOverlay.color = Color.Lerp(startColor, endColor, timer / fadeInDuration);
                yield return null;
            }

            fadeOverlay.color = endColor;
        }

        private IEnumerator FadeOut()
        {
            if (fadeOverlay == null) yield break;
            ManagerRoot.Sound.StopBGM(true, fadeOutDuration);
            float timer = 0f;
            Color startColor = new Color(fadeOutColor.r, fadeOutColor.g, fadeOutColor.b, 0);
            Color endColor = new Color(fadeOutColor.r, fadeOutColor.g, fadeOutColor.b, 1);

            while (timer < fadeOutDuration)
            {
                timer += Time.deltaTime;
                fadeOverlay.color = Color.Lerp(startColor, endColor, timer / fadeOutDuration);
                yield return null;
            }

            fadeOverlay.color = endColor;
        }

        private void SkipCutscene()
        {
            ManagerRoot.Sound.StopBGM();
            StopAllCoroutines();
            TransitionToNextScene();
        }

        private void TransitionToNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (nextSceneName.Equals(GameScene.DUNGEON_MAP_SCENE))
                {
                    ManagerRoot.Dungeon.LoadDungeonFromJson(nextSceneParam);
                    SceneManager.LoadScene(GameScene.DUNGEON_MAP_SCENE);
                }
                else if (nextSceneName.Equals(GameScene.WORLD_MAP_SCENE))
                {
                    ManagerRoot.World.SetCurrentRegionTheme(nextSceneParam); 
                    SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                }
                else
                {
                    SceneManager.LoadScene(nextSceneName);
                }
            }
            else
            {
                Debug.LogWarning("전환할 씬의 이름이 입력되지 않았습니다!");
            }
        }
    }
}
