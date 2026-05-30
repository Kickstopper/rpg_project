using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Manager;
using Data.Database;
using UnityEngine.EventSystems;
using Data;
using Helper;
using Controller;

namespace UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Choice Components")]
        public GameObject choiceContainer;       // 선택지들이 들어갈 컨테이너
        public GameObject choiceButtonPrefab;    // 선택지 프리팹

        [Header("Settings")]
        public CharacterDatabase characterDB;     // inspector에서 설정
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
        private MonsterController currentMonster;
        private EnvironmentState currentEnvState;

        private int currentLineIndex = 0;
        private bool isDialogueActive = false;
        public bool IsDialogueActive => isDialogueActive;

        // 타이핑 상태 관리 변수
        private bool isTyping = false;
        private Coroutine typingCoroutine;
        private bool isWaitingForChoice = false;

        private List<Button> activeChoiceButtons = new List<Button>();
        private int currentChoiceIndex = 0;

        private float inputCooldown = 0f;

        List<string> negotiationChoices = new List<string> { "논리적으로 설득", "유혹하기", "뇌물 주기" };
        List<ChoiceTone> targetTones = new List<ChoiceTone> { ChoiceTone.Logical, ChoiceTone.Flirt, ChoiceTone.Bribe };

        public event Action<int> OnDialogueFinished;
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

        public void Initialize(string eventID, Action<int> onComplete = null)
        {
            currentEventLines = DialogueManager.Instance.GetEventData(eventID);
            if (currentEventLines == null || currentEventLines.Count == 0) return;
            
            OnDialogueFinished = onComplete; 
            
            StartDialogueFlow();
        }

        public void StartNegotiation(List<Dictionary<string, string>> lines, MonsterController monster, Action<int> onNegotiationEnded)
        {
            currentEventLines = lines;
            currentMonster = monster;

            // 교섭 시작 시 몬스터 감정 수치 초기화
            currentMonster.CurrentAnger = 0;
            currentMonster.CurrentJoy = 0;
            currentMonster.CurrentInterest = 0;

            OnDialogueFinished = onNegotiationEnded;
            // 첫 시작 라인을 찾아서 인덱스를 설정
            currentLineIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].ContainsKey("Situation") && lines[i]["Situation"] == "Intro")
                {
                    currentLineIndex = i;
                    break;
                }
            }

            StartDialogueFlow();
        }

        private void StartDialogueFlow()
        {
            inputCooldown = 0.2f;
            currentLineIndex = 0;
            isDialogueActive = true;
            uiCanvas.SetActive(true);
            ShowCurrentLine();
        }

        void EndDialogue(int result = -1)
        {
            isDialogueActive = false;
            uiCanvas.SetActive(false);
            choiceContainer.SetActive(false);
            
            // BattleManager가 알아서 다음 턴을 이어감
            OnDialogueFinished?.Invoke(result);
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
            
            string name = lineData.ContainsKey("Name") ? lineData["Name"] : "";
            float pitch = 1f;
            string characterId = lineData.ContainsKey("CharacterID") ? lineData["CharacterID"] : "";
            if (!string.IsNullOrEmpty(characterId))
            {
                var entry = characterDB.GetEntry(characterId);
                if (entry != null)
                {
                    pitch = GetMedianPitch(entry.gender);
                    SetImage(entry);
                }

                // CSV의 값을 우선하여 표시
                if (string.IsNullOrEmpty(name))
                {
                    var chrData = PartyManager.Instance.GetCharacterByID(characterId);
                    if (chrData != null)
                        name = chrData.name;
                    else if (entry != null)
                        name = entry.name;
                }
            }
            else
            {
                portraitImageUI.enabled = false;
                standingImageUI.enabled = false;
            }

            nameText.text = name;

            // 텍스트 설정 및 타이핑 효과 시작
            string fullText = lineData.ContainsKey("Text") ? lineData["Text"] : "";
            contentText.text = fullText;
            contentText.maxVisibleCharacters = 0; // 글자 표시 개수를 0으로 초기화
            
            // 기존 타이핑이 있다면 중지하고 새로 시작
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText(type, pitch));
        }

        // 타이핑 효과 코루틴. Type을 매개변수로 받아서, 타이핑이 끝나면 어떻게 할지 결정
        IEnumerator TypeText(string lineType, float medianPitch = 1f)
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
                    audioSource.pitch = UnityEngine.Random.Range(medianPitch - 0.1f, medianPitch + 0.1f);
                    audioSource.PlayOneShot(typingSound);
                }
                counter++;
                yield return new WaitForSeconds(typingSpeed);
            }

            contentText.maxVisibleCharacters = totalChars;
            isTyping = false;

            // 타이핑이 끝난 후, 현재 타입이 CHOICE라면 선택지를 띄움
            if (lineType == "CHOICE")
            {
                GenerateChoices();
            }
        }

        float GetMedianPitch(Gender gender)
        {
            if (gender == Gender.Male) return 0.8f;
            if (gender == Gender.Female) return 2.5f;

            return 1f;
        }

        void SetImage(CharacterDatabase.CharacterEntry entry)
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
            }
            else
            {
                standingImageUI.enabled = false; 
            }
        }

        void Update()
        {
            if (!isDialogueActive) return;

            if (inputCooldown > 0)
            {
                inputCooldown -= Time.deltaTime;
                return;
            }
            
            if (isWaitingForChoice)
            {
                HandleChoiceNavigation();
                return;
            }

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

        // 수동 네비게이션 및 포커스 유지 로직
        void HandleChoiceNavigation()
        {
            if (activeChoiceButtons == null || activeChoiceButtons.Count == 0) return;

            bool changed = false;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                currentChoiceIndex = (currentChoiceIndex - 1 + activeChoiceButtons.Count) % activeChoiceButtons.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                currentChoiceIndex = (currentChoiceIndex + 1) % activeChoiceButtons.Count;
                changed = true;
            }

            // 포커스 유실 감지 (마우스로 화면 빈 공간을 클릭했을 경우)
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            bool isFocusLost = currentSelected == null || !activeChoiceButtons.Exists(b => b.gameObject == currentSelected);

            // 인덱스가 변했거나 포커스를 잃었다면 즉시 복구
            if (changed || isFocusLost)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(activeChoiceButtons[currentChoiceIndex].gameObject);
            }

            // 결정
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (activeChoiceButtons[currentChoiceIndex].interactable)
                {
                    isWaitingForChoice = false;
                    SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                    //activeChoiceButtons[currentChoiceIndex].onClick.Invoke();
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
            if (type == "CHOICE")
            {
                GenerateChoices();
            }
        }

        // 선택지 로직
        void GenerateChoices()
        {
            isWaitingForChoice = true;
            choiceContainer.SetActive(true);

            ClearChoiceContainer();

            int lookAheadIndex = currentLineIndex + 1;

            while (lookAheadIndex < currentEventLines.Count)
            {
                var branchData = currentEventLines[lookAheadIndex];
                string branchType = branchData.ContainsKey("Type") ? branchData["Type"].ToUpper() : "";

                if (branchType == "BRANCH")
                {
                    // Condition 검사
                    string condition = branchData.ContainsKey("Condition") ? branchData["Condition"] : "";
                    if (!CheckCondition(condition))
                    {
                        // 조건을 만족하지 못하면 버튼을 만들지 않고 다음 줄로 넘어감
                        lookAheadIndex++;
                        continue; 
                    }

                    GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer.transform);
                    btnObj.GetComponentInChildren<TextMeshProUGUI>().text = branchData["Text"];

                    string nextTargetID = branchData.ContainsKey("NextID") ? branchData["NextID"] : "END";
                    
                    // Action 데이터 추출
                    string actionStr = branchData.ContainsKey("Action") ? branchData["Action"] : "";
                    
                    Button btn = btnObj.GetComponent<Button>();
                    
                    // 버튼 클릭 시 액션 실행 후 대사 넘기기 연동
                    btn.onClick.AddListener(() => {
                        ExecuteAction(actionStr);          // 액션 먼저 실행
                        OnChoiceSelected(nextTargetID);    // 그 다음 목표 ID로 점프
                    });

                    activeChoiceButtons.Add(btn);
                }
                else
                {
                    break;
                }
                lookAheadIndex++;
            }

            // 첫 번째 버튼에 포커스
            if (activeChoiceButtons.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null); 
                EventSystem.current.SetSelectedGameObject(activeChoiceButtons[0].gameObject);
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

        // Seq 값으로 줄 번호를 찾아주는 함수
        int FindIndexBySeq(string seqValue)
        {
            for (int i = 0; i < currentEventLines.Count; i++)
            {
                if (currentEventLines[i].ContainsKey("Seq") && currentEventLines[i]["Seq"] == seqValue)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool CheckCondition(string conditionData)
        {
            // 조건이 비어있으면 검사 없이 무조건 통과 (선택지 표시)
            if (string.IsNullOrEmpty(conditionData)) return true;

            string[] parts = conditionData.Trim().Split(':');
            if (parts.Length == 0) return true;

            string command = parts[0].ToUpper();

            switch (command)
            {
                case "HASITEM":
                    // parts[0]="HasItem", parts[1]="Gold_100"
                    if (parts.Length >= 2)
                    {
                        string itemID = parts[1];
                        Debug.Log($"[Condition] 아이템 소지 여부 확인: {itemID}");
                        return InventoryManager.Instance.HasItem(itemID);
                    }
                    break;
                    
                case "FLAG":
                    // 특정 퀘스트나 이벤트 플래그 확인 (예: "Flag:passed_guard")
                    if (parts.Length >= 2)
                    {
                        string flagName = parts[1];
                        return FlagManager.Instance.CheckFlag(flagName);
                    }
                    break;
            }

            return false; // 조건을 만족하지 못하면 false 반환 -> 버튼 생성 안 됨
        }

        private void ExecuteAction(string actionData)
        {
            if (string.IsNullOrEmpty(actionData)) return;

            string[] actions = actionData.Split(';'); 

            foreach (string act in actions)
            {
                string[] parts = act.Trim().Split(':'); 
                if (parts.Length == 0) continue;

                string command = parts[0].ToUpper();

                switch (command)
                {
                    case "TONE":
                        // 교섭 선택 시 (예: "TONE:Logical")
                        if (currentMonster != null && Enum.TryParse<ChoiceTone>(parts[1], true, out ChoiceTone tone))
                        {
                            MoodDelta delta = NegotiationCalculator.CalculateMoodChange(tone, currentMonster, new EnvironmentState());
                            
                            currentMonster.CurrentAnger += delta.addedAnger;
                            currentMonster.CurrentJoy += delta.addedJoy;
                            currentMonster.CurrentInterest += delta.addedInterest;
                            
                            Debug.Log($"[교섭 액션] {tone} 선택 -> 분노:{currentMonster.CurrentAnger}, 기쁨:{currentMonster.CurrentJoy}, 흥미:{currentMonster.CurrentInterest}");
                        }
                        break;

                    case "ADD_MOOD":
                        // TODO: 강제 감정치 조절 (예: "ADD_MOOD:-50", "ADD_JOY:30", "ADD_ANGER:50")
                        break;

                    case "REMOVE":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            // TODO: 돈과 인벤토리 아이템 차감 로직 연동
                            InventoryManager.Instance.RemoveItem(itemID, 1);
                            Debug.Log($"[Action] 아이템 차감: {itemID}");
                        }
                        break;

                    case "BATTLE":
                        // TODO: 전투 재개
                        Debug.Log("[Action] 전투 개시 트리거 발생!");
                        break;
                }
            }
        }

        // 유저가 타이핑이 끝난 후 클릭했을 때 실행되는 함수
        void AdvanceLine()
        {
            var currentData = currentEventLines[currentLineIndex];
            string nextTargetID = currentData.ContainsKey("NextID") ? currentData["NextID"].Trim() : "";

            // NextID가 "CHECK_MOOD:목적지" 형태일 경우 점수 판정
            if (nextTargetID.StartsWith("CHECK_MOOD"))
            {
                if (currentMonster.CurrentJoy >= 100 || currentMonster.CurrentInterest >= 100) 
                {
                    nextTargetID = "SUCCESS"; // 교섭 성공
                }
                else if (currentMonster.CurrentAnger >= 100) 
                {
                    nextTargetID = "FAIL";    // 교섭 결렬
                }
                else
                {
                    // 0~99점 사이라면 지정한 루프 시작점으로 돌아감 (CHECK_MOOD:LOOP_START -> LOOP_START)
                    string[] parts = nextTargetID.Split(':');
                    nextTargetID = parts.Length > 1 ? parts[1].Trim() : "LOOP_START";
                }
            }

            // 현재 줄의 NextID가 "END"라면 즉시 대화 종료
            if (nextTargetID.ToUpper() == "END")
            {
                EndDialogue();
                return;
            }

            // 특정 Seq로 점프
            if (!string.IsNullOrEmpty(nextTargetID))
            {
                int targetIndex = FindIndexBySeq(nextTargetID);
                if (targetIndex != -1)
                {
                    currentLineIndex = targetIndex;
                    ShowCurrentLine();
                    return;
                }
                else
                {
                    Debug.LogWarning($"NextID '{nextTargetID}'를 찾을 수 없습니다.");
                    EndDialogue();
                    return;
                }
            }

            // 아무 지시가 없으면 바로 다음 줄로 이동
            currentLineIndex++;
            if (currentLineIndex < currentEventLines.Count)
            {
                ShowCurrentLine();
            }
            else
            {
                EndDialogue(); 
            }
        }

        private void ClearChoiceContainer()
        {
            // 리스트와 인덱스 초기화
            activeChoiceButtons.Clear();
            currentChoiceIndex = 0;

            // 기존 버튼 모두 제거
            foreach (Transform child in choiceContainer.transform)
                Destroy(child.gameObject);
        }

    }
}