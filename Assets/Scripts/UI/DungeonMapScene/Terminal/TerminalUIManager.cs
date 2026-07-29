using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Manager;
using Controller;

namespace UI
{
    public enum TerminalState { MainMenu, WarpMode }

    public class TerminalUIManager : MonoBehaviour
    {
        public static TerminalUIManager Instance { get; private set; }

        public bool IsSelectionComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public TerminalData SelectedTerminal { get; private set; }

        // 메인 메뉴 관리를 위한 변수들
        private TerminalState _currentState;
        private int _mainMenuIndex = 0;
        private string _currentTerminalID;

        [Header("Main Menu Panel")]
        public GameObject mainMenuPanel; // WARP, SAVE, LOAD 버튼들을 담고 있는 부모 컨테이너
        [Tooltip("0: WARP, 1: SAVE, 2: LOAD 버튼 순서대로 할당")]
        public RectTransform[] mainMenuButtons; 
        public SaveLoadUIController saveLoadUI; // 인스펙터에서 씬의 SaveLoadUI를 드래그 앤 드롭

        [Header("UI References (Top)")]
        public Image destinationImage;
        public TextMeshProUGUI percentageText;
        public TextMeshProUGUI infoText;
        public CanvasGroup digitalRain;
        public Image fadeOverlay;
        public StarWarpController warp;

        [Header("UI References (Bottom Grid)")]
        public RectTransform buttonGridContainer;
        public GameObject terminalGridButtonPrefab;
        public GridLayoutGroup gridLayoutGroup;

        [Header("ASCII Warp Effect")]
        public AsciiObjectPool objectPool;
        public RectTransform asciiCanvasRoot;
        public Image characterImage;      
        public TextAsset characterAscii;  
        public float gridSpacing = 16f;
        
        private List<GameObject> _activeAsciiNodes = new List<GameObject>();

        [Header("Settings")]
        private const int GRID_WIDTH = 5;
        private const int GRID_HEIGHT = 5;
        private const int TOTAL_BUTTONS = 25;

        private List<TerminalGridButton> _buttons = new List<TerminalGridButton>();
        private Vector2[] _cachedButtonPositions = new Vector2[TOTAL_BUTTONS];

        private WaitForSeconds wait05 = new WaitForSeconds(0.5f);
        private WaitForSeconds wait10 = new WaitForSeconds(1f);
        
