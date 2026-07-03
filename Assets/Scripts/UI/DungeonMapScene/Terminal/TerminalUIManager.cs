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
        public CanvasGroup digitalRain;
        public Image fadeOverlay;

        [Header("UI References (Bottom Grid)")]
        public RectTransform buttonGridContainer;
        public GameObject terminalGridButtonPrefab;
        public GridLayoutGroup gridLayoutGroup;

        [Header("ASCII Warp Effect")]
        public AsciiObjectPool objectPool;
        public RectTransform asciiCanvasRoot;
        public Image characterImage;      // 연출에 사용할 플레이어(또는 전송) 이미지
        public TextAsset characterAscii;  // 이미지에 대응하는 아스키 텍스트 파일
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
            digitalRain.alpha = 0f;
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

            // 애니메이션이 다 끝날 때까지 대기
            yield return wait10;

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

            // 1. 버튼들을 화면 밖으로 무작위로 날려버림
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

            // 버튼이 날아가는 도중에 DigitalRain 페이드 인
            fadeOverlay.DOFade(1f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 0f);});
            digitalRain.DOFade(1f, 0.5f).SetDelay(0.2f);
            
            // 비가 내리기 시작할 때까지 잠시 대기
            yield return wait10;

            // 플레이어 전송 연출 시작
            if (characterImage != null && characterAscii != null && objectPool != null)
            {
                // 플레이어 원본 이미지 페이드 인
                characterImage.gameObject.SetActive(true);
                yield return StartCoroutine(FadeSpriteAlpha(characterImage, 0f, 1f, 0.5f));

                // 아스키 아트 노드 생성 (투명상태)
                DrawAscii(characterAscii, 0f);
                yield return wait05;

                digitalRain.DOFade(0f, 0.5f);

                // 원본 이미지 -> 아스키 아트로 분해
                StartCoroutine(FadeSpriteAlpha(characterImage, 1f, 0f, 1.0f));
                yield return StartCoroutine(RevealAsciiRandomly(1.0f));

                yield return wait05;

                // 아스키 아트 방사형 팽창 및 복귀
                // 폭발(0.6초), 대기(0.5초), 복귀(0.5초), 크기 팽창(15배), 난수 파편화(30f)
                yield return StartCoroutine(ExplodeAndReturnAsciiRoutine(0.6f, 0.5f, 0.5f, 15f, 30f));

                // 데이터 전송 중인 느낌을 주기 위해 짧은 대기
                yield return wait05;

                digitalRain.DOFade(1f, 1f);

                // 아스키 아트 -> 원본 이미지로 재결합
                StartCoroutine(FadeSpriteAlpha(characterImage, 0f, 1f, 1.0f));
                yield return StartCoroutine(DissolveAsciiRandomly(1.0f));
                
                // 다 쓴 노드 반환 및 초기화
                objectPool.ReturnAllObjects(_activeAsciiNodes);
                _activeAsciiNodes.Clear();
                
                yield return wait05;

                fadeOverlay.DOFade(0f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 1f);});
                digitalRain.DOFade(0f, 0.5f);

                // 원본 이미지 페이드 아웃
                yield return StartCoroutine(FadeSpriteAlpha(characterImage, 1f, 0f, 0.5f));

                characterImage.gameObject.SetActive(false);
            }
            else
            {
                fadeOverlay.DOFade(0f, 0.5f).OnStart(()=>{fadeOverlay.color = new Color(0f, 0f, 0f, 1f);});
                digitalRain.DOFade(0f, 0.5f);
                // 아스키 세팅이 안 되어있을 경우를 대비한 안전 장치
                yield return wait10;
            }
    
            yield return wait10;

            // 선택 완료 처리 후 컨트롤러에 턴을 넘김
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

        private IEnumerator FadeSpriteAlpha(Image img, float startAlpha, float endAlpha, float duration)
        {
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                img.color = new Color(1, 1, 1, Mathf.Lerp(startAlpha, endAlpha, time / duration));
                yield return null;
            }
            img.color = new Color(1, 1, 1, endAlpha);
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

                // 중심점(0,0)을 기준으로 바깥쪽으로 밀어내는 벡터
                Vector2 expandVec = originalPositions[i];
                
                // 완벽한 정중앙(0,0)에 있는 문자는 제자리에 멈추지 않도록 랜덤 방향 지정
                if (expandVec == Vector2.zero) expandVec = Random.insideUnitCircle.normalized * 10f;

                // 팽창 벡터 + 흩뿌려지는 랜덤 파편화(Jitter) 효과 추가
                Vector2 randomJitter = Random.insideUnitCircle * Random.Range(0f, jitterAmount);
                targetPositions[i] = (expandVec * expandMultiplier) + randomJitter;

                // 글자들이 날아갈 때 회전할 각도 (±90도 이내)
                targetRotations[i] = Random.Range(-90f, 90f);
            }

            // 방사형 팽창 폭발 (Cubic Ease Out: 터질 때 빠르고 끝에서 느려짐)
            float elapsed = 0f;
            while (elapsed < explodeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / explodeTime;
                float easeOut = 1f - Mathf.Pow(1f - t, 3f); 

                for (int i = 0; i < count; i++)
                {
                    rects[i].anchoredPosition = Vector2.Lerp(originalPositions[i], targetPositions[i], easeOut);
                    // Z축으로 글자를 회전시켜 데이터가 깨지는 듯한 느낌 부여
                    rects[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, targetRotations[i], easeOut));
                }
                yield return null;
            }

            // 최대 팽창 상태(공중에 멈춘 파편들)에서 대기
            yield return new WaitForSeconds(waitTime);

            // 원래 좌표와 회전으로 블랙홀처럼 빨려 들어감 (Cubic Ease In: 점점 가속)
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

            // 안전장치: 오차를 없애고 원래 위치/각도로 완벽한 초기화 보장
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
            yield return wait05;

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