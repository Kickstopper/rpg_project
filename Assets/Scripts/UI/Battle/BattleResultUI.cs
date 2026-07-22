using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine.EventSystems;
using Data;

namespace UI.Battle
{
    public class BattleResultUI : MonoBehaviour
    {
        [Header("Quest Complete Notice")]
        public QuestNoticePopupUI popupUI;

        [Header("Reward Info")]
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI totalExpText;
        
        [Header("Item List")]
        public Transform itemContainer;
        public GameObject itemSlotPrefab;

        [Header("Party Members")]
        public Transform memberContainer;
        public GameObject memberSlotPrefab;

        [Header("Controls")]
        public Button continueButton;

        private System.Action onClosed;
        private bool isClosing = false;
        
        // 실행 중인 팝업 코루틴을 추적하여 안전하게 끄기 위한 변수
        private Coroutine questPopupCoroutine; 

        public void Show(BattleManager.BattleReward reward, 
                         List<PlayerController> partyMembers, 
                         Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates, 
                         List<QuestData> completedQuests,
                         System.Action onCloseCallback)
        {
            this.gameObject.SetActive(true);
            this.onClosed = onCloseCallback;
            this.isClosing = false; 

            // 텍스트 설정
            moneyText.text = $"{reward.totalMoney} G";
            totalExpText.text = $"{reward.totalExp} EXP";

            // 아이템 슬롯 생성
            foreach(Transform child in itemContainer) Destroy(child.gameObject);
            
            Dictionary<string, int> itemCounts = new Dictionary<string, int>();
            foreach(var itemId in reward.dropItems)
            {
                if(itemCounts.ContainsKey(itemId)) itemCounts[itemId]++;
                else itemCounts[itemId] = 1;
            }

            foreach(var kvp in itemCounts)
            {
                var itemData = ManagerRoot.Database.GetItem(kvp.Key);
                if(itemData != null)
                {
                    GameObject go = Instantiate(itemSlotPrefab, itemContainer);
                    var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                    if(texts.Length > 0) texts[0].text = $"{itemData.dataName}"; 
                    if(texts.Length > 1) texts[1].text = $"x{kvp.Value}";        
                }
            }

            // 파티원 슬롯 생성 및 애니메이션 시작
            foreach(Transform child in memberContainer) Destroy(child.gameObject);

            foreach(var pc in partyMembers)
            {
                if (pc == null) continue;

                if (preBattleStates.TryGetValue(pc, out var oldState))
                {
                    GameObject go = Instantiate(memberSlotPrefab, memberContainer);
                    var slot = go.GetComponent<ResultMemberSlot>();
                    slot.Setup(pc, reward.expPerMember, oldState.oldLv, oldState.oldExp, oldState.oldMaxExp);
                }
            }

            // 여러 개의 퀘스트가 있을 경우 코루틴을 통해 순차적으로 표시
            if (completedQuests != null && completedQuests.Count > 0)
            {
                if (popupUI != null)
                {
                    questPopupCoroutine = StartCoroutine(ShowQuestPopupsSequentially(completedQuests));
                }
            }

            // 버튼 이벤트 연결
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);

            StartCoroutine(SelectButtonDelayed());
        }

        // 퀘스트 팝업 순차 표시 코루틴
        private IEnumerator ShowQuestPopupsSequentially(List<QuestData> quests)
        {
            // 원하는 간격으로 시간을 조절할 수 있습니다 (현재 2.5초)
            WaitForSeconds waitTime = new WaitForSeconds(2.5f);

            foreach (var q in quests)
            {
                popupUI.Open(q);
                // ManagerRoot.Sound.PlaySFX(SfxID.UI_Notification); // 갱신될 때마다 효과음 출력
                yield return waitTime;
            }

            // 모든 퀘스트를 보여준 후 팝업을 닫고 싶다면 아래 주석을 해제하세요.
            // popupUI.Close();
        }

        System.Collections.IEnumerator SelectButtonDelayed()
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        void Update()
        {
            if (!isClosing)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnContinueClicked();
                }
            }
        }

        void OnContinueClicked()
        {
            if (isClosing) return;
            isClosing = true;

            if (questPopupCoroutine != null) StopCoroutine(questPopupCoroutine);
            if (popupUI != null) popupUI.Close();

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            gameObject.SetActive(false);
            onClosed?.Invoke();
        }
    }
}