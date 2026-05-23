using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Data;
using Manager;

namespace UI.CharacterCreationScene
{
    public class VirtualKeyboard : MonoBehaviour
    {
        [SerializeField] private TMP_InputField targetInputField;

        [Header("Keyboard Navigation")]
        [SerializeField] private Button firstSelectedKey;

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

        public void FocusFirstKey()
        {
            if (firstSelectedKey != null)
            {
                firstSelectedKey.Select();
            }
        }

        // ㄱㄴㄷ, A~Z 등 문자 버튼 OnClick에 할당됨
        public void OnKeyPress(string character)
        {
            targetInputField.text += character;
        }

        // Backspace 버튼 OnClick에 할당됨
        public void OnBackspacePress()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            if (targetInputField.text.Length > 0)
            {
                targetInputField.text = targetInputField.text.Substring(0, targetInputField.text.Length - 1);
            }
        }
        
        // 입력창 초기화
        public void ClearInput()
        {
            targetInputField.text = "";
        }
    }
}
