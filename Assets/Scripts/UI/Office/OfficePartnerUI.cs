using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; 
using Manager;
using Data.Database;
using Data;
using UI.Common;

namespace UI.Office
{
    public class OfficePartnerUI : MonoBehaviour
    {
        [Header("Partner Info Panel")]
        public GameObject partnerInfoPanel; 
        public Image partnerPortraitImage; 
        public TextMeshProUGUI partnerInfoText; 
        public TextMeshProUGUI contractInfoText;

        [Header("Partner List Panel")]
        public Transform contentPanel;
        public GameObject partnerSlotPrefab;
        
        [Header("Warning Popup UI")]
        public GameObject warningPopupPanel;
        public TextMeshProUGUI txtWarningMessage;
        public Button btnWarningOk;

        private OfficeUIController mainUI;
        private List<PartnerSlotUI> spawnedSlots = new List<PartnerSlotUI>();
        private string currentPartnerID = "";

        // 포커스 및 상태 제어용 변수
        private int currentSlotIndex = 0;
        private float inputCooldown = 0f;
        private bool isPopupActive = false; 
        
        private GameObject lastSelectedSlotObject = null; // 현재 포커스된 슬롯을 추적하기 위한 변수

        public void Show(OfficeUIController parentUI)
        {
            mainUI = parentUI;
            
            if (warningPopupPanel != null) 
                warningPopupPanel.SetActive(false);
            
            if (partnerInfoPanel != null)
                partnerInfoPanel.SetActive(true);

            isPopupActive = false;
            lastSelectedSlotObject = null;
            
            PopulatePartnerList();
        }

        private void PopulatePartnerList()
        {
            foreach (var slot in spawnedSlots) Destroy(slot.gameObject);
            spawnedSlots.Clear();

            var party = ManagerRoot.Party.partyData;
            var currentPartner = party.Find(m => !m.isCommander);
            currentPartnerID = currentPartner != null ? currentPartner.characterId : "";

            List<CharacterDatabase.CharacterEntry> allCharacters = ManagerRoot.Database.charDB.entries;

            foreach (var charEntry in allCharacters)
            {
                if (charEntry.isCommander || charEntry.isMonster) continue;

                GameObject go = Instantiate(partnerSlotPrefab, contentPanel);
                PartnerSlotUI slotUI = go.GetComponent<PartnerSlotUI>();
                
                bool isCurrentlyInParty = (charEntry.id == currentPartnerID);
                slotUI.Setup(charEntry, isCurrentlyInParty);

                Button btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => OnPartnerSelected(charEntry.id, slotUI));

                spawnedSlots.Add(slotUI);
            }

            if (spawnedSlots.Count > 0)
            {
                currentSlotIndex = 0;
                SelectCurrentSlot();
            }
        }

