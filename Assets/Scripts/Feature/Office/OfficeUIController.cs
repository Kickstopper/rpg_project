using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPGProject.Shared.Input;
using RPGProject.Core;
using RPGProject.Feature.Quests;
using RPGProject.Infrastructure.Audio;

namespace RPGProject.Feature.Office
{
    public class OfficeUIController : MonoBehaviour
    {
        public GameObject dialoguePanel;
        public TextMeshProUGUI dialogueText;
        public float typingSpeed = 0.05f;
        public AudioClip typingSound;
        public GameObject buttonContainer;
        public Button questButton, partnerButton;
        public OfficeQuestUI questUI;
        public OfficePartnerUI partnerUI;
        private bool opened, busy;
        private Coroutine flow;

        void Awake()
        {
            questButton.onClick.AddListener(OnQuestClicked);
            partnerButton.onClick.AddListener(OnPartnerClicked);
        }
        void OnDisable()
        {
            StopAllCoroutines(); flow = null; opened = false; busy = false;
        }
        void Update()
        {
            if (opened && !busy && buttonContainer.activeSelf && GameInput.GetCancelDown()) Run(Exit());
        }
        public void OpenOffice()
        {
            if (opened) return;
            opened = true;
            gameObject.SetActive(true);
            questUI.gameObject.SetActive(false); partnerUI.gameObject.SetActive(false);
            Run(Enter());
        }
        private void Run(IEnumerator routine)
        {
            if (flow != null) StopCoroutine(flow);
            busy = true; buttonContainer.SetActive(false); dialoguePanel.SetActive(true);
            flow = StartCoroutine(routine);
        }
        private IEnumerator Enter()
        {
            var receipts = new List<QuestReceipt>();
            var errors = new List<string>();
            var manager = ManagerRoot.Quest;
            manager.RefreshExternalGoals();
            // No yields inside a claim batch. A disabled window cannot interrupt a single payment.
            foreach (var q in manager.GetReadyToReportQuests())
            {
                string runID = manager.GetRunID(q.QuestID);
                if (manager.TryClaimReward(q.QuestID, runID, out var receipt, out var reason)) receipts.Add(receipt);
                else errors.Add(q.QuestName + ": " + reason);
            }
            if (receipts.Count > 0)
            {
                long total = 0;
                foreach (var r in receipts)
                {
                    total += r.gold;
                    yield return Say($"보고 완료: {r.questName}\n보상 {r.gold:N0} G를 지급했습니다.\n[확인] 계속", true);
                }
                yield return Say($"총 {receipts.Count}건 보고 완료\n합계 {total:N0} G 수령\n[확인] 계속", true);
            }
            if (errors.Count > 0) yield return Say(string.Join("\n", errors) + "\n[확인] 계속", true);
            yield return Say("어서 오게나. 무슨 일로 온 거지?", false);
            ShowMenu(questButton);
        }
        private void ShowMenu(Button focus)
        {
            busy = false; flow = null; buttonContainer.SetActive(true); focus.Select();
        }
        private void OnQuestClicked()
        {
            if (!busy && buttonContainer.activeSelf) Run(OpenQuests());
        }
        private IEnumerator OpenQuests()
        {
            yield return Say("현재 의뢰 목록이다. 목표 달성 후 돌아오면 보상을 지급하지.", false);
            dialoguePanel.SetActive(false); questUI.gameObject.SetActive(true); questUI.Show(this);
            busy = false; flow = null;
        }
        private void OnPartnerClicked()
        {
            if (!busy && buttonContainer.activeSelf) Run(OpenPartners());
        }
        private IEnumerator OpenPartners()
        {
            yield return Say("파트너를 확인하겠나?", false);
            dialoguePanel.SetActive(false); partnerUI.gameObject.SetActive(true); partnerUI.Show(this);
            busy = false; flow = null;
        }
        public void ReturnFromSubPanel(string message, Button focus) { Run(Return(message, focus)); }
        private IEnumerator Return(string message, Button focus)
        {
            yield return Say(message, false); ShowMenu(focus);
        }
        private IEnumerator Exit()
        {
            yield return Say("행운을 비네. 무사히 돌아오게나.", false);
            ManagerRoot.GameState.ChangeState(GameState.Exploration);
            gameObject.SetActive(false);
        }
        private IEnumerator Say(string message, bool requireConfirm)
        {
            dialoguePanel.SetActive(true);
            dialogueText.text = message;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
            int count = dialogueText.textInfo.characterCount;
            float started = Time.unscaledTime;
            for (int i = 1; i <= count; i++)
            {
                dialogueText.maxVisibleCharacters = i;
                if (Time.unscaledTime - started > 0.15f && GameInput.GetConfirmDown()) break;
                if (typingSound != null) ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                yield return new WaitForSecondsRealtime(Mathf.Max(0, typingSpeed));
            }
            dialogueText.maxVisibleCharacters = int.MaxValue;
            yield return new WaitForSecondsRealtime(0.2f);
            if (requireConfirm)
            {
                while (!GameInput.GetConfirmDown() && !GameInput.GetCancelDown()) yield return null;
                yield return null;
            }
        }
    }
}
