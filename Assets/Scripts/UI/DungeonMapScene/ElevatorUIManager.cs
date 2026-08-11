using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using UnityEngine.EventSystems;
using DG.Tweening;
using Manager;
using System.Collections.Generic;

namespace UI
{
    public class ElevatorUIManager : MonoBehaviour
    {
        public static ElevatorUIManager Instance;

        [Header("UI 연결")]
        public Transform visualContainer;
        public Image elevatorBackgroundImage; 
        public Image elevatorCharacterImage;
        public GameObject buttonPanel;
        public Transform buttonContainer;     
        public GameObject buttonPrefab;
        
        [Header("도어 애니메이션 (RectTransform)")]
        public RectTransform leftDoor;   // Split용 좌측 도어
        public RectTransform rightDoor;  // Split용 우측 도어
        public RectTransform singleDoor; // SlideLeft, SlideUp용 단일 도어
        public RectTransform characterTransform; // 문이 열릴 때 캐릭터도 같이 치우고 싶다면 할당

        // 원래 위치를 기억할 변수들
        private Vector2 _leftDoorOrigin;
        private Vector2 _rightDoorOrigin;
        private Vector2 _singleDoorOrigin;
        private Vector2 _characterOrigin;

        [Header("캐릭터 세팅")]
        public Sprite[] characterImages;

        [Header("애니메이션 설정")]
        public float fadeOutTime = 0.3f;
        public float timePerFloor = 0.5f;     
        public float baseSweepSpeed = 1.5f;   

        public bool IsSelectionComplete { get; private set; }
        public bool IsAnimationFinished { get; private set; }
        public FloorData SelectedFloor { get; private set; }

        private Material _bgSweepMat;
        private Material _chrSweepMat;
        private int _currentFloorNum;
        private CanvasGroup _containerCanvasGroup;
        private GridLayoutGroup _gridLayoutGroup;
        private GameObject _lastSelectedObject;

        // 취소 시 현재 있는 층에서 내리기 위해 현재 층 데이터를 기억할 변수
        private FloorData _currentFloorData;
        private bool _hasCurrentFloor;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);

            // 도어들의 원래 앵커 위치를 저장
            if (leftDoor != null) _leftDoorOrigin = leftDoor.anchoredPosition;
            if (rightDoor != null) _rightDoorOrigin = rightDoor.anchoredPosition;
            if (singleDoor != null) _singleDoorOrigin = singleDoor.anchoredPosition;
            if (characterTransform != null) _characterOrigin = characterTransform.anchoredPosition;

            if (elevatorBackgroundImage != null)
            {
                _bgSweepMat = new Material(elevatorBackgroundImage.material);
                elevatorBackgroundImage.material = _bgSweepMat;
                _bgSweepMat.SetFloat("_ShineSpeed", 0f);
            }

            if (elevatorCharacterImage != null)
            {
                _chrSweepMat = new Material(elevatorCharacterImage.material);
                elevatorCharacterImage.material = _chrSweepMat;
                _chrSweepMat.SetFloat("_ShineSpeed", 0f);
            }

            _containerCanvasGroup = buttonPanel.GetComponent<CanvasGroup>();
            if (_containerCanvasGroup == null)
                _containerCanvasGroup = buttonPanel.AddComponent<CanvasGroup>();

