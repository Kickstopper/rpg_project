using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Data;
using Manager;
using Helper;

namespace UI.CharacterCreationScene
{
    public class VirtualKeyboard : MonoBehaviour
    {
        [Header("Keyboard Panels")]
        [SerializeField] private GameObject englishUpperPanel;
        [SerializeField] private GameObject englishLowerPanel;
        [SerializeField] private GameObject koreanPanel;
        [SerializeField] private GameObject japanesePanel;

        [Header("First Keys For Navigation")]
        [SerializeField] private Button firstSelectedKey;

        [Header("Input Limits")]
        [Tooltip("최대 입력 가능한 글자 수 설정")]
        [SerializeField] private int maxCharacterLimit = 8;

        [Header("Cursor Settings")]
        [SerializeField] private string cursorChar = "█"; // 커서 깜박임 효과용
        [SerializeField] private float blinkRate = 0.5f;

        [SerializeField] private TMP_InputField targetInputField;

        private HangulCombiner hangulCombiner = new HangulCombiner();

        // 실제 유저가 입력한 순수 데이터
        private string inputText = "";
        
        // 커서 깜박임 상태 관리
        private bool isCursorVisible = true;
        private float cursorTimer = 0f;
        
        // 대문자 고정 여부
        private bool isCapsLock = false;
        
        public enum KeyboardLanguage { English, Korean, Japanese }
        private KeyboardLanguage currentLanguage = KeyboardLanguage.English;

        private void Update()
        {
            // 커서 깜박임 타이머 처리
            cursorTimer += Time.deltaTime;
            if (cursorTimer >= blinkRate)
            {
                cursorTimer = 0f;
                isCursorVisible = !isCursorVisible;
                UpdateDisplay();
            }

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                OnBackspacePress();
            }
        }

        // 언어 전환 및 패널 활성화 로직
        public void ToggleLanguage()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
            currentLanguage = (KeyboardLanguage)(((int)currentLanguage + 1) % 3);

            isCapsLock = false; // 언어가 바뀌면 무조건 소문자를 우선 표시
            englishUpperPanel.SetActive(false); 
            englishLowerPanel.SetActive(currentLanguage == KeyboardLanguage.English);
            koreanPanel.SetActive(currentLanguage == KeyboardLanguage.Korean);
            japanesePanel.SetActive(currentLanguage == KeyboardLanguage.Japanese);
        }
        
        public void FocusFirstKey()
        {
            firstSelectedKey?.Select();
        }

        public void OnKeyPress(string character)
        {
            if (character.Length > 0)
            {
                // 조합 전 오토마타 상태를 백업
                hangulCombiner.CloneState(out int prevCho, out int prevJung, out int prevJong);

                // 오토마타를 거친 예상 결합 텍스트를 미리 생성
                string nextText = hangulCombiner.InputChar(inputText, character[0]);

                // 3글자 수 초과 검사
                if (nextText.Length > maxCharacterLimit)
                {
                    hangulCombiner.RestoreState(prevCho, prevJung, prevJong); // 상태 롤백
                    return;
                }

                // 폰트 지원 여부 검사
                if (nextText.Length > 0)
                {
                    char lastChar = nextText[nextText.Length - 1];
                    
                    // TMP_InputField의 Text 컴포넌트에 적용된 폰트 에셋이 해당 글자를 지원하는지 확인
                    bool isCharacterSupported = targetInputField.textComponent.font.HasCharacter(lastChar);
                    
                    if (!isCharacterSupported)
                    {
                        // 폰트에 없는 글자라면 결합을 취소하고 오토마타 상태를 이전으로 되돌림
                        hangulCombiner.RestoreState(prevCho, prevJung, prevJong);
                        
                        // 지원하지 않는 글자 입력 시 경고음
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                        return;
                    }
                }

                // 모든 조건을 통과했을 때만 실제 텍스트로 반영
                inputText = nextText;
                
                isCursorVisible = true;
                cursorTimer = 0f;
                UpdateDisplay();
            }
        }

        public void OnBackspacePress()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
            inputText = hangulCombiner.DeleteChar(inputText);
            isCursorVisible = true;
            cursorTimer = 0f;
            UpdateDisplay();
        }

        public void ToggleCapsLock()
        {
            if (currentLanguage != KeyboardLanguage.English) return;

            isCapsLock = !isCapsLock;
            
            if (isCapsLock)
            {
                englishUpperPanel.SetActive(true);
                englishLowerPanel.SetActive(false);
            }
            else
            {
                englishUpperPanel.SetActive(false);
                englishLowerPanel.SetActive(true);
            }
        }
        
        public void ClearInput()
        {
            inputText = "";
            hangulCombiner.ResetState();
            isCursorVisible = true;
            cursorTimer = 0f;
            UpdateDisplay();
        }

        public void ForceUpdateDisplay()
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            string blinkCursor = isCursorVisible ? cursorChar : $"<color=#00000000>{cursorChar}</color>";

            if (string.IsNullOrEmpty(inputText))
            {
                string placeholderStr = "";
                TextMeshProUGUI placeholderText = targetInputField.placeholder as TextMeshProUGUI;
                
                if (placeholderText != null)
                {
                    placeholderStr = placeholderText.text;
                }
                targetInputField.text = $"<color=#888888>{placeholderStr}</color>";
            }
            else
            {
                targetInputField.text = inputText + blinkCursor;
            }
        }

        public string GetInputText()
        {
            return inputText;
        }
    }
}