        private int _currentIndex = 0;
        private bool _isAnimating = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                GameObject obj = Instantiate(terminalGridButtonPrefab, buttonGridContainer);
                _buttons.Add(obj.GetComponent<TerminalGridButton>());
            }
        }

        public void OpenTerminal(string currentTerminalID)
        {
            ManagerRoot.Sound.PlayBGM(Data.BgmID.Terminal);
            IsSelectionComplete = false;
            IsCanceled = false;
            SelectedTerminal = null;
            _isAnimating = false;

            // 초기 상태를 메인 메뉴로 설정
            _currentTerminalID = currentTerminalID;
            _currentState = TerminalState.MainMenu;
            _mainMenuIndex = 0;
            
            gameObject.SetActive(true);
            warp.gameObject.SetActive(false);
            digitalRain.alpha = 0f;
            if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

            // 메인 메뉴 활성화, 워프 그리드 비활성화
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (buttonGridContainer != null) buttonGridContainer.gameObject.SetActive(false);

            UpdateMainMenuHighlight();
            UpdateTopPanelInfo(null);
        }

        // =========================================================
        // 메인 메뉴 관련 로직 시작
        // =========================================================

        private void UpdateMainMenuHighlight()
        {
            if (mainMenuButtons == null || mainMenuButtons.Length == 0) return;
            for (int i = 0; i < mainMenuButtons.Length; i++)
            {
                Button btn = mainMenuButtons[i].GetComponent<Button>();
                if (btn != null && (i == _mainMenuIndex)) btn.Select();
            }
        }

        private void TryConfirmMainMenu()
        {
            if (_mainMenuIndex == 0) // WARP 선택
            {
                _currentState = TerminalState.WarpMode;
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                if (buttonGridContainer != null) buttonGridContainer.gameObject.SetActive(true);

                _isAnimating = true;
                List<TerminalData> availableList = ManagerRoot.Terminal.GetAvailableTerminals(_currentTerminalID);

                for (int i = 0; i < TOTAL_BUTTONS; i++)
                {
                    TerminalData data = i < availableList.Count ? availableList[i] : null;
                    _buttons[i].Initialize(i, data, this);
                }

                StartCoroutine(IntroSequenceRoutine());
            }
            else if (_mainMenuIndex == 1) // SAVE 선택
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false); 
                if (saveLoadUI != null) saveLoadUI.Open(true);
            }
            else if (_mainMenuIndex == 2) // LOAD 선택
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false); 
                if (saveLoadUI != null) saveLoadUI.Open(false);
            }
        }

        // 마우스 클릭 이벤트 처리
        public void OnMainMenuButtonClicked(int index)
        {
            // 애니메이션 중이거나, 현재 메인 메뉴 상태가 아니거나, Save/Load 창이 열려있다면 무시
            if (_isAnimating || !gameObject.activeSelf || _currentState != TerminalState.MainMenu) return;
            if (saveLoadUI != null && saveLoadUI.gameObject.activeSelf) return;

            // 클릭한 버튼의 인덱스로 현재 선택 인덱스 동기화
            _mainMenuIndex = index;
            UpdateMainMenuHighlight();

            // 스페이스/엔터 키를 누른 것과 동일하게 실행
            TryConfirmMainMenu();
        }

        private void ReturnToMainMenu()
        {
            _currentState = TerminalState.MainMenu;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (buttonGridContainer != null) buttonGridContainer.gameObject.SetActive(false);
            UpdateTopPanelInfo(null); // 상단 디스플레이 정보 초기화
        }

        // =========================================================
        // 메인 메뉴 관련 로직 종료
        // =========================================================

        private IEnumerator IntroSequenceRoutine()
        {
            fadeOverlay.DOFade(0, 0.5f).OnStart(()=>{
                fadeOverlay.gameObject.SetActive(true);
            });

            gridLayoutGroup.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonGridContainer);
            yield return null; 

            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                _cachedButtonPositions[i] = _buttons[i].GetComponent<RectTransform>().anchoredPosition;
            }
            gridLayoutGroup.enabled = false;

            float screenWidth = Screen.width;
            for (int i = 0; i < TOTAL_BUTTONS; i++)
            {
                RectTransform rt = _buttons[i].GetComponent<RectTransform>();
                float startX = (Random.value > 0.5f) ? -screenWidth : screenWidth; 
                rt.anchoredPosition = new Vector2(startX, _cachedButtonPositions[i].y);
                
                float delay = Random.Range(0f, 0.4f);
                rt.DOAnchorPosX(_cachedButtonPositions[i].x, 0.5f)
                  .SetDelay(delay)
                  .SetEase(Ease.OutExpo);
            }

            yield return wait10;

            _isAnimating = false;
            ChangeSelection(0);
        }

        private void Update()
        {
            if (_isAnimating || !gameObject.activeSelf) return;

            // SaveLoad UI가 열려있다면 Terminal의 입력 처리를 일시정지
            if (saveLoadUI != null && saveLoadUI.gameObject.activeSelf) return;

            if (_currentState == TerminalState.MainMenu)
            {
                // SaveLoadUI가 닫히고 터미널로 복귀했을 때 메인 메뉴를 다시 켜줌
                if (mainMenuPanel != null && !mainMenuPanel.activeSelf)
                {
                    mainMenuPanel.SetActive(true);
                }

                // 메인 메뉴 전용 입력 처리
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
                {
                    OnCancel(); // 터미널 완전 종료
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    TryConfirmMainMenu();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    _mainMenuIndex++;
                    if (mainMenuButtons != null && _mainMenuIndex >= mainMenuButtons.Length) _mainMenuIndex = 0;
                    UpdateMainMenuHighlight();
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    _mainMenuIndex--;
                    if (mainMenuButtons != null && _mainMenuIndex < 0) _mainMenuIndex = mainMenuButtons.Length - 1;
                    UpdateMainMenuHighlight();
                }
            }
            else if (_currentState == TerminalState.WarpMode)
            {
                // 기존 워프 메뉴 입력 처리
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
                {
                    ReturnToMainMenu(); // ESC를 누르면 터미널 종료 대신 메인 메뉴로 복귀
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    TryConfirmSelection();
                    return;
                }

                int x = _currentIndex % GRID_WIDTH;
                int y = _currentIndex / GRID_WIDTH;

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) x++;
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) x--;
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) y++;
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) y--;

                x = Mathf.Clamp(x, 0, GRID_WIDTH - 1);
                y = Mathf.Clamp(y, 0, GRID_HEIGHT - 1);

                int newIndex = y * GRID_WIDTH + x;
                if (newIndex != _currentIndex)
                {
                    ChangeSelection(newIndex);
                }
            }
        }

        public void OnButtonHovered(int index)
        {
            if (_isAnimating || _currentIndex == index || _currentState != TerminalState.WarpMode) return;
            ChangeSelection(index);
        }

        public void OnButtonClicked(int index)
        {
            if (_isAnimating || _currentState != TerminalState.WarpMode) return;

            if (_currentIndex == index)
            {
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
                
                percentageText.text = "100%"; 
                percentageText.color = Color.green;

                infoText.text = $"<size=120%>{data.displayName}</size>\n<color=#A0A0A0>Floor: {data.floorNumber}</color>\n\n{data.description}";
            }
            else
            {
                destinationImage.sprite = null; 
                
                if (_currentState == TerminalState.MainMenu)
                {
                    // 메인 메뉴 상태일 때의 환영 메시지
                    percentageText.text = "SELECT SERVICE";
                    percentageText.color = Color.cyan;
                    
                    infoText.text = "<size=120%>SYSTEM ONLINE</size>\n<color=#A0A0A0>Awaiting Input</color>\n\nPlease select a service to use.";
                }
                else
                {
                    // 워프 모드에서 빈 슬롯을 가리켰을 때의 에러 메시지
                    percentageText.text = "ERR";
                    percentageText.color = Color.red;
                    infoText.text = "<color=red>CONNECTION FAILED</color>\nNo terminal signal detected at this coordinate.";
                }
            }
        }

        private void TryConfirmSelection()
        {
            TerminalData selectedData = _buttons[_currentIndex].Data;
            
            if (selectedData != null)
            {
                StartCoroutine(OutroSequenceRoutine(selectedData));
            }
        }

        private IEnumerator OutroSequenceRoutine(TerminalData selectedData)
        {
            _isAnimating = true;
            
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

            ManagerRoot.Sound.StopBGM();

            fadeOverlay.DOFade(1f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 0f);});
            
            ManagerRoot.Sound.PlaySFX(Data.SfxID.Computer, 0.5f);
            digitalRain.DOFade(1f, 0.5f).SetDelay(0.2f);
            
            yield return wait10;

            if (characterImage != null && characterAscii != null && objectPool != null)
            {
                warp.gameObject.SetActive(true);
                warp.Reset();
                
                characterImage.gameObject.SetActive(true);
                yield return StartCoroutine(FadeSpriteAlpha(characterImage, 0f, 1f, 0.5f, Color.white));

                DrawAscii(characterAscii, 0f);
                yield return wait05;

                digitalRain.DOFade(0f, 0.5f);
                ManagerRoot.Sound.StopAllSFX(true);
                ManagerRoot.Sound.PlaySFX(Data.SfxID.Warp_Start);
                Color digitalTint = Color.cyan;
                StartCoroutine(FadeSpriteAlpha(characterImage, 1f, 0f, 1.0f, digitalTint));
                yield return StartCoroutine(RevealAsciiRandomly(1.0f));
                
                yield return wait05;
                
                ManagerRoot.Sound.PlaySFX(Data.SfxID.Warp_End);
                yield return StartCoroutine(ExplodeAndReturnAsciiRoutine(0.6f, 0.25f, 0.5f, 15f, 30f));
                
                yield return new WaitForSeconds(0.2f);

                warp.gameObject.SetActive(false);
                ManagerRoot.Sound.PlaySFX(Data.SfxID.Computer, 0.25f);
                digitalRain.DOFade(1f, 1.5f);

                StartCoroutine(FadeSpriteAlpha(characterImage, 0f, 1f, 1.5f, digitalTint));
                yield return StartCoroutine(DissolveAsciiRandomly(1.5f));
                
                objectPool.ReturnAllObjects(_activeAsciiNodes);
                _activeAsciiNodes.Clear();
                
                yield return wait05;

                fadeOverlay.DOFade(0f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 1f);});
                digitalRain.DOFade(0f, 0.5f);

                yield return StartCoroutine(FadeSpriteAlpha(characterImage, 1f, 0f, 0.5f, digitalTint));
                
                characterImage.gameObject.SetActive(false);
            }
            else
            {
                fadeOverlay.DOFade(0f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 1f);});
                digitalRain.DOFade(0f, 0.5f);
                yield return wait10;
            }
    
            yield return wait10;
            ManagerRoot.Sound.StopAllSFX();
            SelectedTerminal = selectedData;
            IsCanceled = false;
            IsSelectionComplete = true;
            gameObject.SetActive(false);
        }

        private void DrawAscii(TextAsset asciiData, float initialAlpha)
        {
            if (_activeAsciiNodes.Count > 0) 
            {
                objectPool.ReturnAllObjects(_activeAsciiNodes);
                _activeAsciiNodes.Clear();
            }

            string[] lines = asciiData.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            int totalRows = lines.Length;
            int totalCols = lines[0].Length;

            float startX = -(totalCols - 1) * gridSpacing * 0.5f;
            float startY = (totalRows - 1) * gridSpacing * 0.5f;

            for (int y = 0; y < totalRows; y++)
            {
                for (int x = 0; x < totalCols; x++)
                {
                    if (lines[y][x] == ' ') continue;

                    Vector2 pos = new Vector2(startX + (x * gridSpacing), startY - (y * gridSpacing));
                    GameObject nodeObj = objectPool.GetObjectFromPool(asciiCanvasRoot, pos);

                    RectTransform rect = nodeObj.GetComponent<RectTransform>();
                    rect.localScale = Vector3.one; 
                    rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);
                    rect.localRotation = Quaternion.identity;
                    
                    TextMeshProUGUI tmp = nodeObj.GetComponent<TextMeshProUGUI>();
                    tmp.text = lines[y][x].ToString();
                    tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, initialAlpha); 
                    _activeAsciiNodes.Add(nodeObj);
                }
            }
        }

        private IEnumerator FadeSpriteAlpha(Image img, float startAlpha, float endAlpha, float duration, Color tint)
        {
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
                Color currentColor = Color.Lerp(tint, Color.white, currentAlpha);
                currentColor.a = currentAlpha;
                img.color = currentColor;
                yield return null;
            }

            Color finalColor = Color.Lerp(tint, Color.white, endAlpha);
            finalColor.a = endAlpha;
            img.color = finalColor;
        }

        private IEnumerator RevealAsciiRandomly(float duration)
        {
            List<GameObject> shuffledNodes = new List<GameObject>(_activeAsciiNodes);
            ShuffleList(shuffledNodes);

            int totalNodes = shuffledNodes.Count;
            int nodesPerFrame = Mathf.CeilToInt(totalNodes / (duration / Time.deltaTime));

            int currentIndex = 0;
            while (currentIndex < totalNodes)
            {
                for (int i = 0; i < nodesPerFrame && currentIndex < totalNodes; i++)
                {
                    TextMeshProUGUI tmp = shuffledNodes[currentIndex].GetComponent<TextMeshProUGUI>();
                    tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
                    currentIndex++;
                }
                yield return null;
            }
        }

        private IEnumerator DissolveAsciiRandomly(float duration)
        {
            List<GameObject> shuffledNodes = new List<GameObject>(_activeAsciiNodes);
            ShuffleList(shuffledNodes);

            int totalNodes = shuffledNodes.Count;
            int nodesPerFrame = Mathf.CeilToInt(totalNodes / (duration / Time.deltaTime));

            int currentIndex = 0;
            while (currentIndex < totalNodes)
            {
                for (int i = 0; i < nodesPerFrame && currentIndex < totalNodes; i++)
                {
                    TextMeshProUGUI tmp = shuffledNodes[currentIndex].GetComponent<TextMeshProUGUI>();
                    tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0f);
                    currentIndex++;
                }
                yield return null;
            }
        }

        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private IEnumerator ExplodeAndReturnAsciiRoutine(float explodeTime, float waitTime, float returnTime, float expandMultiplier, float jitterAmount)
        {
            int count = _activeAsciiNodes.Count;
            Vector2[] originalPositions = new Vector2[count];
            Vector2[] targetPositions = new Vector2[count];
            float[] targetRotations = new float[count];
            RectTransform[] rects = new RectTransform[count];

            for (int i = 0; i < count; i++)
            {
                rects[i] = _activeAsciiNodes[i].GetComponent<RectTransform>();
                originalPositions[i] = rects[i].anchoredPosition;

                Vector2 expandVec = originalPositions[i];
                if (expandVec == Vector2.zero) expandVec = Random.insideUnitCircle.normalized * 10f;

                Vector2 randomJitter = Random.insideUnitCircle * Random.Range(0f, jitterAmount);
                targetPositions[i] = (expandVec * expandMultiplier) + randomJitter;
                targetRotations[i] = Random.Range(-90f, 90f);
            }

            warp.PlayWarpAndCollapse();

            float elapsed = 0f;
            while (elapsed < explodeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / explodeTime;
                float easeOut = 1f - Mathf.Pow(1f - t, 3f); 

                for (int i = 0; i < count; i++)
                {
                    rects[i].anchoredPosition = Vector2.Lerp(originalPositions[i], targetPositions[i], easeOut);
                    rects[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, targetRotations[i], easeOut));
                }
                yield return null;
            }

            yield return new WaitForSeconds(waitTime);

            elapsed = 0f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnTime;
                float easeIn = t * t * t; 

                for (int i = 0; i < count; i++)
                {
                    rects[i].anchoredPosition = Vector2.Lerp(targetPositions[i], originalPositions[i], easeIn);
                    rects[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(targetRotations[i], 0f, easeIn));
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                rects[i].anchoredPosition = originalPositions[i];
                rects[i].localRotation = Quaternion.identity;
            }
        }

        public void OnCancel()
        {
            if (_isAnimating) return;
            StartCoroutine(CancelSequenceRoutine());
        }

        private IEnumerator CancelSequenceRoutine()
        {
            _isAnimating = true;

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
            
            yield return wait05;

            fadeOverlay.color = Color.black;
            fadeOverlay.gameObject.SetActive(true);

            SelectedTerminal = null;
            IsCanceled = true;
            IsSelectionComplete = true;
            gameObject.SetActive(false);
        }
    }
}