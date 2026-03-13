using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
namespace UI.PlayerMenu
{
    public class AppItemUI : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI blockText;
        public Image bgImage;
        public Color bgColor;
        public Color selectColor;
        public Image blockColor;
        public CanvasGroup canvasGroup;
        
        public void Setup(GameAppData data, bool isInstalled)
        {
            nameText.text = data.appName;
            blockText.text = $"{data.memoryCost}";
            blockColor.color = data.blockColor;
            // 설치된 앱은 Grayout 투명도를 낮춤
            canvasGroup.alpha = isInstalled ? 0.5f : 1.0f;
        }

        public void SetHighlight(bool isSelected)
        {
            // 선택되었을 때 배경색 변경
            bgImage.color = isSelected ? selectColor : bgColor;
        }
    }
}