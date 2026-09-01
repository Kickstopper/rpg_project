using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Data;
using Manager;
using Helper;
using UnityEngine.EventSystems;

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
        [SerializeField] private Button engLowerFirstKey;
        [SerializeField] private Button engUpperFirstKey;
        [SerializeField] private Button korFirstKey;
        [SerializeField] private Button jpnFirstKey;

        [Header("Input Limits")]
        [SerializeField] private int maxCharacterLimit = 8;

        [Header("Cursor Settings")]
        [SerializeField] private string cursorChar = "█";
        [SerializeField] private float blinkRate = 0.5f;

        [SerializeField] private TMP_InputField targetInputField;

        private HangulCombiner hangulCombiner = new HangulCombiner();

        private string inputText = "";
        private bool isCursorVisible = true;
        private float cursorTimer = 0f;
        private bool isCapsLock = false;
        
        public enum KeyboardLanguage { English, Korean, Japanese }
        private KeyboardLanguage currentLanguage = KeyboardLanguage.English;

        // 포커스 복구를 위한 변수
        private GameObject lastSelectedKey;

        private void Start()
        {
            ApplyLanguagePanels();
            FocusFirstKey();
        }

        private void Update()
        {
            // 커서 깜박임
            cursorTimer += Time.deltaTime;
            if (cursorTimer >= blinkRate)
            {
                cursorTimer = 0f;
                isCursorVisible = !isCursorVisible;
                UpdateDisplay();
            }

            // 현재 선택된 버튼 기억하기
            var currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<Button>() != null)
            {
                lastSelectedKey = currentSelected;
            }

            bool isDirectionalKey = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                                    Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow);
            
            bool isActionKey = UI.Common.GameInput.GetSelectDown();

            // 빈 공간 클릭 시 포커스 증발 방어 로직 (유저가 방향키나 엔터를 누르면 즉시 복구)
            if (currentSelected == null)
            {
                if (isDirectionalKey || isActionKey || Input.GetKeyDown(KeyCode.Backspace))
                {
                    if (lastSelectedKey != null && lastSelectedKey.activeInHierarchy)
                        EventSystem.current.SetSelectedGameObject(lastSelectedKey);
                    else
                        FocusFirstKey();
                }
            }

            // 사운드 및 입력 처리
            if (isDirectionalKey)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                return;
            }

            if (isActionKey)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                return;
            }
            
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                OnBackspacePress();
            }
        }

        private void ApplyLanguagePanels()
        {
            englishUpperPanel.SetActive(currentLanguage == KeyboardLanguage.English && isCapsLock);
            englishLowerPanel.SetActive(currentLanguage == KeyboardLanguage.English && !isCapsLock);
            koreanPanel.SetActive(currentLanguage == KeyboardLanguage.Korean);
            japanesePanel.SetActive(currentLanguage == KeyboardLanguage.Japanese);
        }

        public void ToggleLanguage()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
            currentLanguage = (KeyboardLanguage)(((int)currentLanguage + 1) % 3);
            isCapsLock = false; 

            ApplyLanguagePanels();
            FocusFirstKey(); // 언어가 바뀌면 첫 번째 키로 포커스 초기화
        }
        
        public void ToggleCapsLock()
        {
            if (currentLanguage != KeyboardLanguage.English) return;

            isCapsLock = !isCapsLock;
            
            ApplyLanguagePanels();
            FocusFirstKey();
        }

        public void FocusFirstKey()
        {
            if (currentLanguage == KeyboardLanguage.English)
            {
                if (isCapsLock)
                {
                    if (engUpperFirstKey != null && engUpperFirstKey.gameObject.activeInHierarchy) 
                        engUpperFirstKey.Select();
                }
                else
                {
                    if (engLowerFirstKey != null && engLowerFirstKey.gameObject.activeInHierarchy) 
                        engLowerFirstKey.Select();
                }
            }
            else if (currentLanguage == KeyboardLanguage.Korean)
            {
                if (korFirstKey != null && korFirstKey.gameObject.activeInHierarchy) 
                    korFirstKey.Select();
            }
            else if (currentLanguage == KeyboardLanguage.Japanese)
            {
                if (jpnFirstKey != null && jpnFirstKey.gameObject.activeInHierarchy) 
                    jpnFirstKey.Select();
            }
            
        }

        public void OnKeyPress(string character)
        {
            if (character.Length > 0)
            {
                hangulCombiner.CloneState(out int prevCho, out int prevJung, out int prevJong);
                string nextText = hangulCombiner.InputChar(inputText, character[0]);

                if (nextText.Length > maxCharacterLimit)
                {
                    hangulCombiner.RestoreState(prevCho, prevJung, prevJong);
                    return;
                }

                if (nextText.Length > 0)
                {
                    char lastChar = nextText[nextText.Length - 1];
                    bool isCharacterSupported = targetInputField.textComponent.font.HasCharacter(lastChar);
                    
                    if (!isCharacterSupported)
                    {
                        hangulCombiner.RestoreState(prevCho, prevJung, prevJong);
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                        return;
                    }
                }

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