using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Manager;
using Data.Database;
using UnityEngine.EventSystems;

namespace UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Choice Components")]
        public GameObject choiceContainer;       // 선택지들이 들어갈 컨테이너
        public GameObject choiceButtonPrefab;    // 선택지 프리팹

        [Header("Settings")]
        public CharacterDatabase portraitDB;     // inspector에서 설정
        public float typingSpeed = 0.05f;        // 글자당 시간 (작을수록 빠름)
        public AudioClip typingSound;            // 타이핑 효과음
        private AudioSource audioSource;

        [Header("UI Components")]
        public GameObject uiCanvas;
        public Image portraitImageUI;
        public Image standingImageUI;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI contentText;

        private List<Dictionary<string, string>> currentEventLines;
        private int currentLineIndex = 0;
        private bool isDialogueActive = false;

        // 타이핑 상태 관리 변수
        private bool isTyping = false;
        private Coroutine typingCoroutine;
        private bool isWaitingForChoice = false;

        public event Action OnDialogueFinished;
        public event Action<string> OnChoiceMade;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        void Start()
        {
            uiCanvas.SetActive(false);
        }

        public void Initialize(string eventID, Action onComplete = null)
        {
            currentEventLines = DialogueManager.Instance.GetEventData(eventID);
            if (currentEventLines == null || currentEventLines.Count == 0) return;
            
            OnDialogueFinished = onComplete; 
            
            StartDialogueFlow();
        }

        // CSV를 거치지 않고, 전투 시스템이 실시간으로 조립한 대화 스크립트로 초기화
        public void InitializeDynamic(List<Dictionary<string, string>> dynamicLines, Action onComplete = null, Action<string> onChoice = null)
        {
            currentEventLines = dynamicLines;
            if (currentEventLines == null || currentEventLines.Count == 0) return;

            OnDialogueFinished = onComplete;
            OnChoiceMade = onChoice; // [신규 추가]
            
            StartDialogueFlow();
        }

        private void StartDialogueFlow()
        {
            currentLineIndex = 0;
            isDialogueActive = true;
            uiCanvas.SetActive(true);
            ShowCurrentLine();
        }

        void EndDialogue()
        {
            isDialogueActive = false;
            uiCanvas.SetActive(false);
            choiceContainer.SetActive(false);
            
            // BattleManager가 알아서 다음 턴을 이어감
            OnDialogueFinished?.Invoke();
        }

        void ShowCurrentLine()
        {
            if (currentLineIndex >= currentEventLines.Count)
            {
                EndDialogue();
                return;
            }

            var lineData = currentEventLines[currentLineIndex];

            string type = lineData.ContainsKey("Type") ? lineData["Type"].ToUpper() : "TALK";

            // BRANCH 라인은 패스 (CHOICE가 알아서 처리함)
            if (type == "BRANCH")
            {
                AdvanceLine();
                return;
            }
            
            // 텍스트 및 이름 설정
            nameText.text = lineData.ContainsKey("Speaker") ? lineData["Speaker"] : "";
            contentText.text = lineData.ContainsKey("Text") ? lineData["Text"] : "";

            string portraitID = lineData.ContainsKey("Portrait") ? lineData["Portrait"] : "";
        
            if (!string.IsNullOrEmpty(portraitID))
            {
                var entry = portraitDB.GetEntry(portraitID);
                
                if (entry != null)
                {
                    if (entry.portraitImage != null)
                    {
                        portraitImageUI.sprite = entry.portraitImage;
                        portraitImageUI.SetNativeSize();
                        portraitImageUI.enabled = true;
                    }
                    else
                    {
                        portraitImageUI.enabled = false;
                    }

                    if (entry.standingImage != null)
                    {
                        standingImageUI.sprite = entry.standingImage;
                        standingImageUI.SetNativeSize();
                        standingImageUI.enabled = true;
                        // standingImageUI.SetNativeSize(); // 필요시 원본 비율 유지
                    }
                    else
                    {
                        standingImageUI.enabled = false; 
                    }
                }
            }
            else
            {
                portraitImageUI.enabled = false;
                standingImageUI.enabled = false;
            }

            // 텍스트 설정 및 타이핑 효과 시작
            string fullText = lineData.ContainsKey("Text") ? lineData["Text"] : "";
            contentText.text = fullText;
            contentText.maxVisibleCharacters = 0; // 글자 표시 개수를 0으로 초기화

            // 기존 타이핑이 있다면 중지하고 새로 시작
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText(type));
        }

        // 타이핑 효과 코루틴. Type을 매개변수로 받아서, 타이핑이 끝나면 어떻게 할지 결정
        IEnumerator TypeText(string lineType)
        {
            isTyping = true;
            contentText.ForceMeshUpdate(); 

            int totalChars = contentText.textInfo.characterCount;
            int counter = 0;

            while (counter < totalChars)
            {
                contentText.maxVisibleCharacters = counter + 1;
                if (typingSound != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(typingSound);
                }
                counter++;
                yield return new WaitForSeconds(typingSpeed);
            }

            contentText.maxVisibleCharacters = totalChars;
            isTyping = false;

            // 타이핑이 끝난 후, 현재 타입이 CHOICE라면 선택지를 띄움
            if (lineType == "CHOICE")
                GenerateChoices();
        }

        void Update()
        {
            if (!isDialogueActive) return;

            if (isWaitingForChoice) return;

            if (Input.GetButtonDown("Submit") || Input.GetMouseButtonDown(0))
            {
                if (isTyping)
                {
                    // 타이핑 중이라면 즉시 완성
                    CompleteTypingImmediately();
                }
                else
                {
                    // 타이핑이 끝났다면 다음 대사로
                    AdvanceLine();
                }
            }
        }

        // 즉시 모든 글자 표시
        void CompleteTypingImmediately()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            contentText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;

            // 스킵했을 때도 CHOICE라면 선택지를 띄워야 함
            var lineData = currentEventLines[currentLineIndex];
            string type = lineData.ContainsKey("Type") ? lineData["Type"].ToUpper() : "TALK";
            
            if (type == "CHOICE") GenerateChoices();
        }

        // 선택지 로직
        void GenerateChoices()
        {
            isWaitingForChoice = true;
            choiceContainer.SetActive(true);

            // 기존 버튼들 모두 제거
            foreach (Transform child in choiceContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // 현재 줄 다음부터 BRANCH가 나올 때마다 ChoiceButtonPrefab (선택지) 생성
            int lookAheadIndex = currentLineIndex + 1;
            GameObject firstButton = null;

            while (lookAheadIndex < currentEventLines.Count)
            {
                var branchData = currentEventLines[lookAheadIndex];
                string branchType = branchData.ContainsKey("Type") ? branchData["Type"].ToUpper() : "";

                if (branchType == "BRANCH")
                {
                    // 버튼 생성 로직
                    GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer.transform);
                    btnObj.GetComponentInChildren<TextMeshProUGUI>().text = branchData["Text"];

                    string nextTargetID = branchData.ContainsKey("NextID") ? branchData["NextID"] : "END";
                    
                    // 버튼 클릭 이벤트 연결
                    Button btn = btnObj.GetComponent<Button>();
                    btn.onClick.AddListener(() => OnChoiceSelected(nextTargetID));

                    if (firstButton == null) firstButton = btnObj;
                }
                else
                {
                    // BRANCH가 끝나고 다른 타입(TALK 등)이 나오면 탐색 종료
                    break;
                }
                lookAheadIndex++;
            }

            // 키보드/패드 조작을 위해 첫 번째 버튼에 포커스 주기
            if (firstButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null); // 기존 포커스 해제
                EventSystem.current.SetSelectedGameObject(firstButton); // 새 포커스 지정
            }
        }

        // 버튼을 클릭했을 때 호출됨
        void OnChoiceSelected(string nextTargetID)
        {
            // 선택지 UI 정리
            isWaitingForChoice = false;
            choiceContainer.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null); // 포커스 해제

            // 교섭용 콜백이 연결되어 있다면, 값을 쏴주고 즉시 종료
            if (OnChoiceMade != null)
            {
                OnChoiceMade.Invoke(nextTargetID);
                EndDialogue();
                return;
            }

            // 점프 혹은 대화 종료
            if (nextTargetID.ToUpper() == "END" || string.IsNullOrEmpty(nextTargetID))
            {
                EndDialogue();
            }
            else
            {
                // 대상 Seq 번호를 찾아 이동
                int targetIndex = FindIndexBySeq(nextTargetID);
                
                if (targetIndex != -1)
                {
                    currentLineIndex = targetIndex;
                    ShowCurrentLine();
                }
                else
                {
                    Debug.LogWarning($"NextID '{nextTargetID}'를 찾을 수 없습니다.");
                    EndDialogue();
                }
            }
        }

        // Seq 값으로 줄 번호(Index)를 찾아주는 유틸리티 함수
        int FindIndexBySeq(string seqValue)
        {
            for (int i = 0; i < currentEventLines.Count; i++)
            {
                if (currentEventLines[i].ContainsKey("Seq") && currentEventLines[i]["Seq"] == seqValue)
                {
                    return i;
                }
            }
            return -1; // 못 찾음
        }

        void AdvanceLine()
        {
            currentLineIndex++;
            ShowCurrentLine();
        }

    }
}