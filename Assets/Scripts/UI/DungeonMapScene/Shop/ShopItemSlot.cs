using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Data;
namespace UI.Shop
{
    public class ShopItemSlot : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject highlightBackground; // 선택되었을 때 켜질 배경 (또는 커서)
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI quantityText;
        public TextMeshProUGUI priceText;
        
        [Header("Buttons")]
        public Button leftButton;
        public Button rightButton;

        public BaseItemData currentItem { get; private set; }

        public void Setup(BaseItemData itemData, bool isSell = false)
        {
            currentItem = itemData;
            nameText.text = itemData.dataName;
            
            if (isSell) priceText.text = Mathf.FloorToInt(itemData.price / 2f).ToString();
            else priceText.text = itemData.price.ToString();
            
            UpdateQuantityText(0);
            SetHighlight(false);
        }

        public void SetHighlight(bool isHighlighted)
        {
            highlightBackground.SetActive(isHighlighted);
            // 하이라이트 해제 시 구매 개수 초기화 (옵션)
            if (!isHighlighted) 
            {
                UpdateQuantityText(0);
            }
        }

        public void UpdateQuantityText(int amount)
        {
            quantityText.text = amount.ToString("D2"); // 최대 99. 두 자리 수 고정
        }
    }
}
