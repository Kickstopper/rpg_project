using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using UnityEngine.EventSystems;

namespace UI
{
    public class ElevatorUIManager : MonoBehaviour
    {
        public static ElevatorUIManager Instance;

        [Header("UI 연결")]
        public Image elevatorBackgroundImage; 
        public Image elevatorCharacterImage;
        public GameObject buttonPanel;
        public Transform buttonContainer;     
        public GameObject buttonPrefab;       

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
        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);

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
                if (EventSystem.current.currentSelectedGameObject != null)
                {
                    // 현재 어떤 버튼이 정상적으로 하이라이트 되어 있다면 그 버튼을 저장
                    _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
                }
                else if (_lastSelectedObject != null && _lastSelectedObject.activeInHierarchy)
                {
                    // 만약 허공을 클릭해서 하이라이트가 풀려버렸다면 마우스를 떼는 즉시, 저장한 버튼에 강제로 다시 하이라이트를 넎는다
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
            foreach (var f in elevatorData.floorData)
            {
                if (f.mapID == currentMapID)
                {
                    _currentFloorNum = f.floorNumber;
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

                    string floorName = floorNum > 0 ? $"{floorNum}F" : $"B{Mathf.Abs(floorNum)}";

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

                        if (firstValidButton == null) 
                        {
                            firstValidButton = btn;
                        }
                    }
                    else
                    {
                        txt.text = floorName;
                        btn.interactable = false;
                        txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 0.3f);
                    }
                }
                
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

            // Parallax 처리. 배경과 캐릭터 간의 물리적 위치 오프셋 설정 (추천값: 0.3 ~ 0.5)
            float parallaxOffset = (floorDiff > 0) ? 0.4f : -0.4f;

            // SoundManager.Instance.PlaySFX(SfxID.Elevator_Move);

            for (int i = 0; i < absDiff; i++)
            {
                float elapsed = 0f;
                while (elapsed < timePerFloor)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / timePerFloor;
                    
                    // 배경은 정상적으로 끝에서 끝으로 이동
                    float bgLoc = Mathf.Lerp(startLoc, endLoc, t);
                    
                    // 캐릭터는 배경 위치에 오프셋을 더해 약간 뒤쳐져서 따라오게 함
                    float chrLoc = bgLoc + parallaxOffset; 
                    
                    _bgSweepMat.SetFloat("_ShineLocation", bgLoc);
                    _chrSweepMat.SetFloat("_ShineLocation", chrLoc);
                    
                    yield return null; 
                }
            }

            // 연출이 끝나면 두 빛 모두 화면 밖(-3.0)으로 완전히 치운다.
            _bgSweepMat.SetFloat("_ShineLocation", -3.0f);
            _chrSweepMat.SetFloat("_ShineLocation", -3.0f);
            
            // SoundManager.Instance.PlaySFX(SfxID.Elevator_Arrive);

            if (elevatorCharacterImage != null && characterImages != null && characterImages.Length > 1)
            {
                // 동승한 캐릭터의 뒷모습을 표시한다
                elevatorCharacterImage.sprite = characterImages[1];
                elevatorCharacterImage.SetNativeSize();
            }

            if (_containerCanvasGroup != null)
            {
                float elapsedFade = 0f;
                while (elapsedFade < fadeOutTime)
                {
                    elapsedFade += Time.deltaTime;
                    _containerCanvasGroup.alpha = Mathf.Clamp01(elapsedFade / fadeOutTime);
                    yield return null;
                }
                _containerCanvasGroup.alpha = 1f;
            }

            IsAnimationFinished = true;
        }

        public void CloseElevator()
        {
            gameObject.SetActive(false);
        }
    }
}
