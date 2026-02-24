using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Data;
using Manager;
namespace UI.Shop
{
    public class ShopBuyUI : MonoBehaviour
    {
        [Header("Buy UI Container")]
        public GameObject buyUI;
        [Header("Scroll View & Prefab")]
        public Transform contentPanel;
        public GameObject itemPrefab;

        [Header("Top/Right Info UI")]
        public TextMeshProUGUI highlightNameText;
        public TextMeshProUGUI highlightDescText;

        [Header("Bottom Left Player Info UI")]
        public TextMeshProUGUI possessionText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI totalPriceText;
        
        [Header("Interaction")]
        public Button confirmButton;

        private List<ShopItemSlot> spawnedSlots = new List<ShopItemSlot>();
        private int currentHighlightIndex = 0;
        private int currentPurchaseQuantity = 0;
        
        private ShopData currentShopData;
        private BaseItemData HighlightedItem => spawnedSlots[currentHighlightIndex].currentItem;

        void Start()
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            confirmButton.gameObject.SetActive(false);
        }

        void Update()
        {
            HandleInput();
        }

        // 던전 문 통과 시 호출될 진입점
        public void OpenShop(string shopID)
        {
            buyUI.SetActive(true);
            currentShopData = ShopManager.Instance.GetShopData(shopID);
            ClearContent();
            PopulateShop(currentShopData);
            
            currentHighlightIndex = 0;
            currentPurchaseQuantity = 0;
            
            UpdatePlayerMoneyUI();
            SelectSlot(currentHighlightIndex);
        }

        private void PopulateShop(ShopData shopData)
        {
            for (int i = 0; i < shopData.itemsForSale.Count; i++)
            {
                var item = shopData.itemsForSale[i];
                GameObject go = Instantiate(itemPrefab, contentPanel);
                ShopItemSlot slot = go.GetComponent<ShopItemSlot>();
                slot.Setup(item);
                
                int slotIndex = i; 
                
                slot.leftButton.onClick.AddListener(() => {
                    ChangeHighlight(slotIndex);
                    ChangeQuantity(-1);
                });
                
                slot.rightButton.onClick.AddListener(() => {
                    ChangeHighlight(slotIndex);
                    ChangeQuantity(1);
                });
                
                spawnedSlots.Add(slot);
            }
        }

        // 마우스 클릭 시 하이라이트를 직접 지정하는 메서드
        private void ChangeHighlight(int newIndex)
        {
            if (currentHighlightIndex == newIndex) return;

            spawnedSlots[currentHighlightIndex].SetHighlight(false);
            currentHighlightIndex = newIndex;
            currentPurchaseQuantity = 0; // 다른 아이템을 선택했으므로 구매 개수 초기화
            
            SelectSlot(currentHighlightIndex);
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseBuyMode();
                return;
            }

            if (spawnedSlots.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveHighlight(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveHighlight(1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                ChangeQuantity(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                ChangeQuantity(1);
            }
            
            // 확인 키
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                if (currentPurchaseQuantity > 0)
                {
                    OnConfirmButtonClicked();
                }
            }
        }

        private void MoveHighlight(int direction)
        {
            int prevIndex = currentHighlightIndex;
            currentHighlightIndex += direction;

            if (currentHighlightIndex < 0) currentHighlightIndex = 0;
            if (currentHighlightIndex >= spawnedSlots.Count) currentHighlightIndex = spawnedSlots.Count - 1;

            if (prevIndex != currentHighlightIndex)
            {
                currentPurchaseQuantity = 0; // 하이라이트 이동 시 개수 초기화
                spawnedSlots[prevIndex].SetHighlight(false);
                SelectSlot(currentHighlightIndex);
            }
        }

        // 하이라이트된 아이템 정보 갱신
        private void SelectSlot(int index)
        {
            spawnedSlots[index].SetHighlight(true);
            BaseItemData item = HighlightedItem;

            highlightNameText.text = item.dataName;
            highlightDescText.text = item.description;

            int possessedAmount = InventoryManager.Instance.GetItemCount(item.id);
            possessionText.text = $"{possessedAmount}/99"; // 일단은 전부 99개가 최대
            
            UpdatePriceUI();
        }

        // 구매 개수 설정
        public void ChangeQuantity(int change)
        {
            currentPurchaseQuantity += change;

            if (currentPurchaseQuantity < 0) currentPurchaseQuantity = 0;
            if (currentPurchaseQuantity > 99) currentPurchaseQuantity = 99;

            spawnedSlots[currentHighlightIndex].UpdateQuantityText(currentPurchaseQuantity);
            UpdatePriceUI();
        }

        // 가격 표시 및 확인 버튼 제어
        private void UpdatePriceUI()
        {
            int totalCost = HighlightedItem.price * currentPurchaseQuantity;
            totalPriceText.text = $"${totalCost}";

            // 개수가 1 이상이면 확인 버튼 활성화
            confirmButton.gameObject.SetActive(currentPurchaseQuantity > 0);
        }

        // 구매 확정 로직
        private void OnConfirmButtonClicked()
        {
            int PlayerMoney = InventoryManager.Instance.GetMoney();
            int totalCost = HighlightedItem.price * currentPurchaseQuantity;
            
            if(PlayerMoney >= totalCost)
            {
                InventoryManager.Instance.AddItem(HighlightedItem.id, currentPurchaseQuantity);
                int currentMoney = PlayerMoney - totalCost;
                InventoryManager.Instance.SetMoney(currentMoney);
                Debug.Log($"{HighlightedItem.dataName} {currentPurchaseQuantity}개 구매 완료!");

            }
            // 구매 후 UI 실시간 갱신
            currentPurchaseQuantity = 0;
            spawnedSlots[currentHighlightIndex].UpdateQuantityText(currentPurchaseQuantity);
            
            UpdatePlayerMoneyUI();
            SelectSlot(currentHighlightIndex); // POSSESSION 갱신을 위해 재호출
            
            confirmButton.gameObject.SetActive(false);
        }

        private void UpdatePlayerMoneyUI()
        {
            moneyText.text = $"${InventoryManager.Instance.GetMoney()}";
        }

        private void ClearContent()
        {
            foreach (var slot in spawnedSlots)
            {
                Destroy(slot.gameObject);
            }
            spawnedSlots.Clear();
        }

        public void CloseBuyMode()
        {
            buyUI.SetActive(false);
        }
    }

}
