using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Manager;
using UnityEngine.EventSystems;
using Data;

namespace Controller
{
    public class CombatResultUI : MonoBehaviour
    {
        [Header("Reward Info")]
        public TextMeshProUGUI goldText;
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

        public void Show(CombatController.BattleReward reward, List<PlayerController> partyMembers, 
                        Dictionary<PlayerController, (int oldLv, int oldExp, int oldMaxExp)> preBattleStates, 
                        System.Action onCloseCallback)
        {
            this.gameObject.SetActive(true);
            this.onClosed = onCloseCallback;
            this.isClosing = false; // 초기화

            // 1. 텍스트 설정
            goldText.text = $"{reward.totalGold} G";
            totalExpText.text = $"{reward.totalExp} EXP";

            // 2. 아이템 슬롯 생성
            foreach(Transform child in itemContainer) Destroy(child.gameObject);
            
            Dictionary<string, int> itemCounts = new Dictionary<string, int>();
            foreach(var itemId in reward.dropItems)
            {
                if(itemCounts.ContainsKey(itemId)) itemCounts[itemId]++;
                else itemCounts[itemId] = 1;
            }

            foreach(var kvp in itemCounts)
            {
                var itemData = DatabaseManager.Instance.GetItem(kvp.Key);
                if(itemData != null)
                {
                    GameObject go = Instantiate(itemSlotPrefab, itemContainer);
                    var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                    if(texts.Length > 0) texts[0].text = $"{itemData.dataName}"; // 이름
                    if(texts.Length > 1) texts[1].text = $"x{kvp.Value}";        // 개수
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

            // 4. 버튼 이벤트 연결
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);

            // UI가 켜질 때 'Continue' 버튼을 강제로 선택 상태로 만듦
            // 이렇게 해야 마우스를 쓰지 않아도 스페이스바가 먹힘
            StartCoroutine(SelectButtonDelayed());
        }

        // 버튼 선택 딜레이 (활성화 직후에는 선택이 안 될 수도 있어서 한 프레임 대기)
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

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            gameObject.SetActive(false);
            onClosed?.Invoke();
        }
    }
}
