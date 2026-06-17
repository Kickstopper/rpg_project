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
        [SerializeField] private GameObject englishPanel;
        [SerializeField] private GameObject koreanPanel;
        [SerializeField] private GameObject japanesePanel;

        [Header("First Keys For Navigation")]
        [SerializeField] private Button firstSelectedKey;

        [SerializeField] private TMP_InputField targetInputField;

        private HangulCombiner hangulCombiner = new HangulCombiner();
        
        public enum KeyboardLanguage { English, Korean, Japanese }
        private KeyboardLanguage currentLanguage = KeyboardLanguage.English;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
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
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
            
            // 다음 언어로 순환
            currentLanguage = (KeyboardLanguage)(((int)currentLanguage + 1) % 3);
            
            englishPanel.SetActive(currentLanguage == KeyboardLanguage.English);
            koreanPanel.SetActive(currentLanguage == KeyboardLanguage.Korean);
            japanesePanel.SetActive(currentLanguage == KeyboardLanguage.Japanese);
        }
        
        public void FocusFirstKey()
        {
            if (firstSelectedKey != null)
            {
                firstSelectedKey.Select();
            }
        }

        public void OnKeyPress(string character)
        {
            if (character.Length > 0)
            {
                // 한글 조합기에 현재 텍스트와 입력된 단일 자모를 넘겨서 처리된 문자열을 받아옴.
                targetInputField.text = hangulCombiner.InputChar(targetInputField.text, character[0]);
            }
        }

        public void OnBackspacePress()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            targetInputField.text = hangulCombiner.DeleteChar(targetInputField.text);
        }
        
        public void ClearInput()
        {
            targetInputField.text = "";
            hangulCombiner.ResetState(); // 초기화 시 오토마타 상태도 반드시 초기화 필요
        }
    }
}
