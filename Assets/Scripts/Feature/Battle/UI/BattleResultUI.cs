using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using RPGProject.Feature.Quests;
using RPGProject.Core;
using RPGProject.Shared.Input;
using RPGProject.Infrastructure.Audio;

namespace RPGProject.Feature.Battle
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
        private bool showingNotice, noticeConfirmed;
        private float noticeAcceptAt, resultAcceptAt;
        
        // 실행 중인 팝업 코루틴을 추적하여 안전하게 끄기 위한 변수
        private Coroutine questPopupCoroutine; 

        public void Show(BattleManager.BattleReward reward, 
                         List<PlayerController> partyMembers, 
                         Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates, 
                         List<QuestData> completedQuests,
                         System.Action onCloseCallback)
        {
            if (questPopupCoroutine != null) StopCoroutine(questPopupCoroutine);
            if (popupUI != null) popupUI.Close();
            this.gameObject.SetActive(true);
            this.onClosed = onCloseCallback;
            this.isClosing = false;
            showingNotice = false; noticeConfirmed = false;
            resultAcceptAt = Time.unscaledTime + 0.2f;

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
            showingNotice = true;
            foreach (var q in quests)
            {
                noticeConfirmed = false;
                noticeAcceptAt = Time.unscaledTime + 0.2f;
                popupUI.Open(q);
                yield return null;
                while (!noticeConfirmed) yield return null;
            }
            popupUI.Close(); showingNotice = false; questPopupCoroutine = null;
            resultAcceptAt = Time.unscaledTime + 0.2f;
        }

        IEnumerator SelectButtonDelayed()
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        void Update()
        {
            if (!isClosing)
            {
                if (GameInput.GetConfirmDown() || GameInput.GetCancelDown())
                {
                    OnContinueClicked();
                }
            }
        }

        void OnContinueClicked()
        {
            if (isClosing || Time.unscaledTime < resultAcceptAt) return;
            if (showingNotice)
            {
                if (Time.unscaledTime >= noticeAcceptAt) noticeConfirmed = true;
                return;
            }
            isClosing = true;

            if (questPopupCoroutine != null) StopCoroutine(questPopupCoroutine);
            if (popupUI != null) popupUI.Close();

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            gameObject.SetActive(false);
            var completed = onClosed; onClosed = null; completed?.Invoke();
        }
    }
}