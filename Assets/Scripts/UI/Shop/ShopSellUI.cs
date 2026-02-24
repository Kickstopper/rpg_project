using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager; 

namespace UI.Shop
{
    public class ShopSellUI : MonoBehaviour
    {
        [Header("Sell UI Container")]
        public GameObject sellUI;

        [Header("Category Tabs")]
        public Button[] tabButtons; // Weapon, Armor, Etc 순
        public GameObject[] tabFocusIndicators;

        [Header("Scroll View & Prefab")]
        public Transform contentPanel;
        public GameObject itemPrefab;

        [Header("UI References")]
        public TextMeshProUGUI possessionText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI totalPriceText;
        public TextMeshProUGUI highlightNameText;
        public TextMeshProUGUI highlightDescText;
        public Button confirmButton;

        private List<ShopItemSlot> spawnedSlots = new List<ShopItemSlot>();
        private List<InventoryItem> currentFilteredItems = new List<InventoryItem>();
        
        private int currentTabIndex = 0;
        private int currentHighlightIndex = -1; // -1은 탭 포커스 상태, 0 이상은 아이템 리스트 포커스 상태
        private int currentSellQuantity = 0;
        private int currentMaxPossession = 0;

        void Start()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmSellClicked);
                confirmButton.gameObject.SetActive(false);
            }

            // 탭 버튼 클릭 이벤트 연결
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
            }
        }

        void Update()
        {
            HandleInput();
        }

        public void OpenSellMode()
        {
            sellUI.SetActive(true);
            currentTabIndex = 0; // 최초 탭은 WEAPON
            ChangeTab(currentTabIndex);
            UpdatePlayerMoneyUI();
        }

        // 탭 변경 로직
        private void ChangeTab(int tabIndex)
        {
            currentTabIndex = tabIndex;
            tabButtons[tabIndex].Select();
            
            currentFilteredItems = InventoryManager.Instance.GetSellableItems((ItemCategory)currentTabIndex);
            PopulateList(currentFilteredItems);
            // 탭을 전환하면 포커스를 탭 영역(-1)으로 초기화
            ChangeHighlight(-1);
            UpdateTabVisuals();
        }

        private void OnTabClicked(int index)
        {
            if (currentTabIndex == index) return;
            ChangeTab(index);
        }

        private void PopulateList(List<InventoryItem> items)
        {
            ClearContent();

            for (int i = 0; i < items.Count; i++)
            {
                var invItem = items[i];
                GameObject go = Instantiate(itemPrefab, contentPanel);
                ShopItemSlot slot = go.GetComponent<ShopItemSlot>();
                
                // 판매 가격은 원래 가격의 절반, 초기 수량은 0
                slot.Setup(invItem.baseData, true); 
                
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

        private void HandleInput()
        {
            // 탭 전환
            if (Input.GetKeyDown(KeyCode.Q)) MoveTab(-1);
            if (Input.GetKeyDown(KeyCode.E)) MoveTab(1);

            
            if (currentHighlightIndex == -1)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveTab(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveTab(1);

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    if (spawnedSlots.Count > 0)
                    {
                        ChangeHighlight(0); // 첫 번째 아이템 선택
                    }
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    // 리스트 영역에 포커스가 있을 때 첫 아이템에서 위를 누르면 탭으로 포커스 이동
                    if (currentHighlightIndex == 0) ChangeHighlight(-1); // 탭으로 복귀
                    else ChangeHighlight(currentHighlightIndex - 1);
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    if (currentHighlightIndex < spawnedSlots.Count - 1)
                        ChangeHighlight(currentHighlightIndex + 1);
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    ChangeQuantity(-1);
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    ChangeQuantity(1);
                }

                // 판매
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    if (currentSellQuantity > 0) OnConfirmSellClicked();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                CloseSellMode();
            }
        }

        private void MoveTab(int direction)
        {
            int nextTab = currentTabIndex + direction;
            if (nextTab < 0) nextTab = tabButtons.Length - 1;
            if (nextTab >= tabButtons.Length) nextTab = 0;
            
            ChangeTab(nextTab);
        }

        private void ChangeHighlight(int newIndex)
        {
            // 기존 하이라이트 해제
            if (currentHighlightIndex >= 0 && currentHighlightIndex < spawnedSlots.Count)
            {
                spawnedSlots[currentHighlightIndex].SetHighlight(false);
            }

            currentHighlightIndex = newIndex;
            currentSellQuantity = 0; // 대상이 바뀌면 수량 초기화

            if (currentHighlightIndex == -1)
            {
                // 탭 포커스 시 UI 정리
                highlightNameText.text = "";
                highlightDescText.text = "";
                possessionText.text = "0/99"; // 기본값
                totalPriceText.text = "$0";
                if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            }
            else
            {
                // 아이템 포커스 시 UI 갱신
                spawnedSlots[currentHighlightIndex].SetHighlight(true);
                var itemInfo = currentFilteredItems[currentHighlightIndex];
                
                highlightNameText.text = itemInfo.baseData.dataName;
                highlightDescText.text = itemInfo.baseData.description;
                currentMaxPossession = itemInfo.amount; // 소지 개수 저장
                
                UpdatePriceUI();
            }

            UpdateTabVisuals();
        }

        public void ChangeQuantity(int change)
        {
            if (currentHighlightIndex == -1 || spawnedSlots.Count == 0) return;

            currentSellQuantity += change;

            // 최소 0개, 최대치는 소지하고 있는 개수로 제한
            if (currentSellQuantity < 0) currentSellQuantity = 0;
            if (currentSellQuantity > currentMaxPossession) currentSellQuantity = currentMaxPossession;

            spawnedSlots[currentHighlightIndex].UpdateQuantityText(currentSellQuantity);
            UpdatePriceUI();
        }

        private void UpdatePriceUI()
        {
            if (currentHighlightIndex == -1) return;

            int sellPrice = Mathf.FloorToInt(currentFilteredItems[currentHighlightIndex].baseData.price / 2f);
            int totalCost = sellPrice * currentSellQuantity;
            
            totalPriceText.text = $"${totalCost}";
            possessionText.text = $"{currentMaxPossession}/99";

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(currentSellQuantity > 0);
            }
        }

        // 판매 확인 및 갱신
        private void OnConfirmSellClicked()
        {
            if (currentHighlightIndex == -1 || currentSellQuantity <= 0) return;

            var invItem = currentFilteredItems[currentHighlightIndex];
            int sellPrice = Mathf.FloorToInt(invItem.baseData.price / 2f);
            int totalEarned = sellPrice * currentSellQuantity;

            InventoryManager.Instance.RemoveItem(invItem.baseData.id, currentSellQuantity);
            InventoryManager.Instance.AddMoney(totalEarned);

            Debug.Log($"{invItem.baseData.dataName} {currentSellQuantity}개 판매 완료. (+${totalEarned})");

            // 판매 후, 해당 탭의 아이템 리스트를 다시 불러와 UI를 갱신. 아이템을 모두 팔아 0개가 됐을 때 리스트에서 제거되므로
            ChangeTab(currentTabIndex); 
            UpdatePlayerMoneyUI();
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabFocusIndicators.Length; i++)
            {
                if (tabFocusIndicators[i] != null)
                {
                    bool isFocused = (currentHighlightIndex == -1 && currentTabIndex == i);
                    tabFocusIndicators[i].SetActive(isFocused);
                }
            }
        }

        private void UpdatePlayerMoneyUI()
        {
            moneyText.text = $"${InventoryManager.Instance.GetMoney()}";
        }

        private void ClearContent()
        {
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedSlots.Clear();
        }

        public void CloseSellMode()
        {
            sellUI.SetActive(false);
        }
    }
}