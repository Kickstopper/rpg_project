using UnityEngine;
using UnityEngine.UI;
using Manager;
using TMPro;
using Data;
using DG.Tweening;

namespace UI.Shop
{
    public class ShopModeSelectUI : MonoBehaviour
    {
        [Header("Sub Panels")]
        public ShopBuyUI buyUI;
        public ShopSellUI sellUI;
        public ShopEquipUI equipUI;

        [Header("UI References")]
        public Image background;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI possessionText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI totalPriceText;
        public Image fadeOverlay;
        
        [Header("Mode Buttons")]
        public Button[] modeButtons; // 0: BUY, 1: SELL, 2: EQUIP
        public GameObject[] modeHighlights; // 선택된 버튼의 하이라이트

        private BgmID prevBgmID;
        
        private int currentIndex = 0;
        private string currentShopID;
        private bool wasSubPanelActive = false;
        void Start()
        {
            // 마우스 클릭 이벤트 연결
            for (int i = 0; i < modeButtons.Length; i++)
            {
                int index = i;
                modeButtons[i].onClick.AddListener(() => OnButtonClicked(index));
            }
        }

        void Update()
        {
            if (GameStateManager.Instance.CurrentState != GameState.Shop) return;
            if (string.IsNullOrEmpty(currentShopID)) return;

            bool isCurrentlyActive = IsAnySubPanelActive();

            // 하위 패널이 켜져 있다가 방금 막 꺼졌다면
            if (wasSubPanelActive && !isCurrentlyActive)
            {
                UpdateTextUI();
                // 현재 인덱스 버튼에 다시 포커스.
                modeButtons[currentIndex].Select();
            }

            // 다음 프레임 비교를 위해 현재 상태를 저장
            wasSubPanelActive = isCurrentlyActive;

            // 하위 패널이 하나라도 켜져 있다면 상위 입력을 무시
            if (isCurrentlyActive) return;

            HandleInput();
        }

        public void OpenShop(string shopID)
        {
            currentShopID = shopID;

            ShopData data = ShopManager.Instance.GetShopData(shopID);
            if (data != null)
            {
                if (titleText != null)
                    titleText.text = data.displayName;

                if (background != null)
                {
                    Sprite img = data.BackgroundImage;
                    background.sprite = img;
                    background.color = img != null ? Color.white : Color.clear; 
                }
                if (data.bgmID != BgmID.None)
                {
                    prevBgmID = SoundManager.Instance.CurrentBgmID;
                    SoundManager.Instance.PlayBGM(data.bgmID);
                }
            }

            // 모든 하위 패널 끄기
            if(buyUI != null) buyUI.gameObject.SetActive(false);
            if(sellUI != null) sellUI.gameObject.SetActive(false);
            if(equipUI != null) equipUI.gameObject.SetActive(false);

            UpdateTextUI();
            
            ChangeHighlight(0); // 초기 포커스는 BUY

            if (fadeOverlay != null)
            {
                fadeOverlay.gameObject.SetActive(true);
                fadeOverlay.color = Color.black;
                fadeOverlay.DOFade(0, 1f).OnComplete(()=> fadeOverlay.gameObject.SetActive(false));
            }
        }

        private void UpdateTextUI()
        {
            if (moneyText != null) moneyText.text = $"${InventoryManager.Instance.GetMoney()}";
            if (totalPriceText != null) totalPriceText.text = "-";
            if (possessionText != null) possessionText.text = "-";
        }

        private void HandleInput()
        {
            // 취소
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || UI.Common.GameInput.GetCancelDown())
            {
                ExitShop();
                return;
            }

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveHighlight(-1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveHighlight(1);
            }

            // 확인
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ConfirmSelection();
            }
        }

        private void MoveHighlight(int direction)
        {
            int nextIndex = currentIndex + direction;

            // 셀렉트 순환
            if (nextIndex < 0) nextIndex = modeButtons.Length - 1;
            if (nextIndex >= modeButtons.Length) nextIndex = 0;

            ChangeHighlight(nextIndex);
        }

        private void ChangeHighlight(int newIndex)
        {
            currentIndex = newIndex;

            modeButtons[newIndex].Select();

            for (int i = 0; i < modeHighlights.Length; i++)
            {
                if (modeHighlights[i] != null)
                {
                    modeHighlights[i].SetActive(i == currentIndex);
                }
            }
        }

        private void OnButtonClicked(int index)
        {
            ChangeHighlight(index);
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            switch (currentIndex)
            {
                case 0: // BUY
                    if (buyUI != null)
                    {
                        buyUI.gameObject.SetActive(true);
                        buyUI.OpenShop(currentShopID);
                    }
                    break;
                case 1: // SELL
                    if (sellUI != null)
                    {
                        sellUI.gameObject.SetActive(true);
                        sellUI.OpenSellMode();
                    }
                    break;
                case 2: // EQUIP
                    if (equipUI != null)
                    {
                        equipUI.gameObject.SetActive(true);
                        equipUI.OpenEquipMode();
                    }
                    break;
            }
        }

        private void ExitShop()
        {
            if (SoundManager.Instance != null)
            {
                if (prevBgmID != BgmID.None)
                    SoundManager.Instance.PlayBGM(prevBgmID);
                else
                    SoundManager.Instance.StopBGM();
                
            }

            prevBgmID = BgmID.None;
            currentShopID = null;

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ChangeState(GameState.Exploration);
            }
        }

        private bool IsAnySubPanelActive()
        {
            bool isBuyActive = buyUI != null && buyUI.gameObject.activeInHierarchy;
            bool isSellActive = sellUI != null && sellUI.gameObject.activeInHierarchy;
            bool isEquipActive = equipUI != null && equipUI.gameObject.activeInHierarchy;

            return isBuyActive || isSellActive || isEquipActive;
        }
    }
}