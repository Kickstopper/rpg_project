using UnityEngine;
using UnityEngine.UI;
using Manager;

namespace UI.Shop
{
    public class ShopModeSelectUI : MonoBehaviour
    {
        [Header("Sub Panels")]
        public ShopBuyUI buyUI;
        public ShopSellUI sellUI;
        public GameObject equipUI; // 임시

        [Header("Mode Buttons")]
        public Button[] modeButtons; // 0: BUY, 1: SELL, 2: EQUIP
        public GameObject[] modeHighlights; // 선택된 버튼을 표시할 시각적 UI (배경, 화살표 등)

        private int currentIndex = 0;
        private string currentShopID;

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
            //Buy, Sell, Equip UI 중 하나라도 켜져 있다면, 입력을 무시
            if (IsAnySubPanelActive()) return;

            HandleInput();
        }

        public void OpenShop(string shopID)
        {
            currentShopID = shopID;
            
            // 모든 하위 패널 끄기
            if(buyUI != null) buyUI.gameObject.SetActive(false);
            if(sellUI != null) sellUI.gameObject.SetActive(false);
            if(equipUI != null) equipUI.SetActive(false);

            // 초기 포커스는 BUY
            ChangeHighlight(0);
        }

        private void HandleInput()
        {
            // 취소
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
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
                        equipUI.SetActive(true);
                        // TODO: Equip 모드 초기화 로직
                    }
                    break;
            }
        }

        private void ExitShop()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ChangeState(GameState.Exploration);
            }
        }

        private bool IsAnySubPanelActive()
        {
            bool isBuyActive = buyUI != null && buyUI.gameObject.activeInHierarchy;
            bool isSellActive = sellUI != null && sellUI.gameObject.activeInHierarchy;
            bool isEquipActive = equipUI != null && equipUI.activeInHierarchy;

            return isBuyActive || isSellActive || isEquipActive;
        }
    }
}