using UnityEngine;
using TMPro;

namespace UI.CharacterCreationScene
{
    public class VirtualKeyboard : MonoBehaviour
    {
        [SerializeField] private TMP_InputField targetInputField;

        // ㄱㄴㄷ, A~Z 등 문자 버튼 OnClick에 할당됨
        public void OnKeyPress(string character)
        {
            targetInputField.text += character;
        }

        // Backspace 버튼 OnClick에 할당됨
        public void OnBackspacePress()
        {
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
