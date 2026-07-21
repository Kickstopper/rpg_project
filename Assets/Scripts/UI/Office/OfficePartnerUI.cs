using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Manager;
using Data.Database;
using Data;

namespace UI.Office
{
    public class OfficePartnerUI : MonoBehaviour
    {
        public Transform contentPanel;
        public GameObject partnerSlotPrefab;
        
        private OfficeUIController mainUI;
        private List<PartnerSlotUI> spawnedSlots = new List<PartnerSlotUI>();
        private string currentPartnerID = "";

        // 포커스 제어용 변수
        private int currentSlotIndex = 0;
        private float inputCooldown = 0f;

        public void Show(OfficeUIController parentUI)
        {
            mainUI = parentUI;
            PopulatePartnerList();
        }

        private void PopulatePartnerList()
        {
            foreach (var slot in spawnedSlots) Destroy(slot.gameObject);
            spawnedSlots.Clear();

            // 현재 파티에 소속된 파트너(isCommander == false) 찾기
            var party = ManagerRoot.Party.partyData;
            var currentPartner = party.Find(m => !m.isCommander);
            currentPartnerID = currentPartner != null ? currentPartner.characterId : "";

            // CharacterDatabase에서 모든 엔트리 가져오기
            List<CharacterDatabase.CharacterEntry> allCharacters = ManagerRoot.Database.charDB.entries;

            foreach (var charEntry in allCharacters)
            {
                // 주인공(Commander)은 목록에서 제외
                if (charEntry.isCommander) continue;

                GameObject go = Instantiate(partnerSlotPrefab, contentPanel);
                PartnerSlotUI slotUI = go.GetComponent<PartnerSlotUI>();
                
                bool isCurrentlyInParty = (charEntry.id == currentPartnerID);
                slotUI.Setup(charEntry, isCurrentlyInParty);

                // 클릭/선택 이벤트 연결
                Button btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => OnPartnerSelected(charEntry.id, slotUI));

                spawnedSlots.Add(slotUI);
            }

            // 첫 번째 파트너 슬롯 선택
            if (spawnedSlots.Count > 0)
            {
                currentSlotIndex = 0;
                SelectCurrentSlot();
            }
        }

        // EventSystem 포커스를 갱신하는 헬퍼 함수
        private void SelectCurrentSlot()
        {
            if (spawnedSlots.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(spawnedSlots[currentSlotIndex].gameObject);
            }
        }

        private void OnPartnerSelected(string newPartnerID, PartnerSlotUI clickedSlot)
        {
            // 이미 파티에 있는 파트너라면 무시
            if (newPartnerID == currentPartnerID) return;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            // 기존 파트너를 파티에서 제거하고 슬롯 색상 원상복구
            if (!string.IsNullOrEmpty(currentPartnerID))
            {
                ManagerRoot.Party.RemoveMember(currentPartnerID);
                var oldSlot = spawnedSlots.Find(s => s.characterID == currentPartnerID);
                if (oldSlot != null) oldSlot.Deselect();
            }

            // 새 파트너를 파티에 영입하고 슬롯 색상 변경
            var newEntry = ManagerRoot.Database.charDB.GetEntry(newPartnerID);
            if (newEntry != null)
            {
                ManagerRoot.Party.AddMember(newEntry, false); 
            }

            clickedSlot.Select();
            currentPartnerID = newPartnerID;

            Debug.Log($"파트너 교체 완료: {newEntry.name} 합류!");
        }

        void Update()
        {
            // 쿨타임 감소 로직
            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

            // 방향키 입력 처리
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

            // 취소 키 입력 시 메인 메뉴로 복귀하며 고유 대사 출력
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown())
            {
                gameObject.SetActive(false);
                mainUI.ReturnFromSubPanel("둘이서 힘을 합쳐 쌓인 일들을 한시바삐 처리해 주게나.", mainUI.partnerButton);
            }
        }
    }
}