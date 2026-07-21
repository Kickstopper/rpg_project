using UnityEngine;
using UnityEngine.UI;
using Manager;
using TMPro;
using Data;
using DG.Tweening;
using System.Collections;

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

        [Header("Dialogue UI")]
        public GameObject dialoguePanel;
        public Image characterPortrait;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI dialogueText; // 주인의 대사가 출력될 텍스트
        private float typingStartTime;
        public float typingSpeed = 0.05f;
        public AudioClip typingSound;
        
        [Header("Mode Buttons")]
        public GameObject buttonContainer; // BUY, SELL, EQUIP 버튼의 부모
        public Button[] modeButtons; // 0: BUY, 1: SELL, 2: EQUIP
        public GameObject[] modeHighlights; // 선택된 버튼의 하이라이트

        ShopData shopData;

        private BgmID prevBgmID;

        private Coroutine typingCoroutine;
        private bool isTyping = false;
        private System.Action onDialogueComplete; // 대사 출력 후 실행할 콜백 함수
        
        private int currentIndex = 0;
        private string currentShopID;
        private bool wasSubPanelActive = false;
        void Start()
        {
            modeButtons[0].onClick.AddListener(OnBuyClicked);
            modeButtons[1].onClick.AddListener(OnSellClicked);
            modeButtons[2].onClick.AddListener(OnEquipClicked);
        }

        void Update()
        {
            if (ManagerRoot.GameState.CurrentState != GameState.Shop) return;
            if (string.IsNullOrEmpty(currentShopID)) return;

            bool isCurrentlyActive = IsAnySubPanelActive();

            // BuyUI나 SellUI에서 취소 키를 눌러 닫혔다면
            if (wasSubPanelActive && !isCurrentlyActive)
            {
                UpdateTextUI();
                
                // 서브 UI가 닫히면 주인이 다시 말을 걸고 버튼을 띄움
                dialoguePanel.SetActive(true);
                SpeakAndDo(shopData.cancelMessage, () => 
                {
                    buttonContainer.SetActive(true);
                    modeButtons[currentIndex].Select(); // 선택했었던 버튼으로 포커스 복구
                });
            }

            // 다음 프레임 비교를 위해 현재 상태를 저장
            wasSubPanelActive = isCurrentlyActive;

            // 하위 패널이 하나라도 켜져 있다면 상위 입력을 무시
            if (isCurrentlyActive) return;

            // 0.1초가 지난 후에만 메시지 스킵이 되도록 함
            if (isTyping && Time.unscaledTime > typingStartTime + 0.1f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            {
                CompleteTypingImmediately();
            }
            
            // 서브 UI들이 모두 꺼져있고, 메인 메뉴 상태일 때 취소 키 처리
            if (buttonContainer.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown()))
            {
                OnExitClicked();
                return;
            }

            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveHighlight(-1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveHighlight(1);
            }
        }

        // 1. 상점에 진입했을 때
        public void OpenShop(string shopID)
        {
            currentShopID = shopID;
            
            titleText.text = "";
            dialogueText.text = "";
            nameText.text = "";

            shopData = ShopManager.Instance.GetShopData(shopID);
            if (shopData != null)
            {
                if (titleText != null)
                    titleText.text = shopData.displayName;

                nameText.text = shopData.characterName;

                if (background != null)
                {
                    Sprite bg = shopData.BackgroundImage;
                    background.sprite = bg;
                    background.color = bg != null ? Color.white : Color.clear; 
                }
                if (characterPortrait != null)
                {
                    Sprite portrait = shopData.characterImage;
                    characterPortrait.sprite = portrait;
                    characterPortrait.color = portrait != null ? Color.white : Color.clear; 
                }
                if (shopData.bgmID != BgmID.None)
                {
                    prevBgmID = ManagerRoot.Sound.CurrentBgmID;
                    ManagerRoot.Sound.PlayBGM(shopData.bgmID);
                }
                else ManagerRoot.Sound.StopBGM();
            }
            else
            {
                background.color = Color.clear;
                characterPortrait.color = Color.clear;
                ManagerRoot.Sound.StopBGM();
            }

            // 모든 하위 패널 끄기
            if(buyUI != null) buyUI.gameObject.SetActive(false);
            if(sellUI != null) sellUI.gameObject.SetActive(false);
            if(equipUI != null) equipUI.gameObject.SetActive(false);

            UpdateTextUI();
            
            gameObject.SetActive(true);
            buttonContainer.SetActive(false); // 버튼을 일단 숨김

            if (fadeOverlay != null)
            {
                fadeOverlay.gameObject.SetActive(true);
                fadeOverlay.color = Color.black;
                fadeOverlay.DOFade(0, 1f).OnComplete(()=>
                {
                    fadeOverlay.gameObject.SetActive(false);
                    // 인삿말 출력 후 버튼 표시
                    SpeakAndDo(shopData.startMessage, () => 
                    {
                        buttonContainer.SetActive(true);
                        modeButtons[0].Select(); // 첫 번째 버튼에 포커스
                        ChangeHighlight(0); // 초기 포커스 인디케이터 위치는 BUY
                    });
                });
            }
        }

        private void SpeakAndDo(string message, System.Action onComplete)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            
            onDialogueComplete = onComplete;
            typingCoroutine = StartCoroutine(TypeText(message));
        }

        private IEnumerator TypeText(string message)
        {
            isTyping = true;
            typingStartTime = Time.unscaledTime; // 타이핑 시작 시간 기록
            dialogueText.text = message;
            dialogueText.maxVisibleCharacters = 0;

            for (int i = 0; i < message.Length; i++)
            {
                dialogueText.maxVisibleCharacters = i + 1;
                if (typingSound != null)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                }
                yield return new WaitForSeconds(typingSpeed);
            }

            CompleteTypingImmediately();
        }

        private void CompleteTypingImmediately()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            
            dialogueText.maxVisibleCharacters = dialogueText.text.Length;
            isTyping = false;

            // 0.2초 정도 짧게 대기 후 서브UI 열기 실행
            StartCoroutine(WaitAndExecuteCallback());
        }

        private IEnumerator WaitAndExecuteCallback()
        {
            yield return new WaitForSeconds(0.2f);
            
            // 등록된 액션이 있다면 실행하고 비움
            if (onDialogueComplete != null)
            {
                System.Action tempAction = onDialogueComplete;
                onDialogueComplete = null;
                tempAction.Invoke();
            }
        }

        private void UpdateTextUI()
        {
            if (moneyText != null) moneyText.text = $"${ManagerRoot.Inventory.GetMoney()}";
            if (totalPriceText != null) totalPriceText.text = "-";
            if (possessionText != null) possessionText.text = "-";
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

        private void OnBuyClicked()
        {
            ChangeHighlight(0);
            buttonContainer.SetActive(false);
            SpeakAndDo(shopData.buyMessage, () => 
            {
                dialoguePanel.SetActive(false);
                buyUI.gameObject.SetActive(true);
                buyUI.Show(currentShopID); 
            });
        }

        private void OnSellClicked()
        {
            ChangeHighlight(1);
            buttonContainer.SetActive(false);
            SpeakAndDo(shopData.sellMessage, () => 
            {
                dialoguePanel.SetActive(false);
                sellUI.gameObject.SetActive(true);
                sellUI.Show(); 
            });
        }

        private void OnEquipClicked()
        {
            ChangeHighlight(2);
            buttonContainer.SetActive(false);
            SpeakAndDo(shopData.equipMessage, () => 
            {
                dialoguePanel.SetActive(false);
                equipUI.gameObject.SetActive(true);
                equipUI.Show(); 
            });
        }

        // 5. 취소 키로 상점 나가기
        private void OnExitClicked()
        {
            buttonContainer.SetActive(false);
            SpeakAndDo(shopData.endMessage, () => 
            {
                CloseShop();
            });
        }

        private void CloseShop()
        {
            if (ManagerRoot.Sound != null)
            {
                if (prevBgmID != BgmID.None)
                    ManagerRoot.Sound.PlayBGM(prevBgmID);
                else
                    ManagerRoot.Sound.StopBGM();
                
            }

            prevBgmID = BgmID.None;
            currentShopID = null;

            if (ManagerRoot.GameState != null)
            {
                ManagerRoot.GameState.ChangeState(GameState.Exploration);
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