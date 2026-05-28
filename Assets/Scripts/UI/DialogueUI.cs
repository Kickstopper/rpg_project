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
            else if (lineType == "REACT")
            {
                // 임시 처리. 전투 재개
                EndDialogue(-1);
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
            else if (type == "REACT")
            {
                // 임시 처리. 전투 재개
                EndDialogue(-1);
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
                    GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer.transform);
                    btnObj.GetComponentInChildren<TextMeshProUGUI>().text = branchData["Text"];

                    string nextTargetID = branchData.ContainsKey("NextID") ? branchData["NextID"] : "END";
                    
                    Button btn = btnObj.GetComponent<Button>();
                    btn.onClick.AddListener(() => OnChoiceSelected(nextTargetID));

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

        // 유저가 타이핑이 끝난 후 클릭했을 때 실행되는 함수
        void AdvanceLine()
        {
            var currentData = currentEventLines[currentLineIndex];
            string nextTargetID = currentData.ContainsKey("NextID") ? currentData["NextID"] : "";

            // 현재 줄의 NextID가 "END"라면 즉시 대화 종료
            if (nextTargetID.ToUpper() == "END")
            {
                EndDialogue();
                return;
            }

            if (nextTargetID == "WAIT_FOR_CHOICE")
            {
                ShowNegotiationChoices(); // 새로 만든 호출 메서드 실행
                return; 
            }

            // 특정 Seq로 점프해야 하는 경우 (일반 대화 중 컷씬 스킵 등)
            if (!string.IsNullOrEmpty(nextTargetID) && nextTargetID.ToUpper() != "END")
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
                // 더 이상 읽을 줄이 남아있지 않다면 종료
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

        public void GenerateChoices(List<string> choiceTexts, Action<int> onChoiceClicked)
        {
            isWaitingForChoice = true;
            choiceContainer.SetActive(true);

            ClearChoiceContainer();

            for (int i = 0; i < choiceTexts.Count; i++)
            {
                GameObject newBtnObj = Instantiate(choiceButtonPrefab, choiceContainer.transform);
                Button btn = newBtnObj.GetComponent<Button>();
                activeChoiceButtons.Add(btn);

                TextMeshProUGUI btnText = newBtnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = choiceTexts[i];

                // 클릭 이벤트 연동
                int index = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => 
                {
                    choiceContainer.SetActive(false);
                    onChoiceClicked?.Invoke(index);
                });
            }

            // 첫 번째 버튼에 포커스
            if (activeChoiceButtons.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null); 
                EventSystem.current.SetSelectedGameObject(activeChoiceButtons[0].gameObject);
            }
        }

        private void ShowNegotiationChoices()
        {
            List<string> texts = new List<string> { "논리적으로 설득", "유혹하기", "뇌물 주기" };
            List<ChoiceTone> targetTones = new List<ChoiceTone> { ChoiceTone.Logical, ChoiceTone.Flirt, ChoiceTone.Bribe };

            GenerateChoices(texts, (selectedIndex) => 
            {
                ChoiceTone selectedTone = targetTones[selectedIndex];
                ProcessNegotiationTurn(selectedTone);
            });
        }

        private void ProcessNegotiationTurn(ChoiceTone selectedTone)
        {
            // 몬스터의 성향과 플레이어의 선택을 넘겨 점수를 계산
            MoodDelta mood = NegotiationCalculator.CalculateMoodChange(selectedTone, currentMonster, new EnvironmentState());
            currentMonster.CurrentAnger += mood.addedAnger;
            currentMonster.CurrentJoy += mood.addedJoy;
            currentMonster.CurrentInterest += mood.addedInterest;
            
            // 결과 판정
            if (currentMonster.CurrentAnger >= 100) 
            {
                // 교섭 결렬 및 적 턴 시작
                JumpToNegotiationReaction("Result_Fail");
            }
            else if (currentMonster.CurrentJoy >= 100 || currentMonster.CurrentInterest >= 100)
            {
                // 아이템 획득 및 적 퇴각
                JumpToNegotiationReaction("Result_Success");
            }
            else 
            {
                // 교섭이 진행 중이라면, 선택한 Tone에 맞는 중간 리액션 대사 출력
                // 대사 출력이 끝나면 CSV에 적힌 NextID(WAIT_FOR_CHOICE)를 타고 다시 루프
                string reactionSituation = $"React_{selectedTone.ToString()}";
                JumpToNegotiationReaction(reactionSituation);
            }
        }

        // 선택 결과에 따라 대사 인덱스를 점프하는 메서드
        private void JumpToNegotiationReaction(string targetSituation)
        {
            int targetIndex = -1;
            for (int i = 0; i < currentEventLines.Count; i++)
            {
                if (currentEventLines[i].ContainsKey("Situation") && 
                    currentEventLines[i]["Situation"] == targetSituation)
                {
                    targetIndex = i;
                    break;
                }
            }
            if (targetIndex != -1)
            {
                currentLineIndex = targetIndex;
                ShowCurrentLine(); // 리액션 대사 출력
            }
            else
            {
                Debug.LogWarning($"[DialogueUI] {targetSituation} 에 해당하는 리액션 스크립트가 없습니다!");
                // 임시 처리. 전투 재개
                EndDialogue(-1);
            }
        }

    }
}