using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using Manager;
namespace Controller
{
    public class RetroBootTerminal : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI terminalText;
        public ScrollRect scrollRect;
        public CanvasGroup terminalCanvasGroup; // 텍스트들이 묶인 그룹 (페이드 아웃용)
        public SceneController sceneController; // 게임 모드 선택 및 화면 이동 조작

        [Header("Settings")]
        public float typeSpeed = 0.01f;
        public float lineDelay = 0.1f;
        public string cursorChar = "_";
        public float cursorBlinkRate = 0.5f;
        public float fadeDuration = 1.5f; // 페이드 효과 지속 시간
        public float skipFadeDuration = 0.3f;

        [System.Serializable]
        public class BootLine
        {
            [TextArea] public string text;
            public float preDelay;
            public bool isMemoryCheck;
            public int memoryStart;
            public int memoryEnd;
        }

        public List<BootLine> bootSequence;

        private StringBuilder sb = new StringBuilder();
        private bool isBooting = false;
        private bool isTransitioning = false; // 이미 전환 중인지 체크

        void Start()
        {
            terminalText.text = "";
            
            // 초기 투명도 설정
            if (terminalCanvasGroup != null) terminalCanvasGroup.alpha = 1f;
            SoundManager.Instance.PlaySFX(Data.SfxID.PC_Boot);
            StartCoroutine(RunBootSequence());
            
        }

        void Update()
        {
            // 부팅 중이고, 아직 전환이 시작되지 않았을 때 키 입력 체크
            if (isBooting && !isTransitioning && Input.anyKeyDown)
            {
                SoundManager.Instance.StopAllSFX(true);
                StartCoroutine(SkipAndTransition());
            }
        }

        // 스킵 기능을 처리하는 코루틴
        IEnumerator SkipAndTransition()
        {
            isTransitioning = true;
            isBooting = false;

            // 현재 돌아가는 모든 부팅/타이핑 관련 코루틴 강제 중지
            StopAllCoroutines(); 

            // 즉시 로고 전환 시작
            yield return StartCoroutine(TransitionSequence());
        }

        IEnumerator RunBootSequence()
        {
            isBooting = true;
            yield return new WaitForSeconds(3f);
            

            foreach (var line in bootSequence)
            {
                if (!isBooting) yield break; // 스킵되었으면 중단

                yield return new WaitForSeconds(line.preDelay);

                if (line.isMemoryCheck)
                {
                    yield return StartCoroutine(ProcessMemoryCheck(line));
                }
                else
                {
                    yield return StartCoroutine(TypewriterText(line.text));
                }

                sb.AppendLine();
                yield return new WaitForSeconds(lineDelay);
            }

            // 모든 텍스트 출력이 끝났으면 자연스럽게 전환
            if (isBooting) 
            {
                isBooting = false;
                isTransitioning = true;
                SoundManager.Instance.StopAllSFX(true);
                StartCoroutine(TransitionSequence());
            }
        }

        // 페이드 아웃 -> 페이드 인 처리
        IEnumerator TransitionSequence()
        {
            float timer = 0f;

            // 텍스트 화면 페이드 아웃
            while (timer < skipFadeDuration)
            {
                timer += Time.deltaTime;
                if (terminalCanvasGroup != null)
                    terminalCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / skipFadeDuration);
                yield return null;
            }
            if (terminalCanvasGroup != null) terminalCanvasGroup.alpha = 0f;

            // 잠시 대기 (연출상 매끄러움을 위해 0.5초 정도)
            yield return new WaitForSeconds(0.5f);
            // 이후 로직 (예: "Press Start" 버튼 활성화 등)
            Debug.Log("Intro Finished. Game Ready.");
            if (!sceneController.IsEnable) sceneController.ShowAnimation();
        }

        IEnumerator TypewriterText(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                sb.Append(line[i]);
                UpdateTerminalText();
                Canvas.ForceUpdateCanvases();
                if(scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
                yield return new WaitForSeconds(typeSpeed);
            }
        }

        IEnumerator ProcessMemoryCheck(BootLine line)
        {
            string prefix = line.text;
            sb.Append(prefix);
            int step = (line.memoryEnd - line.memoryStart) / 50;
            if (step < 1) step = 1;

            for (int m = line.memoryStart; m <= line.memoryEnd; m += step)
            {
                terminalText.text = sb.ToString() + m + "GB OK" + cursorChar;
                yield return null;
            }
            sb.Append(line.memoryEnd + "GB (1.0 PB)");
            UpdateTerminalText();
        }

        IEnumerator BlinkCursor()
        {
            while (true)
            {
                terminalText.text = sb.ToString() + cursorChar;
                yield return new WaitForSeconds(cursorBlinkRate);
                terminalText.text = sb.ToString();
                yield return new WaitForSeconds(cursorBlinkRate);
            }
        }

        void UpdateTerminalText()
        {
            terminalText.text = sb.ToString() + cursorChar;
        }
    }
}
