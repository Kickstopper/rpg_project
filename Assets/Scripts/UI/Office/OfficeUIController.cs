using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using UI.Common;
using Data;
using System.Collections.Generic;

namespace UI.Office
{
    public class OfficeUIController : MonoBehaviour
    {
        [Header("Dialogue UI")]
        public GameObject dialoguePanel;
        public TextMeshProUGUI dialogueText;
        public float typingSpeed = 0.05f;
        public AudioClip typingSound;

        [Header("Menu UI")]
        public GameObject buttonContainer;
        public Button questButton;
        public Button partnerButton;
        
        [Header("Sub Panels")]
        public OfficeQuestUI questUI;
        public OfficePartnerUI partnerUI;

        private Coroutine typingCoroutine;
        private bool isTyping = false;
        private float typingStartTime;
        private System.Action onDialogueComplete;

        void Start()
        {
            questButton.onClick.AddListener(OnQuestClicked);
            partnerButton.onClick.AddListener(OnPartnerClicked);
        }

        void Update()
        {
            // 타이핑 스킵 로직 (쿨타임 적용)
            if (isTyping && Time.unscaledTime > typingStartTime + 0.1f && 
               (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            {
                CompleteTypingImmediately();
            }

            // 메인 메뉴 상태에서 취소 키 입력 시 퇴장
            if (buttonContainer.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || GameInput.GetCancelDown()))
            {
                OnExitClicked();
            }
        }

        public void OpenOffice()
        {
            gameObject.SetActive(true);
            buttonContainer.SetActive(false);
            questUI.gameObject.SetActive(false);
            partnerUI.gameObject.SetActive(false);

            // 진입 시 퀘스트 달성 여부를 검사하는 코루틴 시작
            StartCoroutine(CheckAndProcessRewardsRoutine());
        }

        // 보상 지급 및 진입 연출 코루틴
        private IEnumerator CheckAndProcessRewardsRoutine()
        {
            // 보상받을 수 있는 퀘스트 목록 가져오기
            List<QuestData> readyQuests = ManagerRoot.Quest.GetReadyToReportQuests();

            if (readyQuests.Count > 0)
            {
                // 달성한 퀘스트가 있을 경우의 특수 인삿말
                bool isSpeaking = true;
                SpeakAndDo("의뢰를 완수했군. 여기 약속된 보수네.", () => isSpeaking = false);
                
                // 오피서의 대사가 다 타이핑될 때까지 대기
                yield return new WaitUntil(() => !isSpeaking);

                foreach (var q in readyQuests)
                {
                    // 보상 지급
                    ManagerRoot.Finance.AddMoney(q.Reward);
                    
                    // 퀘스트 완료 처리 (이 안에서 QuestComplete 플래그도 켜짐)
                    ManagerRoot.Quest.CompleteQuest(q.QuestID); 
                    
                    // 용도 폐기된 Ready 플래그 제거 (선택 사항)
                    ManagerRoot.Flag.SetFlag($"QuestReady_{q.QuestID}", false);

                    // TODO: 유저에게 보상 획득을 알리는 팝업 UI 표시
                    // 예: ManagerRoot.UI.ShowAlertPopup($"{q.QuestName} 완료! {q.Reward}G 획득!");
                    // yield return new WaitUntil(() => !ManagerRoot.UI.IsPopupOpen);
                    
                    Debug.Log($"[Office] {q.QuestName} 보상 지급 완료: {q.Reward}G");
                    
                    // 팝업 없이 텍스트로만 처리한다면 약간의 딜레이
                    yield return YieldCache.WaitForSeconds(0.5f); 
                }

                // 보상 지급이 끝나면 자연스럽게 메인 메뉴 표시
                SpeakAndDo("다른 볼일이 남았나?", () => 
                {
                    buttonContainer.SetActive(true);
                    questButton.Select();
                });
            }
            else
            {
                // 달성한 퀘스트가 없을 경우 기존 인삿말
                SpeakAndDo("어서 오게나. 무슨 일로 온 거지?", () => 
                {
                    buttonContainer.SetActive(true);
                    questButton.Select();
                });
            }
        }

        private void OnQuestClicked()
        {
            buttonContainer.SetActive(false);
            SpeakAndDo("현재 가능한 일거리 목록이다.", () => 
            {
                dialoguePanel.SetActive(false);
                questUI.gameObject.SetActive(true);
                questUI.Show(this); // this를 넘겨주어 서브패널이 부모를 알게 함
            });
        }

        private void OnPartnerClicked()
        {
            buttonContainer.SetActive(false);
            SpeakAndDo("파트너 렌탈? 지금의 파트너에 불만이 있는 건가?", () => 
            {
                dialoguePanel.SetActive(false);
                partnerUI.gameObject.SetActive(true);
                partnerUI.Show(this);
            });
        }

        private void OnExitClicked()
        {
            buttonContainer.SetActive(false);
            SpeakAndDo("행운을 비네. 무사히 돌아오게나.", () => 
            {
                gameObject.SetActive(false);
                ManagerRoot.GameState.ChangeState(GameState.Exploration); // 던전 복귀
            });
        }

        // 서브 패널에서 취소 키를 눌렀을 때 호출되는 복귀 함수
        public void ReturnFromSubPanel(string returnMessage, Button buttonToFocus)
        {
            dialoguePanel.SetActive(true);
            SpeakAndDo(returnMessage, () => 
            {
                buttonContainer.SetActive(true);
                buttonToFocus.Select();
            });
        }

        // --- 타이핑 애니메이션 핵심 로직 ---
        private void SpeakAndDo(string message, System.Action onComplete)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            onDialogueComplete = onComplete;
            typingCoroutine = StartCoroutine(TypeText(message));
        }

        private IEnumerator TypeText(string message)
        {
            isTyping = true;
            typingStartTime = Time.unscaledTime;
            dialogueText.text = message;
            dialogueText.maxVisibleCharacters = 0;

            for (int i = 0; i < message.Length; i++)
            {
                dialogueText.maxVisibleCharacters = i + 1;
                if (typingSound != null) ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                yield return YieldCache.WaitForSeconds(typingSpeed);
            }
            CompleteTypingImmediately();
        }

        private void CompleteTypingImmediately()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            dialogueText.maxVisibleCharacters = dialogueText.text.Length;
            isTyping = false;
            StartCoroutine(WaitAndExecuteCallback());
        }

        private IEnumerator WaitAndExecuteCallback()
        {
            yield return YieldCache.WaitForSeconds(0.5f);
            if (onDialogueComplete != null)
            {
                System.Action tempAction = onDialogueComplete;
                onDialogueComplete = null;
                tempAction.Invoke();
            }
        }
    }
}