using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Manager;

namespace UI
{
    public class TerminalUIManager : MonoBehaviour
    {
        public static TerminalUIManager Instance { get; private set; }

        public bool IsSelectionComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public TerminalData SelectedTerminal { get; private set; }

        [Header("UI References (Top)")]
        public Image destinationImage;
        public TextMeshProUGUI percentageText;
        public TextMeshProUGUI dungeonInfoText;
        public Image fadeOverlay;

        [Header("UI References (Bottom Grid)")]
        public RectTransform buttonGridContainer;
        public GameObject terminalGridButtonPrefab;
        public GridLayoutGroup gridLayoutGroup;

        [Header("Settings")]
        private const int GRID_WIDTH = 5;
        private const int GRID_HEIGHT = 5;
        private const int TOTAL_BUTTONS = 25;

        private List<TerminalGridButton> _buttons = new List<TerminalGridButton>();
        private Vector2[] _cachedButtonPositions = new Vector2[TOTAL_BUTTONS];
        
        private int _currentIndex = 0;
        private bool _isAnimating = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            
            // 프리팹 초기화 세팅
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                GameObject obj = Instantiate(terminalGridButtonPrefab, buttonGridContainer);
                _buttons.Add(obj.GetComponent<TerminalGridButton>());
            }
        }

        public void OpenTerminal(string currentTerminalID)
        {
            IsSelectionComplete = false;
            IsCanceled = false;
            SelectedTerminal = null;
            _isAnimating = true;

            gameObject.SetActive(true);

            List<TerminalData> availableList = TerminalManager.Instance.GetAvailableTerminals(currentTerminalID);

            // 데이터 맵핑 (모자란 부분은 null로 채워 빈 슬롯 생성)
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                TerminalData data = i < availableList.Count ? availableList[i] : null;
                _buttons[i].Initialize(i, data, this);
            }

            StartCoroutine(IntroSequenceRoutine());
        }

        private IEnumerator IntroSequenceRoutine()
        {
            fadeOverlay.DOFade(0, 0.5f).OnStart(()=>{
                fadeOverlay.gameObject.SetActive(true);
            });

            // 1. GridLayoutGroup을 켜서 자동으로 정렬하게 만듭니다.
            gridLayoutGroup.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonGridContainer);
            yield return null; // 한 프레임 대기하여 UI 갱신 보장

            // 2. 정렬된 최종 위치(목표 좌표)를 캐싱하고, GridLayoutGroup을 끕니다. (DOTween과 충돌 방지)
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                _cachedButtonPositions[i] = _buttons[i].GetComponent<RectTransform>().anchoredPosition;
            }
            gridLayoutGroup.enabled = false;

            // 3. 화면 밖 좌우로 날려보냅니다.
            float screenWidth = Screen.width;
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                RectTransform rt = _buttons[i].GetComponent<RectTransform>();
                float startX = (Random.value > 0.5f) ? -screenWidth : screenWidth; // 좌 or 우 랜덤
                rt.anchoredPosition = new Vector2(startX, _cachedButtonPositions[i].y);
                
                // DOTween으로 원래 자리로 복귀 (순서대로 약간씩 딜레이를 줌)
                float delay = Random.Range(0f, 0.4f);
                rt.DOAnchorPosX(_cachedButtonPositions[i].x, 0.5f)
                  .SetDelay(delay)
                  .SetEase(Ease.OutExpo);
            }

            // 4. 애니메이션이 다 끝날 때까지 대기
            yield return new WaitForSeconds(0.9f);

            // 5. 첫 번째 칸을 하이라이트 처리하며 조작 활성화
            _isAnimating = false;
            ChangeSelection(0);
        }

        private void Update()
        {
            if (_isAnimating || !gameObject.activeSelf) return;

            // 취소 (ESC / Tab)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
            {
                OnCancel();
                return;
            }

            // 확정 (Space / Enter)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                TryConfirmSelection();
                return;
            }

            // 방향키 조작 (WASD / Arrows)
            int x = _currentIndex % GRID_WIDTH;
            int y = _currentIndex / GRID_WIDTH;

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) x++;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) x--;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) y++;
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) y--;

            // 그리드 범위 제한
            x = Mathf.Clamp(x, 0, GRID_WIDTH - 1);
            y = Mathf.Clamp(y, 0, GRID_HEIGHT - 1);

            int newIndex = y * GRID_WIDTH + x;
            if (newIndex != _currentIndex)
            {
                ChangeSelection(newIndex);
            }
        }

        public void OnButtonHovered(int index)
        {
            if (_isAnimating || _currentIndex == index) return;
            ChangeSelection(index);
        }

        public void OnButtonClicked(int index)
        {
            if (_isAnimating) return;

            if (_currentIndex == index)
            {
                // 재차 클릭 시 확정
                TryConfirmSelection();
            }
            else
            {
                ChangeSelection(index);
            }
        }

        private void ChangeSelection(int newIndex)
        {
            _buttons[_currentIndex].SetHighlight(false);
            _currentIndex = newIndex;
            _buttons[_currentIndex].SetHighlight(true);

            UpdateTopPanelInfo(_buttons[_currentIndex].Data);
        }

        private void UpdateTopPanelInfo(TerminalData data)
        {
            if (data != null)
            {
                if (data.destinationImage != null) destinationImage.sprite = data.destinationImage;
                
                percentageText.text = "100%"; // 예시. 혹은 data.syncRate 등으로 확장 가능
                percentageText.color = Color.green;

                dungeonInfoText.text = $"<size=120%>{data.displayName}</size>\n<color=#A0A0A0>Floor: {data.floorNumber}</color>\n\n{data.description}";
            }
            else
            {
                // 빈 데이터일 경우 글리치/노이즈 연출 느낌
                destinationImage.sprite = null; 
                percentageText.text = "ERR";
                percentageText.color = Color.red;
                dungeonInfoText.text = "<color=red>CONNECTION FAILED</color>\nNo terminal signal detected at this coordinate.";
            }
        }

        private void TryConfirmSelection()
        {
            TerminalData selectedData = _buttons[_currentIndex].Data;
            
            // 데이터가 있는 정상 터미널만 이동 가능
            if (selectedData != null)
            {
                StartCoroutine(OutroSequenceRoutine(selectedData));
            }
            else
            {
                // 에러 사운드 출력
                // SoundManager.Instance.PlaySFX(SfxID.Error);
            }
        }

        private IEnumerator OutroSequenceRoutine(TerminalData selectedData)
        {
            _isAnimating = true;

            // 선택 확정 사운드 재생
            // SoundManager.Instance.PlaySFX(SfxID.Terminal_Confirm);

            // 버튼들을 화면 밖으로 무작위로 날려버림
            float offScreenX = 3000f;
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                RectTransform rt = _buttons[i].GetComponent<RectTransform>();
                float endX = (Random.value > 0.5f) ? -offScreenX : offScreenX;
                float delay = Random.Range(0f, 0.3f);
                
                rt.DOAnchorPosX(endX, 0.4f)
                  .SetDelay(delay)
                  .SetEase(Ease.InExpo);
            }

            yield return new WaitForSeconds(0.5f);

            // 선택 완료 처리 후 컨트롤러에 턴을 넘김
            SelectedTerminal = selectedData;
            IsCanceled = false;
            IsSelectionComplete = true;
            gameObject.SetActive(false);
        }

        public void OnCancel()
        {
            if (_isAnimating) return;
            StartCoroutine(CancelSequenceRoutine());
        }

        private IEnumerator CancelSequenceRoutine()
        {
            _isAnimating = true;

            // 버튼들을 화면 밖으로 퇴장
            float offScreenX = 3000f; 
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                RectTransform rt = _buttons[i].GetComponent<RectTransform>();
                float endX = (Random.value > 0.5f) ? -offScreenX : offScreenX; 
                float delay = Random.Range(0f, 0.2f);
                
                rt.DOAnchorPosX(endX, 0.3f)
                  .SetDelay(delay)
                  .SetEase(Ease.InExpo);
            }
            
            fadeOverlay.DOFade(1, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 0f);});            
            // 애니메이션이 끝날 때까지 대기
            yield return new WaitForSeconds(0.5f);

            // 취소 확정 및 UI 종료 (컨트롤러로 제어권 반환)
            fadeOverlay.color = Color.black;
            fadeOverlay.gameObject.SetActive(true);

            SelectedTerminal = null;
            IsCanceled = true;
            IsSelectionComplete = true;
            gameObject.SetActive(false);
        }
    }
}