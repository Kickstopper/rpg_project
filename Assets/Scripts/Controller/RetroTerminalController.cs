using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using Manager;
using UI.CharacterCreationScene;
namespace Controller
{
    public class RetroTerminalController : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI terminalText;
        public ScrollRect scrollRect;
        public CanvasGroup terminalCanvasGroup; // 텍스트들이 묶인 그룹 (페이드 아웃용)
        public CharacterCreationManager charCreationManager;

        [Header("Settings")]
        public float typeSpeed = 0.01f;
        public float lineDelay = 0.1f;
        public string cursorChar = "_";
        public float cursorBlinkRate = 0.25f;
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
        private bool isProgress = false;
        private bool isType = false;
        private bool isTransitioning = false; // 이미 전환 중인지 체크

        void Start()
        {
            charCreationManager.enabled = false;

            terminalText.text = "";
            // 초기 투명도 설정
            if (terminalCanvasGroup != null) terminalCanvasGroup.alpha = 1f;
            StartCoroutine(BlinkCursor());
            StartCoroutine(RunSequence());
        }

        void Update()
        {
            if (isProgress || isTransitioning) return;
            if (Input.anyKeyDown)
            {
                if (ManagerRoot.Sound != null)
                {
                    ManagerRoot.Sound.StopAllSFX(true, 1);
                    ManagerRoot.Sound.StopBGM();
                }
                StartTransitionSequence();
            }
        }

        // 스킵 기능을 처리하는 코루틴
        IEnumerator SkipAndTransition()
        {
            isTransitioning = true;
            isProgress = false;

            // 현재 돌아가는 모든 부팅/타이핑 관련 코루틴 강제 중지
            StopAllCoroutines(); 

            // 즉시 로고 전환 시작
            yield return StartCoroutine(TransitionSequence());
        }

        IEnumerator RunSequence()
        {
            isProgress = true;
            yield return new WaitForSeconds(1f);
            
            foreach (var line in bootSequence)
            {
                if (!isProgress) yield break; // 스킵되었으면 중단

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
            if (isProgress) 
            {
                isProgress = false;
            }
        }

        private void StartTransitionSequence()
        {
            if (isTransitioning) return;
            
            isTransitioning = true;
            if (ManagerRoot.Sound != null)
            {
                ManagerRoot.Sound.StopAllSFX(true, 1);
                ManagerRoot.Sound.StopBGM();
            }
            
            StartCoroutine(TransitionSequence());
        }

        // 페이드 아웃/인 처리
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

            yield return new WaitForSeconds(0.1f);

            if (charCreationManager != null)
            {
                charCreationManager.enabled = true;
                charCreationManager.StartFirstStep();
            } 
        }

        IEnumerator TypewriterText(string line)
        {
            // typeSpeed가 0 이하일 경우 딜레이 없이 한 줄을 즉시 출력
            if (typeSpeed <= 0f)
            {
                sb.Append(line);
                UpdateTerminalText();
                Canvas.ForceUpdateCanvases();
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
                yield break; 
            }

            float timer = 0f;
            int charIndex = 0;
            isType = true;
            while (charIndex < line.Length)
            {
                // 프레임이 넘어가는 동안 걸린 시간을 누적
                timer += Time.deltaTime;

                // 누적된 시간이 typeSpeed를 넘었다면, 그만큼 계산해서 여러 글자를 한 번에 출력
                int charsToAdd = Mathf.FloorToInt(timer / typeSpeed);
                
                if (charsToAdd > 0)
                {
                    // 남은 글자 수보다 많이 출력하지 않도록 제한
                    int remainingChars = line.Length - charIndex;
                    int charsToActuallyAdd = Mathf.Min(charsToAdd, remainingChars);

                    // 한 번에 잘라내서 추가
                    sb.Append(line.Substring(charIndex, charsToActuallyAdd));
                    charIndex += charsToActuallyAdd;
                    
                    // 처리한 글자 수만큼 누적 시간 차감
                    timer -= (charsToActuallyAdd * typeSpeed);

                    UpdateTerminalText();
                    Canvas.ForceUpdateCanvases();
                    if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
                }
                isType = false;
                // 다음 프레임까지 대기
                yield return null; 
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
            // TextMeshPro에서 글자를 투명하게 만드는 태그
            string invisibleCursor = $"<alpha=#00>{cursorChar}</alpha=#FF>";

            while (true)
            {
                if (isType)
                {
                    // 부팅 텍스트가 한창 출력 중일 때는 깜빡이지 않고 커서를 켜둠
                    yield return null; 
                }
                else
                {
                    // 대기 상태일 때만 깜빡임
                    terminalText.text = sb.ToString() + cursorChar;
                    yield return new WaitForSeconds(cursorBlinkRate);
                    
                    terminalText.text = sb.ToString() + invisibleCursor;
                    yield return new WaitForSeconds(cursorBlinkRate);
                }
            }
        }

        void UpdateTerminalText()
        {
            terminalText.text = sb.ToString() + cursorChar;
        }
    }
}
