using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using UnityEngine.EventSystems;

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
            
            currentFilteredItems = ManagerRoot.Inventory.GetSellableItems((ItemCategory)currentTabIndex);
            PopulateList(currentFilteredItems);
            // 탭을 전환하면 포커스를 탭 영역(-1)으로 초기화
            ChangeHighlight(-1);
            UpdateTabFocusIndicator();
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
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown())
            {
                CloseSellMode();
                return;
            }

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
                    if (currentHighlightIndex == 0) ChangeTab(currentTabIndex); // 탭으로 복귀
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
                possessionText.text = "0/0";
                totalPriceText.text = "0";
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
                // AutoScrollRect가 변경을 감지하고 스크롤을 따라가도록 EventSystem에게 현재 선택된 UI를 알려줌.
                EventSystem.current.SetSelectedGameObject(spawnedSlots[currentHighlightIndex].gameObject);
            }

            UpdateTabFocusIndicator();
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
            InventoryItem currentItem = currentFilteredItems[currentHighlightIndex];
            int sellPrice = currentItem.baseData.sellPrice;
            int totalCost = sellPrice * currentSellQuantity;
            
            totalPriceText.text = $"${totalCost}";
            possessionText.text = $"{currentMaxPossession}/{currentItem.baseData.maxStackCount}";

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
            
            int sellPrice = invItem.baseData.sellPrice; 
            int totalEarned = sellPrice * currentSellQuantity;

            ManagerRoot.Inventory.RemoveItem(invItem.baseData.id, currentSellQuantity);
            ManagerRoot.Inventory.AddMoney(totalEarned);

            Debug.Log($"{invItem.baseData.dataName} {currentSellQuantity}개 판매 완료. (+${totalEarned})");

            int remainAmount = ManagerRoot.Inventory.GetItemCount(invItem.baseData.id);
            if (remainAmount <= 0)
            {
                // 0개가 되어 리스트에서 지워야 할 때만 새로고침을 수행
                currentFilteredItems = ManagerRoot.Inventory.GetSellableItems((ItemCategory)currentTabIndex);
                PopulateList(currentFilteredItems);

                // 커서 위치 재조정
                if (currentFilteredItems.Count == 0)
                {
                    // 더 이상 팔 아이템이 없다면 탭으로 포커스를 돌려보냄
                    ChangeHighlight(-1); 
                }
                else
                {
                    // 아직 팔 아이템이 남았다면, 현재 인덱스를 유지하거나 맨 마지막 아이템을 가리키게 함.
                    int newIndex = Mathf.Min(currentHighlightIndex, currentFilteredItems.Count - 1);
                    ChangeHighlight(newIndex);
                }
            }
            else
            {
                // 아직 아이템이 남아있다면 슬롯을 파괴하거나 생성하지 않고 정보만 갱신
                currentMaxPossession = remainAmount;
                ChangeHighlight(currentHighlightIndex); 
            }

            UpdatePlayerMoneyUI();
        }

        private void UpdateTabFocusIndicator()
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
            moneyText.text = $"${ManagerRoot.Inventory.GetMoney()}";
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