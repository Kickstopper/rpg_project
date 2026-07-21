using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Manager;
using Data;

namespace UI.Office
{
    public class OfficeQuestUI : MonoBehaviour
    {
        [Header("Quest List")]
        public Transform contentPanel;
        public GameObject questSlotPrefab;

        [Header("Info View")]
        public QuestInfoView infoView;
        
        [Header("Popup - Confirm (YES/NO)")]
        public GameObject confirmPopup;
        public TextMeshProUGUI confirmText;
        public Button confirmYesBtn;
        public Button confirmNoBtn;

        [Header("Popup - Alert (OK)")]
        public GameObject alertPopup;
        public TextMeshProUGUI alertText;
        public Button alertOkBtn;

        private OfficeUIController mainUI;
        private List<GameObject> spawnedSlots = new List<GameObject>();

        private int currentSlotIndex = 0;
        private float inputCooldown = 0f;
        
        // 팝업 중복 입력 방지용 쿨타임 변수
        private float popupCooldown = 0f; 
        private bool isPopupOpen = false;

        private System.Action onConfirmYesAction;

        void Start()
        {
            // 기존에 연결된 이벤트나 인스펙터 설정이 꼬여있다면 전부 초기화
            confirmYesBtn.onClick.RemoveAllListeners();
            confirmNoBtn.onClick.RemoveAllListeners();
            alertOkBtn.onClick.RemoveAllListeners();

            // 팝업 버튼 이벤트 스크립트로 강제 연결
            confirmYesBtn.onClick.AddListener(OnConfirmYesClicked);
            confirmNoBtn.onClick.AddListener(ClosePopups);
            alertOkBtn.onClick.AddListener(ClosePopups);
        }

        public void Show(OfficeUIController parentUI)
        {
            mainUI = parentUI;
            ClosePopups(); 
            PopulateQuestList();
        }

        private void PopulateQuestList()
        {
            foreach (var slot in spawnedSlots) Destroy(slot);
            spawnedSlots.Clear();

            List<QuestData> allQuests = ManagerRoot.Quest.GetAllQuests();

            for (int i = 0; i < allQuests.Count; i++)
            {
                QuestData q = allQuests[i];
                GameObject go = Instantiate(questSlotPrefab, contentPanel);
                
                var slotScript = go.GetComponent<QuestSlotUI>(); 
                
                bool isCompleted = ManagerRoot.Quest.IsQuestCompleted(q.QuestID);
                bool isActive = ManagerRoot.Quest.IsQuestActive(q.QuestID);
                
                slotScript.Setup(q, isCompleted, isActive);
                
                Button btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => OnQuestSlotSelected(slotScript, q));

                spawnedSlots.Add(go);
            }

            SelectCurrentSlot();
        }

        private void SelectCurrentSlot()
        {
            if (spawnedSlots.Count > 0 && !isPopupOpen)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(spawnedSlots[currentSlotIndex]);

                // 포커스가 바뀔 때마다 상세 정보 뷰를 즉시 갱신
                UpdateQuestInfoView();
            }
        }

        // 뷰어 데이터 갱신 함수
        private void UpdateQuestInfoView()
        {
            if (infoView != null)
            {
                QuestData currentData = ManagerRoot.Quest.GetAllQuests()[currentSlotIndex];
                bool isCompleted = ManagerRoot.Quest.IsQuestCompleted(currentData.QuestID);
                bool isActive = ManagerRoot.Quest.IsQuestActive(currentData.QuestID);
                
                infoView.UpdateView(currentData, isCompleted, isActive);
            }
        }

        private void OnQuestSlotSelected(QuestSlotUI slot, QuestData data)
        {
            if (slot.IsCompleted || slot.IsActive) return;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            var commander = ManagerRoot.Party.partyData.Find(p => p.isCommander);
            int commanderLevel = (commander != null) ? commander.stats.level : 1;

            int maxAllowedQuests = 1 + (commanderLevel / 15);
            int currentActiveCount = ManagerRoot.Quest.GetActiveQuests().Count;

            if (currentActiveCount >= maxAllowedQuests)
            {
                ShowAlertPopup($"현재 당신의 LV에서는 {maxAllowedQuests}개의 퀘스트만 수주할 수 있습니다.");
            }
            else
            {
                ShowConfirmPopup("이 퀘스트를 진행하시겠습니까?", () => 
                {
                    ManagerRoot.Quest.AcceptQuest(data.QuestID);
                    PopulateQuestList(); 
                });
            }
        }

        // 팝업 제어 로직
        private void ShowConfirmPopup(string message, System.Action onYes)
        {
            isPopupOpen = true;
            popupCooldown = 0.2f;
            onConfirmYesAction = onYes;
            
            confirmText.text = message;
            confirmPopup.SetActive(true);
            
            EventSystem.current.SetSelectedGameObject(null);
            confirmYesBtn.Select();
        }

        private void ShowAlertPopup(string message)
        {
            isPopupOpen = true;
            popupCooldown = 0.2f;
            
            alertText.text = message;
            alertPopup.SetActive(true);
            
            EventSystem.current.SetSelectedGameObject(null);
            alertOkBtn.Select();
        }

        private void OnConfirmYesClicked()
        {
            if (popupCooldown > 0f) return; 

            confirmPopup.SetActive(false);
            alertPopup.SetActive(false);
            isPopupOpen = false;
            
            onConfirmYesAction?.Invoke();
        }

        private void ClosePopups()
        {
            if (popupCooldown > 0f) return;

            confirmPopup.SetActive(false);
            alertPopup.SetActive(false);
            isPopupOpen = false;
            
            SelectCurrentSlot();
        }

        // 입력 제어 로직
        void Update()
        {
            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;
            
            // 팝업 쿨타임 감소
            if (popupCooldown > 0) popupCooldown -= Time.deltaTime; 

            if (isPopupOpen)
            {
                // 팝업이 열려있을 때 취소 키(ESC)를 누르면 자연스럽게 팝업이 닫힘
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown())
                {
                    ClosePopups();
                }
                // 마우스 클릭 등에 의한 포커스 유실 방지 락
                else if (EventSystem.current.currentSelectedGameObject == null)
                {
                    if (confirmPopup.activeSelf) confirmYesBtn.Select();
                    else if (alertPopup.activeSelf) alertOkBtn.Select();
                }
                return;
            }

            if (spawnedSlots.Count > 0 && inputCooldown <= 0f)
            {
                bool moved = false;

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    currentSlotIndex--;
                    if (currentSlotIndex < 0) currentSlotIndex = spawnedSlots.Count - 1; 
                    moved = true;
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    currentSlotIndex++;
                    if (currentSlotIndex >= spawnedSlots.Count) currentSlotIndex = 0; 
                    moved = true;
                }

                if (moved)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    SelectCurrentSlot();
                    inputCooldown = 0.05f; 
                }
            }

            // 팝업이 열려있지 않을 때 취소 키를 누르면 메인 메뉴로 복귀
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown())
            {
                gameObject.SetActive(false);
                mainUI.ReturnFromSubPanel("잘 확인했나?", mainUI.questButton);
            }
        }
    }
}