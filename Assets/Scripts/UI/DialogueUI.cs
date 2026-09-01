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
using UI.Battle;
using System.Text.RegularExpressions;
namespace UI
{
    public enum NegotiationResult { PLAYER_TURN, MONSTER_TURN, BATTLE_END }
    public class DialogueUI : MonoBehaviour
    {
        [Header("Choice Components")]
        public GameObject choiceContainer;       // 선택지들이 들어갈 컨테이너
        public GameObject choiceButtonPrefab;    // 선택지 프리팹

        [Header("Speech Bubble Effects")]
        public Image speechBubbleUI;         // 화면 중앙 등에 띄울 말풍선 UI 컴포넌트
        public Transform[] additionalShakeTargets;
        public Sprite bubbleGentle;
        public Sprite bubbleThreat;
        public Sprite bubbleAccept;
        public Sprite bubbleRefuse;

        [Header("Settings")]
        public float typingSpeed = 0.05f;        // 글자당 시간 (작을수록 빠름)
        public float imageFadeSpeed = 0.3f;
        public AudioClip typingSound;            // 타이핑 효과음
        private AudioSource audioSource;

        [Header("UI Components")]
        public GameObject uiCanvas;
        public Image backgroundImageUI;
        public GameObject portraitPanel;
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
        private Coroutine imageFadeCoroutine;
        private string lastCharacterId = ""; // 이전 화자의 ID를 저장

        private List<Button> activeChoiceButtons = new List<Button>();
        private int currentChoiceIndex = 0;

        private float inputCooldown = 0f;
        private bool isProcessingChoice = false;
        private event Action<int> onDialogueFinished;
        private event Action<string> onChoiceMade;
        private Func<string, int, bool> onResourceDemanded; // 교섭 중 몬스터의 요구 발생
        private NegotiationResult negotiationResult = NegotiationResult.PLAYER_TURN; // 반환될 교섭 결과

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
            currentEventLines = ManagerRoot.Dialogue.GetEventData(eventID);
            if (currentEventLines == null || currentEventLines.Count == 0) return;
            
            onDialogueFinished = onComplete; 
            
            StartDialogueFlow();
        }

