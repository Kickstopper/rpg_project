using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Manager;
using Data.Database;

namespace UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Settings")]
        public CharacterDatabase portraitDB; // inspector에서 설정
        public float typingSpeed = 0.05f; // 글자당 시간 (작을수록 빠름)
        public AudioClip typingSound;     // 타이핑 효과음
        private AudioSource audioSource;

        [Header("UI Components")]
        public GameObject uiPanel;
        public Image portraitImageUI;   // 대화창 옆 얼굴 (Portrait)
        public Image standingImageUI;   // 화면 중앙 전신 (Standing) - 새로 추가!
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI contentText;

        private List<Dictionary<string, string>> currentEventLines;
        private int currentLineIndex = 0;
        private bool isDialogueActive = false;

        // 타이핑 상태 관리 변수
        private bool isTyping = false;
        private Coroutine typingCoroutine;

        // 대화 종료 시 알려줄 이벤트 (C# Action)
        public event Action OnDialogueFinished;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        void Start()
        {
            uiPanel.SetActive(false); // 시작 시 숨김
        }

        // 외부에서 호출: 대화 시작
        public void StartDialogue(string eventID)
        {
            currentEventLines = DialogueManager.Instance.GetEventData(eventID);
            
            if (currentEventLines == null || currentEventLines.Count == 0) return;

            currentLineIndex = 0;
            isDialogueActive = true;
            uiPanel.SetActive(true);

            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            if (currentLineIndex >= currentEventLines.Count)
            {
                EndDialogue();
                return;
            }

            var lineData = currentEventLines[currentLineIndex];
            
            // 텍스트 및 이름 설정
            nameText.text = lineData.ContainsKey("Speaker") ? lineData["Speaker"] : "";
            contentText.text = lineData.ContainsKey("Text") ? lineData["Text"] : "";

            // 초상화 로드 변경 (Resources.Load 삭제)
            string portraitID = lineData.ContainsKey("Portrait") ? lineData["Portrait"] : "";
        
            if (!string.IsNullOrEmpty(portraitID))
            {
                // 데이터베이스에서 Entry 전체를 가져옴
                var entry = portraitDB.GetEntry(portraitID);
                
                if (entry != null)
                {
                    // 얼굴 이미지 적용
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

                    // 전신 이미지 적용 (Standing)
                    // 만약 전신 이미지가 등록되어 있다면 표시, 없으면 숨김
                    if (entry.standingImage != null)
                    {
                        standingImageUI.sprite = entry.standingImage;
                        standingImageUI.SetNativeSize();
                        standingImageUI.enabled = true;
                        // standingImageUI.SetNativeSize(); // 필요시 원본 비율 유지
                    }
                    else
                    {
                        // 전신 이미지는 없을 수도 있으므로(표정 변화만 있는 경우 등)
                        // 기획에 따라 이전 이미지를 유지할지, 숨길지 결정해야 합니다.
                        // 여기서는 '없으면 숨김'으로 처리했습니다.
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
            contentText.text = fullText; // 텍스트를 미리 다 넣어둠 (안 보일 뿐)
            contentText.maxVisibleCharacters = 0; // 글자 표시 개수를 0으로 초기화

            // 기존 타이핑이 있다면 중지하고 새로 시작
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText());
        }

        // 타이핑 효과 코루틴
        IEnumerator TypeText()
        {
            isTyping = true;

            // TMP가 텍스트 정보를 갱신할 때까지 한 프레임 대기 (필수)
            contentText.ForceMeshUpdate(); 

            int totalVisibleCharacters = contentText.textInfo.characterCount; // 실제 글자 수 (태그 제외)
            int counter = 0;

            while (counter < totalVisibleCharacters)
            {
                int visibleCount = counter % (totalVisibleCharacters + 1);
                contentText.maxVisibleCharacters = visibleCount + 1;

                // 소리 재생 (너무 빠르면 귀 아프므로 매 글자마다 재생하되 피치 조절)
                if (typingSound != null)
                {
                    // 약간의 피치 변화를 주어 기계적인 느낌을 줄임
                    audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(typingSound);
                }

                counter++;
                yield return new WaitForSeconds(typingSpeed);
            }

            // 완료 처리
            contentText.maxVisibleCharacters = totalVisibleCharacters;
            isTyping = false;
        }

        void Update()
        {
            if (!isDialogueActive) return;

            // 입력 감지 (마우스 클릭, 스페이스바, Enter 등)
            if (Input.GetButtonDown("Submit") || Input.GetMouseButtonDown(0))
            {
                if (isTyping)
                {
                    // 타이핑 중이라면 -> 즉시 완성 (Skip)
                    CompleteTypingImmediately();
                }
                else
                {
                    // 타이핑이 끝났다면 -> 다음 대사로
                    AdvanceLine();
                }
            }
        }

        // 타이핑 스킵: 즉시 모든 글자 표시
        void CompleteTypingImmediately()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            
            contentText.maxVisibleCharacters = int.MaxValue; // 전부 표시
            isTyping = false;
        }

        void AdvanceLine()
        {
            currentLineIndex++;
            ShowCurrentLine();
        }

        void EndDialogue()
        {
            isDialogueActive = false;
            uiPanel.SetActive(false);
            OnDialogueFinished?.Invoke();
        }
    }
}