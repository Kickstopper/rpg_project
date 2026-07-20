using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Manager;
using Data;

namespace UI.Office
{
    public class OfficeQuestUI : MonoBehaviour
    {
        public Transform contentPanel;
        public GameObject questSlotPrefab;
        
        private OfficeUIController mainUI;
        private List<GameObject> spawnedSlots = new List<GameObject>();

        // 포커스 제어용 변수
        private int currentSlotIndex = 0;
        private float inputCooldown = 0f;

        public void Show(OfficeUIController parentUI)
        {
            mainUI = parentUI;
            PopulateQuestList();
        }

        private void PopulateQuestList()
        {
            // 기존 슬롯 삭제
            foreach (var slot in spawnedSlots) Destroy(slot);
            spawnedSlots.Clear();

            List<QuestData> allQuests = ManagerRoot.Quest.GetAllQuests();

            for (int i = 0; i < allQuests.Count; i++)
            {
                QuestData q = allQuests[i];
                GameObject go = Instantiate(questSlotPrefab, contentPanel);
                
                var slotScript = go.GetComponent<QuestSlotUI>(); 
                
                // 달성 여부 체크
                bool isCompleted = ManagerRoot.Quest.IsQuestCompleted(q.QuestID);
                
                slotScript.Setup(q, isCompleted);
                spawnedSlots.Add(go);
            }

            // 첫 번째 퀘스트 슬롯 선택 함수 호출
            currentSlotIndex = 0;
            SelectCurrentSlot();
        }

        // EventSystem 포커스를 갱신하는 헬퍼 함수
        private void SelectCurrentSlot()
        {
            if (spawnedSlots.Count > 0)
            {
                // 포커스 유실 방지 및 확실한 OnSelect 호출을 위해 먼저 null로 세팅
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(spawnedSlots[currentSlotIndex]);
            }
        }

        void Update()
        {
            // 쿨타임 감소
            if (inputCooldown > 0) inputCooldown -= Time.deltaTime;

            // 방향키 입력 처리 (리스트가 비어있지 않고 쿨타임이 끝났을 때만)
            if (spawnedSlots.Count > 0 && inputCooldown <= 0f)
            {
                bool moved = false;

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    currentSlotIndex--;
                    if (currentSlotIndex < 0) currentSlotIndex = spawnedSlots.Count - 1; // 맨 위에서 위로 누르면 맨 아래로 순환
                    moved = true;
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    currentSlotIndex++;
                    if (currentSlotIndex >= spawnedSlots.Count) currentSlotIndex = 0; // 맨 아래에서 아래로 누르면 맨 위로 순환
                    moved = true;
                }

                // 이동이 발생했다면 포커스를 갱신하고 효과음을 재생
                if (moved)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                    SelectCurrentSlot();
                    inputCooldown = 0.05f; // 연속 입력 방지용 짧은 쿨타임
                }
            }

            // 취소 키 입력 시 메인 메뉴로 복귀하며 고유 대사 출력
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Common.GameInput.GetCancelDown())
            {
                gameObject.SetActive(false);
                mainUI.ReturnFromSubPanel("잘 확인했나?", mainUI.questButton);
            }
        }
    }
}