            if (buttonContainer != null)
            {
                _gridLayoutGroup = buttonContainer.GetComponent<GridLayoutGroup>();
                if (_gridLayoutGroup == null)
                    _gridLayoutGroup = buttonContainer.gameObject.AddComponent<GridLayoutGroup>();
            }
        }

        private void Update()
        {
            // 엘리베이터 UI가 켜져 있고 층 선택이 안 끝난 상태일 때만 작동
            if (gameObject.activeSelf && !IsSelectionComplete && EventSystem.current != null)
            {
                // 취소 키를 누르면 현재 층을 선택하여 내림
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
                {
                    if (_hasCurrentFloor)
                    {
                        SelectFloor(_currentFloorData);
                        return;
                    }
                }

                if (EventSystem.current.currentSelectedGameObject != null)
                {
                    // 현재 어떤 버튼이 정상적으로 하이라이트 되어 있다면 그 버튼을 저장
                    _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
                }
                else if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
                {
                    // 만약 허공을 클릭해서 하이라이트가 풀려버렸다면 마우스를 떼는 즉시, 저장한 버튼에 강제로 다시 하이라이트를 넣는다
                    EventSystem.current.SetSelectedGameObject(_lastSelectedObject);
                }
            }
        }

        public void OpenElevator(ElevatorData elevatorData, string currentMapID)
        {
            gameObject.SetActive(true);
            IsSelectionComplete = false;
            IsAnimationFinished = false;

            if (elevatorCharacterImage != null && characterImages != null && characterImages.Length > 0)
            {
                // 동승한 캐릭터의 옆모습을 표시한다
                elevatorCharacterImage.sprite = characterImages[0];
                elevatorCharacterImage.SetNativeSize();
            }

            _bgSweepMat.SetFloat("_ShineSpeed", 0f);
            _chrSweepMat.SetFloat("_ShineSpeed", 0f);

            // UI가 다시 열릴 때 투명도와 상호작용을 강제 복구
            if (_containerCanvasGroup != null)
            {
                _containerCanvasGroup.alpha = 0.8f;
                _containerCanvasGroup.interactable = true;
                _containerCanvasGroup.blocksRaycasts = true;
            }
            
            if (buttonContainer != null) buttonContainer.gameObject.SetActive(true);

            _currentFloorNum = 0;
            _hasCurrentFloor = false;

            foreach (var f in elevatorData.floorData)
            {
                if (f.mapID == currentMapID)
                {
                    _currentFloorNum = f.floorNumber;
                    _currentFloorData = f; // 취소 시 사용할 데이터를 캐싱
                    _hasCurrentFloor = true;
                    break;
                }
            }

            // 전체 층수 계산 시, 0층이 빠지므로 0층을 관통하는 범위라면 전체 칸 수에서 1을 빼줌
            int totalFloors = elevatorData.maxFloor - elevatorData.minFloor + 1;
            if (elevatorData.minFloor <= 0 && elevatorData.maxFloor >= 0)
            {
                totalFloors--;
            }
            
            int columnCount = Mathf.CeilToInt(totalFloors / 6f); // 6개씩 줄을 나눔 

            if (_gridLayoutGroup != null)
            {
                _gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _gridLayoutGroup.constraintCount = columnCount;
            }

            if (buttonContainer != null)
            {
                foreach (Transform child in buttonContainer) Destroy(child.gameObject);

                Button firstValidButton = null; 
                Button currentFloorButton = null; // 현재 층 버튼을 별도로 기억할 변수
                List<Button> spawnedButtons = new List<Button>(); // 생성된 모든 버튼을 담을 리스트

                for (int i = elevatorData.maxFloor; i >= elevatorData.minFloor; i--)
                {
                    if (i == 0) continue; 

                    int floorNum = i; 
                    
                    GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
                    Button btn = btnObj.GetComponent<Button>();
                    TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();

                    bool isFloorExist = false;
                    FloorData matchedFloor = default;

                    foreach (var f in elevatorData.floorData)
                    {
                        if (f.floorNumber == floorNum)
                        {
                            isFloorExist = true;
                            matchedFloor = f;
                            break;
                        }
                    }

                    string floorName = floorNum > 0 ? $"{floorNum}" : $"B{Mathf.Abs(floorNum)}";

                    if (isFloorExist)
                    {
                        txt.text = string.IsNullOrEmpty(matchedFloor.displayName) ? floorName : matchedFloor.displayName;

                        if (floorNum == _currentFloorNum)
                        {
                            btn.interactable = true;
                            btn.onClick.AddListener(() => SelectFloor(matchedFloor));
                            
                            // 현재 층에 해당하는 버튼 컴포넌트를 저장합니다.
                            currentFloorButton = btn;
                        }
                        else
                        {
                            btn.interactable = true;
                            btn.onClick.AddListener(() => SelectFloor(matchedFloor));
                        }

                        if (firstValidButton == null) firstValidButton = btn;
                    }
                    else
                    {
                        txt.text = floorName;
                        btn.interactable = false;
                        txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 0.3f);
                    }

                    spawnedButtons.Add(btn);
                }

                // 버튼 생성이 끝나면 그리드 바깥으로 빠져나가지 못하게 네비게이션을 연결
                SetupExplicitNavigation(spawnedButtons, columnCount);
                
                // 현재 층 버튼이 존재하면 무조건 하이라이트를 줌
                if (currentFloorButton != null)
                {
                    StartCoroutine(SetFirstSelectedButton(currentFloorButton.gameObject));
                }
                // 만약 현재 층 데이터가 예외적으로 없다면 기존처럼 첫 번째 버튼을 선택
                else if (firstValidButton != null)
                {
                    StartCoroutine(SetFirstSelectedButton(firstValidButton.gameObject));
                }
            }
        }

        // 그리드 형태에 맞춰 명시적 네비게이션을 설정하는 메서드
        private void SetupExplicitNavigation(List<Button> buttons, int cols)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                Button btn = buttons[i];
                if (!btn.interactable) continue; // 비활성화된 버튼은 설정 패스

                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.Explicit;

                // 상하는 cols만큼 인덱스 차이가 남
                nav.selectOnUp = FindNextValid(buttons, i, -cols, cols, false);
                nav.selectOnDown = FindNextValid(buttons, i, cols, cols, false);
                
                // 좌우는 1만큼 인덱스 차이가 남
                nav.selectOnLeft = FindNextValid(buttons, i, -1, cols, true);
                nav.selectOnRight = FindNextValid(buttons, i, 1, cols, true);

                btn.navigation = nav;
            }
        }

        // 특정 방향으로 가면서 가장 먼저 만나는 활성화된 버튼을 반환
        private Button FindNextValid(List<Button> list, int startIndex, int step, int cols, bool isHorizontal)
        {
            int curr = startIndex + step;
            while (curr >= 0 && curr < list.Count)
            {
                // 왼쪽 또는 오른쪽 방향일 때 그리드 바깥으로 넘어가면 연결을 멈춤
                if (isHorizontal)
                {
                    if (step == 1 && curr % cols == 0) break; // 오른쪽 끝에서 더 갔을 때
                    if (step == -1 && curr % cols == cols - 1) break; // 왼쪽 끝에서 더 갔을 때
                }

                // 이동한 곳의 버튼이 상호작용 가능하다면 해당 버튼으로 연결
                if (list[curr].interactable) 
                    return list[curr];
                    
                // 비활성화 층이라면 건너뛰고 같은 방향으로 계속 탐색
                curr += step;
            }
            return null; // 연결할 버튼이 없다면 포커스가 이동하지 않고 제자리에 머무름
        }

        // UI 레이아웃 정렬이 끝날 때까지 1프레임 대기 후 포커스를 표시
        private IEnumerator SetFirstSelectedButton(GameObject firstButton)
        {
            yield return null; 
            EventSystem.current.SetSelectedGameObject(null); // 기존 포커스 초기화
            EventSystem.current.SetSelectedGameObject(firstButton); // 새 포커스 지정
        }

        public void SelectFloor(FloorData floor)
        {
            if (IsSelectionComplete) return;
            SelectedFloor = floor;
            IsSelectionComplete = true;

            if (buttonContainer != null) buttonContainer.gameObject.SetActive(false);

            if (_containerCanvasGroup != null)
            {
                _containerCanvasGroup.interactable = false;
                _containerCanvasGroup.blocksRaycasts = false;
            }

            if (singleDoor) singleDoor.gameObject.SetActive(false);
            if (leftDoor) leftDoor.gameObject.SetActive(false);
            if (rightDoor) rightDoor.gameObject.SetActive(false);

            StartCoroutine(ElevatorMovingAnimation(floor.floorNumber));
        }

        private IEnumerator ElevatorMovingAnimation(int targetFloor)
        {
            if (_containerCanvasGroup != null)
            {
                float elapsedFade = 0f;
                while (elapsedFade < fadeOutTime)
                {
                    elapsedFade += Time.deltaTime;
                    _containerCanvasGroup.alpha = 0.8f * (1f - Mathf.Clamp01(elapsedFade / fadeOutTime));
                    yield return null;
                }
                _containerCanvasGroup.alpha = 0f;
            }

            int floorDiff = targetFloor - _currentFloorNum;
            int absDiff = Mathf.Abs(floorDiff);
            
            if (absDiff == 0)
            {
                IsAnimationFinished = true;
                if (elevatorCharacterImage != null && characterImages != null && characterImages.Length > 1)
                {
                    // 동승한 캐릭터의 뒷모습을 표시한다
                    elevatorCharacterImage.sprite = characterImages[1];
                    elevatorCharacterImage.SetNativeSize();
                }
                yield break;
            }
            
            // 오프셋이 더해져도 화면 밖에서 안전하게 시작/종료되도록 범위를 넓힘 (-2.0 ~ 2.0)
            float startLoc = (floorDiff > 0) ? 2.0f : -2.0f;
            float endLoc   = (floorDiff > 0) ? -2.0f : 2.0f;

            // Parallax 처리. 배경과 캐릭터 간의 물리적 위치 오프셋 설정
            float parallaxOffset = (floorDiff > 0) ? 0.4f : -0.4f;

            for (int i = 0; i < absDiff; i++)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.Elevator);
                float elapsed = 0f;
                while (elapsed < timePerFloor)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / timePerFloor;
                    
                    float bgLoc = Mathf.Lerp(startLoc, endLoc, t);
                    float chrLoc = bgLoc + parallaxOffset; 
                    
                    _bgSweepMat.SetFloat("_ShineLocation", bgLoc);
                    _chrSweepMat.SetFloat("_ShineLocation", chrLoc);
                    yield return null; 
                }
            }

            _bgSweepMat.SetFloat("_ShineLocation", -3.0f);
            _chrSweepMat.SetFloat("_ShineLocation", -3.0f);
            
            if (elevatorCharacterImage != null && characterImages != null && characterImages.Length > 1)
            {
                elevatorCharacterImage.sprite = characterImages[1];
                elevatorCharacterImage.SetNativeSize();
            }

            IsAnimationFinished = true;
        }

        // 문 열림 연출 코루틴
        public IEnumerator OpenDoorsRoutine(ElevatorDoorType doorType)
        {
            elevatorBackgroundImage.gameObject.SetActive(false);
            float animDuration = 1.0f; 
            Sequence seq = DOTween.Sequence();
            seq.Join(visualContainer.DOScale(2f, animDuration)).SetEase(Ease.InOutCubic);

            // 열기 전에 현재 사용될 문을 활성화
            if (doorType == ElevatorDoorType.Split)
            {
                if (singleDoor) singleDoor.gameObject.SetActive(false);
                if (leftDoor) leftDoor.gameObject.SetActive(true);
                if (rightDoor) rightDoor.gameObject.SetActive(true);
                
                float moveDist = 1200f; 
                seq.Join(leftDoor.DOAnchorPosX(_leftDoorOrigin.x - moveDist, animDuration).SetEase(Ease.InOutCubic));
                seq.Join(rightDoor.DOAnchorPosX(_rightDoorOrigin.x + moveDist, animDuration).SetEase(Ease.InOutCubic));
            }
            else if (doorType == ElevatorDoorType.SlideLeft)
            {
                if (singleDoor) singleDoor.gameObject.SetActive(true);
                if (leftDoor) leftDoor.gameObject.SetActive(false);
                if (rightDoor) rightDoor.gameObject.SetActive(false);

                float moveDist = 2000f;
                seq.Join(singleDoor.DOAnchorPosX(_singleDoorOrigin.x - moveDist, animDuration).SetEase(Ease.InOutCubic));
            }
            else if (doorType == ElevatorDoorType.SlideUp)
            {
                if (singleDoor) singleDoor.gameObject.SetActive(true);
                if (leftDoor) leftDoor.gameObject.SetActive(false);
                if (rightDoor) rightDoor.gameObject.SetActive(false);

                float moveDist = 1200f; 
                seq.Join(singleDoor.DOAnchorPosY(_singleDoorOrigin.y + moveDist, animDuration).SetEase(Ease.InOutCubic));
            }

            if (characterTransform != null)
            {
                CanvasGroup charGroup = characterTransform.GetComponent<CanvasGroup>();
                if (charGroup != null) seq.Join(charGroup.DOFade(0f, animDuration * 0.8f));
                else seq.Join(characterTransform.DOAnchorPosX(_characterOrigin.x - 500f, animDuration));
            }

            yield return seq.WaitForCompletion();
        }

        // 밖으로 걸어 나가는 줌인만 전담하는 코루틴
        public IEnumerator StepOutZoomRoutine(float duration)
        {
            yield return transform.DOScale(1.2f, duration).SetEase(Ease.InOutCubic).WaitForCompletion();
        }

        // UI를 닫을 때 다음 탑승을 위해 도어 위치를 원상 복구하는 메서드
        public void CloseElevator()
        {
            gameObject.SetActive(false);

            visualContainer.localScale = Vector3.one;
            elevatorBackgroundImage.gameObject.SetActive(true);
            // 도어 위치 및 캐릭터 원상 복구
            if (leftDoor != null)
            {
                leftDoor.anchoredPosition = _leftDoorOrigin;
                leftDoor.gameObject.SetActive(false);
            }
            if (rightDoor != null)
            {
                rightDoor.anchoredPosition = _rightDoorOrigin;
                rightDoor.gameObject.SetActive(false);  
            } 
            if (singleDoor != null)
            {
                singleDoor.anchoredPosition = _singleDoorOrigin;
                singleDoor.gameObject.SetActive(false);
            }
            if (characterTransform != null) 
            {
                characterTransform.anchoredPosition = _characterOrigin;
                CanvasGroup charGroup = characterTransform.GetComponent<CanvasGroup>();
                if (charGroup != null) charGroup.alpha = 1f;
            }
        }
    }
}