        public void StartNegotiation(
            List<Dictionary<string, string>> lines, MonsterController monster, 
            Action<int> onNegotiationEnded, Func<string, int, bool> onResourceDemandCallback = null)
        {
            negotiationResult = NegotiationResult.PLAYER_TURN;
            currentEventLines = lines;
            currentMonster = monster;

            onDialogueFinished = onNegotiationEnded;
            onResourceDemanded = onResourceDemandCallback;
    
            // 첫 시작 라인을 찾아서 인덱스를 설정
            currentLineIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].ContainsKey("Situation") && lines[i]["Situation"] == "Intro")
                {
                    // TODO: 여기서 몬스터의 현재 누적된 감정(Anger, Interest 등)을 체크하여
                    // 대화를 원천 거부하는 Intro 라인으로 분기
                    currentLineIndex = i;
                    break;
                }
            }

            StartDialogueFlow();
        }

        private void StartDialogueFlow()
        {
            lastCharacterId = "";
            inputCooldown = 0.05f;
            currentLineIndex = 0;
            isProcessingChoice = false;
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
            onDialogueFinished?.Invoke((int)negotiationResult);
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

            // JOIN 라인 처리
            if (type == "JOIN")
            {
                string targetCharId = lineData.ContainsKey("CharacterID") ? lineData["CharacterID"] : "";
                ExecuteJoinCharacter(targetCharId);

                // 출력할 Text가 아예 없다면, 화면 갱신 없이 시스템 로직만 처리하고 바로 다음 줄로
                string textContent = lineData.ContainsKey("Text") ? lineData["Text"] : "";
                if (string.IsNullOrEmpty(textContent))
                {
                    AdvanceLine();
                    return;
                }
            }
            // LEAVE 라인 처리
            if (type == "LEAVE")
            {
                string targetCharId = lineData.ContainsKey("CharacterID") ? lineData["CharacterID"] : "";
                ExecuteLeaveCharacter(targetCharId);

                // 출력할 텍스트가 없다면 바로 다음 줄로 넘깁니다. (숨은 이탈 처리)
                string textContent = lineData.ContainsKey("Text") ? lineData["Text"] : "";
                if (string.IsNullOrEmpty(textContent))
                {
                    AdvanceLine();
                    return;
                }
            }

            // 배경 이미지 처리 로직
            if (lineData.ContainsKey("BackgroundID"))
            {
                string bgID = lineData["BackgroundID"].Trim();

                // BG를 꺼야 하는 경우 (예약어 "NONE" 또는 "CLEAR")
                if (bgID.ToUpper() == "NONE" || bgID.ToUpper() == "CLEAR")
                {
                    backgroundImageUI.enabled = false;
                    backgroundImageUI.sprite = null;
                }
                // 새로운 BG ID가 입력된 경우 변경
                else if (ManagerRoot.Database != null)
                {
                    var bgDB = ManagerRoot.Database.bgDB;
                    var bgEntry = bgDB != null ? bgDB.GetEntry(bgID) : null;
                    if (bgEntry != null && bgEntry.bgImage != null)
                    {
                        backgroundImageUI.sprite = bgEntry.bgImage;
                        backgroundImageUI.enabled = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[DialogueUI] BackgroundID '{bgID}'를 찾을 수 없습니다.");
                    }
                }
                // 빈칸("")인 경우 아무 작업도 하지 않음. 이전 배경 유지
            }
            
            string name = lineData.ContainsKey("Name") ? lineData["Name"] : "";
            float pitch = 1f;
            string characterId = lineData.ContainsKey("CharacterID") ? lineData["CharacterID"] : "";

            // 이미지를 세팅할 임시 변수들
            Sprite targetPortrait = null;
            Sprite targetStanding = null;

            if (!string.IsNullOrEmpty(characterId))
            {
                var chrDB = ManagerRoot.Database.charDB;
                var npcDB = ManagerRoot.Database.npcDB;
                var monDB = ManagerRoot.Database.monsterDB;
                
                // 1순위: NPC DB 검색
                var npcEntry = npcDB != null ? npcDB.GetEntry(characterId) : null;
                if (npcEntry != null)
                {
                    pitch = GetMedianPitch(npcEntry.gender);
                    targetPortrait = npcEntry.portraitImage;
                    targetStanding = npcEntry.standingImage;
                    if (string.IsNullOrEmpty(name)) name = npcEntry.name;
                }
                else
                {
                    // 2순위: Character DB 검색
                    var charEntry = chrDB != null ? chrDB.GetEntry(characterId) : null;
                    if (charEntry != null)
                    {
                        pitch = GetMedianPitch(charEntry.gender);
                        targetPortrait = charEntry.portraitImage;
                        targetStanding = charEntry.standingImage;
                        
                        if (string.IsNullOrEmpty(name))
                        {
                            var pData = ManagerRoot.Party.GetCharacterByID(characterId);
                            name = pData != null ? pData.name : charEntry.name;
                        }
                    }
                    else
                    {
                        // 3순위: Monster DB 검색 (교섭 시 몬스터 초상화 띄우기 위함)
                        var monEntry = monDB != null ? monDB.GetEntry(characterId) : null;
                        if (monEntry != null)
                        {
                            pitch = GetMedianPitch(monEntry.gender); // MonsterDB에 Gender가 있다면 사용
                            targetPortrait = monEntry.portrait;
                            targetStanding = monEntry.image[0];
                            if (string.IsNullOrEmpty(name)) name = monEntry.name;
                        }
                    }
                }
            }

            // 최종적으로 찾은 이미지를 UI에 적용
            SetImage(targetPortrait, targetStanding, characterId);

            nameText.text = name;

            // 텍스트 설정 및 타이핑 효과 시작
            string fullText = lineData.ContainsKey("Text") ? lineData["Text"] : "";

            // 정규식을 이용해 [ID] 또는 [ID|조사포맷] 패턴을 처리
            // 패턴 설명: \[ 는 여는 대괄호, ([^\]]+) 는 닫는 대괄호가 아닌 문자들의 연속을 그룹화, \] 는 닫는 대괄호
            fullText = Regex.Replace(fullText, @"\[([^\]]+)\]", match =>
            {
                string innerContent = match.Groups[1].Value; 
                
                // '|' 기호를 기준으로 분리 (예: "chr_01|이/가" -> parts[0]="chr_01", parts[1]="이/가")
                string[] parts = innerContent.Split('|');
                
                string charId = parts[0].Trim();
                string resolvedName = GetCharacterNameFromDB(charId); 
                
                // DB에서 캐릭터 이름을 찾은 경우
                if (!string.IsNullOrEmpty(resolvedName))
                {
                    // '|' 뒤에 조사 포맷이 존재한다면 KoreanParticleHelper를 통해 조사 추가
                    if (parts.Length > 1)
                    {
                        string particleFormat = parts[1].Trim();
                        return resolvedName.AttachParticle(particleFormat); //
                    }
                    
                    // 조사가 없다면 이름만 반환
                    return resolvedName;
                }
                
                // DB에서 이름을 찾지 못했으면 오류 방지를 위해 원본 문자열(예: "[system_var]") 그대로 유지
                return match.Value;
            });

            contentText.text = fullText;
            contentText.maxVisibleCharacters = 0; // 글자 표시 개수를 0으로 초기화
            
            // 기존 타이핑이 있다면 중지하고 새로 시작
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText(type, pitch));
        }

        // 특정 ID의 캐릭터를 파티에 영입
        private void ExecuteJoinCharacter(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;

            CharacterDatabase.CharacterEntry entry = null;
            bool isMonster = false; // 몬스터 여부를 판별

            var chrDB = ManagerRoot.Database.charDB;
            var monDB = ManagerRoot.Database.monsterDB;
            // Character Database에서 먼저 검색
            if (chrDB != null && chrDB.GetEntry(charId) != null)
            {
                entry = chrDB.GetEntry(charId);
                isMonster = false;
            }
            // 없다면 Monster Database에서 검색
            else if (monDB != null && monDB.GetEntry(charId) != null)
            {
                var monsterEntry = monDB.GetEntry(charId);
                entry = monsterEntry.ToCharacterEntry();
                isMonster = true;
            }

            // DB에 유효한 ID가 존재할 경우 파티 추가 로직 실행
            if (entry != null)
            {
                ManagerRoot.Party.AddMember(entry, isMonster);
            }
            else
            {
                Debug.LogError($"[DialogueUI - JOIN] ID '{charId}'를 CharacterDB나 MonsterDB에서 찾을 수 없습니다. 오타를 확인해 주세요.");
            }
        }

        // 특정 ID의 멤버를 파티에서 제외시킴
        private void ExecuteLeaveCharacter(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;

            if (ManagerRoot.Party == null || ManagerRoot.Party.partyData == null)
            {
                Debug.LogError("[DialogueUI - LEAVE] PartyManager 인스턴스 또는 partyData 리스트를 찾을 수 없습니다.");
                return;
            }

            ManagerRoot.Party.RemoveMember(charId);
        }

        // 타이핑 효과 코루틴. Type을 매개변수로 받아서, 타이핑이 끝나면 어떻게 할지 결정
        IEnumerator TypeText(string lineType, float medianPitch = 1f)
        {
            isTyping = true;

            // 이미지 페이드 인 코루틴이 실행 중이라면, 완료될 때까지 대기
            if (imageFadeCoroutine != null)
            {
                yield return new WaitUntil(() => imageFadeCoroutine == null);
            }

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
                yield return YieldCache.WaitForSeconds(typingSpeed);
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

        void SetImage(Sprite portrait, Sprite standing, string charId)
        {
            // 이전 대사와 화자가 같고, 누군가(ID) 지정되어 있다면 연출을 생략
            if (charId == lastCharacterId && !string.IsNullOrEmpty(charId))
            {
                // 연출은 생략하더라도 스킵 등으로 인해 이미지가 안 켜진 상태일 수 있으므로 확실히 켜줌
                if (portrait != null)
                {
                    portraitImageUI.sprite = portrait;
                    //portraitImageUI.SetNativeSize();
                    portraitImageUI.color = Color.white;
                    portraitPanel.SetActive(true);
                }
                else
                {
                    portraitPanel.SetActive(false);
                }

                if (standing != null)
                {
                    standingImageUI.sprite = standing;
                    standingImageUI.SetNativeSize();
                    standingImageUI.enabled = true;
                    standingImageUI.color = Color.white;
                }
                return;
            }

            // 화자가 변경되었으므로 ID를 갱신
            lastCharacterId = charId;

            // 기존에 실행 중인 이미지 연출이 있다면 중지
            if (imageFadeCoroutine != null)
            {
                StopCoroutine(imageFadeCoroutine);
                imageFadeCoroutine = null;
            }

            // 새로운 화자의 코루틴 연출 시작
            imageFadeCoroutine = StartCoroutine(ImageFadeRoutine(portrait, standing));
        }

        IEnumerator ImageFadeRoutine(Sprite portrait, Sprite standing)
        {
            // 초상화는 스탠딩이 뜰 때까지 일단 숨김
            portraitPanel.SetActive(false);
            if (portrait != null)
            {
                portraitImageUI.sprite = portrait;
                //portraitImageUI.SetNativeSize();
                portraitImageUI.color = Color.white; 
            }

            // 스탠딩 이미지 서서히 나타나는 애니메이션
            if (standing != null)
            {
                standingImageUI.sprite = standing;
                standingImageUI.SetNativeSize();
                
                // 시작: 검은색 반투명
                Color startColor = new Color(0f, 0f, 0f, 0.9f);
                // 끝: 원래 색
                Color endColor = new Color(1f, 1f, 1f, 1f); 
                
                standingImageUI.color = startColor;

                float elapsed = 0f;
                while (elapsed < imageFadeSpeed)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / imageFadeSpeed;
                    
                    // 색상과 투명도를 보간
                    standingImageUI.color = Color.Lerp(startColor, endColor, t);
                    yield return null;
                }
                
                standingImageUI.color = endColor; 
            }
            else
            {
                standingImageUI.enabled = false;
            }

            // 스탠딩 이미지가 완전히 나타난 후(또는 스탠딩이 없을 때), 초상화 표시
            if (portrait != null)
            {
                portraitPanel.SetActive(true);
            }
            else portraitPanel.SetActive(false);

            imageFadeCoroutine = null; // 연출 완료
        }

        void Update()
        {
            if (!isDialogueActive || isProcessingChoice) return;

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

            if (Common.GameInput.GetConfirmDown())
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
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                currentChoiceIndex = (currentChoiceIndex - 1 + activeChoiceButtons.Count) % activeChoiceButtons.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
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
            if (UI.Common.GameInput.GetSelectDown())
            {
                if (activeChoiceButtons[currentChoiceIndex].interactable)
                {
                    //isWaitingForChoice = false;
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                    activeChoiceButtons[currentChoiceIndex].onClick.Invoke();
                }
            }
        }

        // 즉시 모든 글자 표시
        void CompleteTypingImmediately()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            contentText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;

            // 대사를 스킵하면 이미지 연출도 즉시 완료 상태로 강제 전환
            if (imageFadeCoroutine != null)
            {
                StopCoroutine(imageFadeCoroutine);
                imageFadeCoroutine = null;
                
                if (standingImageUI.sprite != null && standingImageUI.enabled)
                {
                    standingImageUI.color = new Color(1f, 1f, 1f, 1f); // 스탠딩 즉시 불투명/원래색
                }
                
                if (portraitImageUI.sprite != null && !portraitPanel.activeInHierarchy)
                {
                    portraitPanel.SetActive(true); // 초상화 즉시 표시
                }
            }

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
                        if (isProcessingChoice) return;
                        isProcessingChoice = true;
                        inputCooldown = 0.2f;

                        ChoiceTone? foundTone = null;
                        if (!string.IsNullOrEmpty(actionStr))
                        {
                            string[] actions = actionStr.Split(';'); 
                            foreach (string act in actions)
                            {
                                string[] parts = act.Trim().Split(':'); 
                                if (parts.Length >= 2 && parts[0].ToUpper() == "TONE")
                                {
                                    if (Enum.TryParse<ChoiceTone>(parts[1], true, out ChoiceTone parsedTone))
                                    {
                                        foundTone = parsedTone;
                                        break;
                                    }
                                }
                            }
                        }

                        // 교섭 TONE 액션이 발견되었다면 말풍선 연출 코루틴 실행
                        if (foundTone.HasValue)
                        {
                            StartCoroutine(PlaySpeechBubbleRoutine(foundTone.Value, actionStr, nextTargetID));
                        }
                        else
                        {
                            // TONE이 없는 일반 선택지라면 기존처럼 즉시 실행
                            ExecuteAction(actionStr);          
                            OnChoiceSelected(nextTargetID);    
                        }
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
            inputCooldown = 0.05f;
            isProcessingChoice = false;
            // 선택지 UI 정리
            isWaitingForChoice = false;
            choiceContainer.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null); // 포커스 해제

            // 교섭용 콜백이 연결되어 있다면, 값을 쏴주고 즉시 종료
            if (onChoiceMade != null)
            {
                onChoiceMade.Invoke(nextTargetID);
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
            // 조건이 비어있으면 검사 없이 무조건 통과
            if (string.IsNullOrEmpty(conditionData)) return true;

            // 여러 조건을 ';'로 구분하여 모두 만족하는지(AND) 검사
            string[] conditions = conditionData.Split(';');

            foreach (string cond in conditions)
            {
                string[] parts = cond.Trim().Split(':');
                if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) continue;

                string command = parts[0].ToUpper();

                switch (command)
                {
                    case "HASITEM":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            if (itemID.StartsWith("Gold_"))
                            {
                                if (int.TryParse(itemID.Substring(5), out int reqGold))
                                {
                                    // 플레이어의 소지금이 요구 골드보다 크거나 같아야 함
                                    if (ManagerRoot.Finance.CurrentMoney < reqGold) return false;
                                }
                            }
                            else
                            {
                                // 필요시 parts[2]를 이용해 요구 수량을 파싱 (예: HASITEM:Potion:3)
                                if (!ManagerRoot.Inventory.HasItem(itemID)) return false;
                            }
                        }
                        break;
                        
                    case "FLAG":
                        if (parts.Length >= 2)
                        {
                            string flagName = parts[1];
                            if (!ManagerRoot.Flag.CheckFlag(flagName)) return false;
                        }
                        break;
                }
            }

            return true;
        }

        private void ExecuteAction(string actionData)
        {
            if (string.IsNullOrEmpty(actionData)) return;

            string[] actions = actionData.Split(';'); 

            foreach (string act in actions)
            {
                string[] parts = act.Trim().Split(':'); 
                if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) continue;

                string command = parts[0].ToUpper();

                switch (command)
                {
                    case "TONE":
                        if (parts.Length >= 2 && currentMonster != null)
                        {
                            if (Enum.TryParse<ChoiceTone>(parts[1], true, out ChoiceTone tone))
                            {
                                MoodDelta delta = NegotiationCalculator.CalculateMoodChange(tone, currentMonster, new EnvironmentState());
                                
                                currentMonster.CurrentAnger += delta.addedAnger;
                                currentMonster.CurrentJoy += delta.addedJoy;
                                currentMonster.CurrentInterest += delta.addedInterest;
                                
                                Debug.Log($"[교섭 액션] {tone} 선택 -> 분노:{currentMonster.CurrentAnger}, 기쁨:{currentMonster.CurrentJoy}, 흥미:{currentMonster.CurrentInterest}");
                            }
                        }
                        break;

                    case "ADD_MOOD":
                        // TODO: 강제 감정치 조절 (예: "ADD_MOOD:-50", "ADD_JOY:30", "ADD_ANGER:50")
                        break;

                    case "REMOVE":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            if (!string.IsNullOrEmpty(itemID))
                            {
                                if (itemID.StartsWith("Gold_"))
                                {
                                    if (int.TryParse(itemID.Substring(5), out int gold))
                                    {
                                        ManagerRoot.Finance.SubMoney(gold);
                                        Debug.Log($"[Action] MONEY 차감: {gold}");
                                    }
                                }
                                else
                                {
                                    // REMOVE:Potion:3 -> 3개 삭제, 없으면 1개
                                    int removeCount = 1;
                                    if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedCount))
                                    {
                                        removeCount = parsedCount;
                                    }

                                    ManagerRoot.Inventory.RemoveItem(itemID, removeCount);
                                    Debug.Log($"[Action] 아이템 차감: {itemID} x {removeCount}");
                                }
                            }
                        }
                        break;

                    case "SET_FLAG":
                        if (parts.Length >= 3)
                        {
                            string flagID = parts[1].Trim();
                            bool state = parts[2].Trim().ToLower() == "true"; 
                            
                            if(!string.IsNullOrEmpty(flagID))
                            {
                                ManagerRoot.Flag.SetFlag(flagID, state);
                                Debug.Log($"[Action] 플래그 설정: {flagID} -> {state}");
                            }
                        }
                        break;

                    case "BATTLE":
                        // TODO: 전투 재개
                        Debug.Log("[Action] 전투 개시 트리거 발생!");
                        break;
                }
            }
        }

        // 자신(Dialogue)과 외부 타겟(Battle)을 하나로 묶어 동시에 흔드는 코루틴
        private IEnumerator UIShakeRoutine(float duration, float magnitude)
        {
            // 흔들어야 할 모든 객체들의 원래 위치를 저장할 딕셔너리
            Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();

            // Dialogue 캔버스 내부의 자식들 등록
            if (uiCanvas != null)
            {
                foreach (Transform child in uiCanvas.transform)
                {
                    originalPositions[child] = child.localPosition;
                }
            }

            // 외부 타겟(배틀 UI 등) 등록
            if (additionalShakeTargets != null)
            {
                foreach (Transform target in additionalShakeTargets)
                {
                    if (target != null && !originalPositions.ContainsKey(target))
                    {
                        originalPositions[target] = target.localPosition;
                    }
                }
            }

            float elapsed = 0f;

            // 등록된 모든 UI를 한꺼번에 동일한 방향으로 흔듦
            while (elapsed < duration)
            {
                float offsetX = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float offsetY = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                Vector3 offset = new Vector3(offsetX, offsetY, 0f);

                foreach (var kvp in originalPositions)
                {
                    kvp.Key.localPosition = kvp.Value + offset;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 흔들림이 끝나면 모두 원래 위치로 정확히 복구
            foreach (var kvp in originalPositions)
            {
                kvp.Key.localPosition = kvp.Value;
            }
        }

        // 말풍선 연출 후 다음 대사로 넘어가는 코루틴
        private IEnumerator PlaySpeechBubbleRoutine(ChoiceTone tone, string actionStr, string nextTargetID)
        {
            // 선택지 UI 숨기기 및 조작 잠금
            isWaitingForChoice = false;
            choiceContainer.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);

            // TONE에 맞는 이미지 매핑
            Sprite targetSprite = null;
            switch (tone)
            {
                case ChoiceTone.Gentle: //우호적
                case ChoiceTone.Persuade: // 설득  
                    targetSprite = bubbleGentle;
                break;
                
                case ChoiceTone.Threat: // 위압적
                case ChoiceTone.Mad: // 화냄
                    targetSprite = bubbleThreat;
                break;

                case ChoiceTone.Accept: // 수락
                    targetSprite = bubbleAccept;
                break;

                case ChoiceTone.Refuse: // 거절
                    targetSprite = bubbleRefuse;
                break;
                
                case ChoiceTone.Relieve: // 안심
                case ChoiceTone.Request: // 요구
                case ChoiceTone.Bribe: // 상납
                case ChoiceTone.Flirt: // 희롱
                case ChoiceTone.Insult: // 모욕
                break;
                
            }

            // 연출 실행
            if (targetSprite != null && speechBubbleUI != null)
            {
                speechBubbleUI.sprite = targetSprite;
                speechBubbleUI.SetNativeSize();
                speechBubbleUI.gameObject.SetActive(true);

                // 짧고 강하게 화면을 흔듦 (지속 시간, 강도)
                StartCoroutine(UIShakeRoutine(0.3f, 30f));

                // 사운드 재생
                ManagerRoot.Sound.PlaySFX(SfxID.Dialogue_Impact);

                // 말풍선이 떠 있는 동안 잠시 대기
                yield return YieldCache.WaitForSeconds(0.8f);

                speechBubbleUI.gameObject.SetActive(false);
            }

            // 효과 종료 후, 액션과 대사를 실행
            ExecuteAction(actionStr);
            OnChoiceSelected(nextTargetID);
        }

        // 유저가 타이핑이 끝난 후 클릭했을 때 실행되는 함수
        void AdvanceLine()
        {
            var currentData = currentEventLines[currentLineIndex];
            string nextTargetID = currentData.ContainsKey("NextID") ? currentData["NextID"].Trim() : "";
            string[] parts = nextTargetID.Split(':');
            string param = parts.Length > 1 ? parts[1].Trim() : "";
            string choice = parts.Length > 2 ? parts[2].Trim() : "";
            string item = parts.Length > 3 ? parts[3].Trim() : "";

            // NextID가 "CHECK_MOOD:목적지" 형태일 경우 점수 판정

            if (nextTargetID.StartsWith("CHECK_MOOD"))
            {
                negotiationResult = NegotiationResult.PLAYER_TURN;
                // 플레이어의 원군 요청
                if (param == "RECRUIT")
                {
                    if (currentMonster.CurrentJoy >= 100 || currentMonster.CurrentInterest >= 100) 
                    {
                        nextTargetID = "SUCCESS_RECRUIT"; // 테이밍 성공
                    }
                    else
                    {
                        nextTargetID = "FAIL_RECRUIT"; // 테이밍 실패
                    }
                }
                // 플레이어의 아이템 요구
                else if (param == "ITEM")
                {
                    if (currentMonster.CurrentJoy >= 50 && currentMonster.CurrentInterest >= 50)
                    {
                        nextTargetID = "SUCCESS_ITEM";
                    }
                    else
                    {
                        nextTargetID = "FAIL_ITEM";
                    }
                }
                // 몬스터의 아이템 요구
                else if (param == "GIVE")
                {
                    nextTargetID = "NEGO_START"; // 기본적으로 다음 대화로 이어짐
                    if (choice == "ACCEPT")
                    {
                        if (item.StartsWith("HP_")) 
                        {
                            // HP 요구 (예: GIVE:ACCEPT:HP_50)
                            int amount = int.Parse(item.Substring(3));
                            
                            // BattleManager에 HP 차감 요청
                            bool success = onResourceDemanded != null && onResourceDemanded.Invoke("HP", amount);
                            
                            if (!success) 
                            { 
                                negotiationResult = NegotiationResult.MONSTER_TURN; 
                                nextTargetID = "FAIL"; 
                            }
                        }
                        else if (item.StartsWith("MP_")) 
                        {
                            // MP 요구 (예: GIVE:ACCEPT:MP_20)
                            int amount = int.Parse(item.Substring(3));
                            
                            bool success = onResourceDemanded != null && onResourceDemanded.Invoke("MP", amount);
                            
                            if (!success) 
                            { 
                                negotiationResult = NegotiationResult.MONSTER_TURN; 
                                nextTargetID = "FAIL"; 
                            }
                        }
                        else if (int.TryParse(item, out int gold)) 
                        {
                            // 골드 요구
                            if (ManagerRoot.Finance.CurrentMoney >= gold) ManagerRoot.Finance.SubMoney(gold);
                            else { negotiationResult = NegotiationResult.MONSTER_TURN; nextTargetID = "INSUFFICIENT_ITEM"; }
                        }
                        else if (ManagerRoot.Inventory.HasItem(item)) 
                        {
                            // 아이템 요구
                            ManagerRoot.Inventory.RemoveItem(item, 1);
                        }
                        else
                        {
                            negotiationResult = NegotiationResult.MONSTER_TURN;
                            nextTargetID = "INSUFFICIENT_ITEM";
                        }
                    }
                    else
                    {
                        nextTargetID = "END";
                    }
                }
                else if (param == "ANGRY")
                {
                    negotiationResult = NegotiationResult.MONSTER_TURN;
                    nextTargetID = "FAIL";
                }
                else if (param == "DISAPPOINT")
                {
                    negotiationResult = NegotiationResult.MONSTER_TURN;
                    nextTargetID = "FAIL";
                }
                else
                {
                    nextTargetID = param;
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

        // CharacterID를 통해 DB에서 이름을 찾아 반환하는 헬퍼 메서드
        private string GetCharacterNameFromDB(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return null;

            var chrDB = ManagerRoot.Database.charDB;
            var npcDB = ManagerRoot.Database.npcDB;
            var monDB = ManagerRoot.Database.monsterDB;

            // 1순위: NPC DB 검색
            var npcEntry = npcDB != null ? npcDB.GetEntry(charId) : null;
            if (npcEntry != null) return npcEntry.name;

            // 2순위: Character DB 검색
            var charEntry = chrDB != null ? chrDB.GetEntry(charId) : null;
            if (charEntry != null)
            {
                // 파티에 존재하여 변경된 이름이 있다면 그것을 우선 사용
                var pData = ManagerRoot.Party.GetCharacterByID(charId);
                return pData != null ? pData.name : charEntry.name;
            }

            // 3순위: Monster DB 검색
            var monEntry = monDB != null ? monDB.GetEntry(charId) : null;
            if (monEntry != null) return monEntry.name;

            return null;
        }

        // DialogueEditorWindow에서 실시간 미리보기용으로 사용함
        public void InitializeDynamic(List<Dictionary<string, string>> dynamicLines, Action<int> onComplete = null)
        {
            currentEventLines = dynamicLines;
            if (currentEventLines == null || currentEventLines.Count == 0) return;
            
            onDialogueFinished = onComplete; 
            
            // 에디터 미리보기 모드이므로 몬스터와 자원 요구 콜백을 비움
            currentMonster = null;
            onResourceDemanded = null;
            negotiationResult = NegotiationResult.PLAYER_TURN;
            
            StartDialogueFlow();
        }
    }
}