        private void SelectCurrentSlot()
        {
            if (spawnedSlots.Count > 0 && !isPopupActive)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(spawnedSlots[currentSlotIndex].gameObject);
            }
        }

        // 포커스된 파트너의 정보를 패널에 갱신하는 메서드
        private void UpdatePartnerInfoPanel(string targetPartnerID)
        {
            var entry = ManagerRoot.Database.charDB.GetEntry(targetPartnerID);
            if (entry == null) return;

            // 초상화 갱신
            if (partnerPortraitImage != null)
            {
                if (entry.portraitImage != null)
                {
                    partnerPortraitImage.sprite = entry.portraitImage;
                    partnerPortraitImage.color = Color.white;
                }
                else
                {
                    partnerPortraitImage.color = Color.clear;
                }
            }

            // 레벨 및 스탯 판별 (로스터 검사)
            int level = 1;
            Race race = entry.race;
            Gender gender = entry.gender;
            string alignment = entry.align.ToString().ToUpper();
            int currentExp = 0;
            int nextExp = Helper.BattleCalculator.GetMaxExpForLevel(level, race, gender);

            if (ManagerRoot.Party.unlockedRoster.ContainsKey(targetPartnerID))
            {
                var rosterData = ManagerRoot.Party.unlockedRoster[targetPartnerID];
                level = rosterData.stats.level;
                alignment = rosterData.align.ToString().ToUpper();
                currentExp = rosterData.currentExp;
                nextExp = rosterData.GetRequiredExpForNextLevel();
            }

            // 기본 정보 텍스트 갱신
            if (partnerInfoText != null)
            {
                partnerInfoText.text = 
                    $"NAME  : {entry.name}\n" +
                    $"LEVEL : {level}\n" +
                    $"ALIGN : {alignment}\n" +
                    $"EXP   : {currentExp} / {nextExp}";
            }

            // 계약 조건(급여/착수금) 갱신
            if (contractInfoText != null)
            {
                int monthlySalary = level * ManagerRoot.Finance.SalaryPerPartner;
                int advanceFee = ManagerRoot.Finance.GetHiringAdvancePayment(level);
                int dailyWage = Mathf.RoundToInt((float)monthlySalary / 30f);

                contractInfoText.text = 
                    $"[ CONTRACT CONDITIONS ]\n" +
                    $"ADVANCE FEE (10 DAYS) : {advanceFee:N0} G\n" +
                    $"DAILY WAGE PRO-RATA   : {dailyWage:N0} G / DAY\n" +
                    $"FULL MONTHLY SALARY   : {monthlySalary:N0} G";
            }
        }

        private void OnPartnerSelected(string newPartnerID, PartnerSlotUI clickedSlot)
        {
            if (isPopupActive || newPartnerID == currentPartnerID) return;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            var newEntry = ManagerRoot.Database.charDB.GetEntry(newPartnerID);
            if (newEntry == null) return;

            int partnerLevel = 1; 
            if (ManagerRoot.Party.unlockedRoster.ContainsKey(newPartnerID))
            {
                partnerLevel = ManagerRoot.Party.unlockedRoster[newPartnerID].stats.level;
            }

            int requiredAdvanceFee = ManagerRoot.Finance.GetHiringAdvancePayment(partnerLevel);
            int currentMoney = ManagerRoot.Finance.CurrentMoney;

            if (currentMoney < requiredAdvanceFee)
            {
                ShowWarningPopup(requiredAdvanceFee, currentMoney);
                return; 
            }

            ManagerRoot.Finance.SubMoney(requiredAdvanceFee);

            if (!string.IsNullOrEmpty(currentPartnerID))
            {
                ManagerRoot.Party.RemoveMember(currentPartnerID);
                var oldSlot = spawnedSlots.Find(s => s.characterID == currentPartnerID);
                if (oldSlot != null) oldSlot.Deselect();
            }

            ManagerRoot.Party.AddMember(newEntry, false); 
            
            var newlyAddedPartner = ManagerRoot.Party.partyData.Find(m => m.characterId == newPartnerID);
            if (newlyAddedPartner != null) newlyAddedPartner.workedDays = 0; 

            clickedSlot.Select();
            currentPartnerID = newPartnerID;

            Debug.Log($"파트너 교체 완료: {newEntry.name}(Lv.{partnerLevel}) 합류! (착수금 {requiredAdvanceFee}G 지불)");
        }

        private void ShowWarningPopup(int requiredFee, int currentMoney)
        {
            isPopupActive = true;
            warningPopupPanel.SetActive(true);
            
            if (txtWarningMessage != null)
            {
                txtWarningMessage.text = 
                    $"[SYSTEM ERROR: CONTRACT FAILED]\n" +
                    $"--------------------------------\n" +
                    $"자금이 부족합니다.\n" +
                    $"새로운 파트너와 계약을 체결하려면\n" +
                    $"최소 10일 치의 착수금이 필요합니다.\n\n" +
                    $"필요 자금 : {requiredFee:N0} G\n" +
                    $"현재 잔액 : {currentMoney:N0} G";
            }

            EventSystem.current.SetSelectedGameObject(null);
            if (btnWarningOk != null)
            {
                EventSystem.current.SetSelectedGameObject(btnWarningOk.gameObject);
                btnWarningOk.onClick.RemoveAllListeners();
                btnWarningOk.onClick.AddListener(CloseWarningPopup);
            }
        }

        private void CloseWarningPopup()
        {
            if (!isPopupActive) return;

            isPopupActive = false;
            warningPopupPanel.SetActive(false);
            SelectCurrentSlot();
        }

        void Update()
        {
            // EventSystem의 현재 선택된 오브젝트를 감시하여 포커스가 바뀌면 정보를 갱신
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != lastSelectedSlotObject)
            {
                lastSelectedSlotObject = currentSelected;
                
                // 선택된 오브젝트가 우리가 생성한 슬롯 중 하나인지 확인
                var focusedSlot = spawnedSlots.Find(s => s.gameObject == currentSelected);
                if (focusedSlot != null)
                {
                    UpdatePartnerInfoPanel(focusedSlot.characterID);
                }
            }

            if (isPopupActive)
            {
                if (GameInput.GetConfirmDown() || GameInput.GetCancelDown())
                {
                    CloseWarningPopup();
                }
                return;
            }

            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

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

            if (Common.GameInput.GetCancelDown())
            {
                gameObject.SetActive(false);
                mainUI.ReturnFromSubPanel("둘이서 힘을 합쳐 쌓인 일들을 한시바삐 처리해 주게나.", mainUI.partnerButton);
            }
        }
    }
}