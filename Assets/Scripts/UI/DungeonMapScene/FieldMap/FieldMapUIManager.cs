using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Manager;
using Data;
using UnityEngine.EventSystems;

namespace UI
{
    public enum FieldMapState { ListMode, PopupMode, TransitionMode }

    public class FieldMapUIManager : MonoBehaviour
    {
        public bool IsSelectionComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public FieldMapDestData SelectedDestination { get; private set; }

        [Header("List UI")]
        public GameObject listContainer;
        public RectTransform contentPanel; 
        public GameObject slotPrefab;
        
        [Header("Popup UI")]
        public GameObject popupContainer;
        public TextMeshProUGUI popupMessageText;
        public Button yesButton;
        public Button noButton;

        public Color buttonNormalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color buttonHighlightColor = new Color(0.2f, 0.6f, 1f, 1f);
        

        [Header("Road Transition UI (Pseudo 3D)")]
        public CanvasGroup fadeOverlay;
        public GameObject roadContainer; 
        public Pseudo3DRoad roadScroller; 
        public Slider progressBar;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI timeText;
        public float roadTransitionRealTime = 3.0f;

        private FieldMapState _currentState;
        private List<FieldMapSlotUI> _slots = new List<FieldMapSlotUI>();
        private int _currentIndex = 0;
        private bool _isPopupYesSelected = true;
        public static FieldMapUIManager Instance { get; private set; }
        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (yesButton != null)
            {
                yesButton.onClick.RemoveAllListeners();
                yesButton.onClick.AddListener(OnPopupYesClicked);
                
                EventTrigger trigger = yesButton.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((data) => { _isPopupYesSelected = true; UpdatePopupHighlight(); });
                trigger.triggers.Add(entry);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveAllListeners();
                noButton.onClick.AddListener(OnPopupNoClicked);

                EventTrigger trigger = noButton.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((data) => { _isPopupYesSelected = false; UpdatePopupHighlight(); });
                trigger.triggers.Add(entry);
            }
        }

        public void OpenFieldMap(string sourceMapID)
        {
            IsSelectionComplete = false;
            IsCanceled = false;
            SelectedDestination = null;
            
            gameObject.SetActive(true);
            
            // 초기화
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
            }
            if (roadContainer != null) roadContainer.SetActive(false);
            
            List<FieldMapDestData> availableDestinations = ManagerRoot.FieldMap.GetAvailableDestinations(sourceMapID);

            PopulateList(availableDestinations);
            SetState(FieldMapState.ListMode);
        }

        private void SetState(FieldMapState state)
        {
            _currentState = state;
            if (state == FieldMapState.ListMode)
            {
                listContainer.SetActive(true);
                popupContainer.SetActive(false);
                UpdateListHighlight();
            }
            else if (state == FieldMapState.PopupMode)
            {
                listContainer.SetActive(true); 
                popupContainer.SetActive(true);
                _isPopupYesSelected = true;
                UpdatePopupHighlight();

                FieldMapDestData dest = _slots[_currentIndex].Data;
                popupMessageText.text = $"<color=#00FFFF>{dest.displayName}</color>(으)로 이동하시겠습니까?\n(소요 시간: {dest.timeHours}시간)";
            }
            else if (state == FieldMapState.TransitionMode)
            {
                // 연출 중에는 방해되지 않게 기존 창들을 모두 숨김
                listContainer.SetActive(false);
                popupContainer.SetActive(false);
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf || _currentState == FieldMapState.TransitionMode) return;

            if (_currentState == FieldMapState.ListMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
                {
                    OnCancel();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    TrySelectDestination();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ChangeListSelection(1);
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) ChangeListSelection(-1);
            }
            else if (_currentState == FieldMapState.PopupMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1))
                {
                    SetState(FieldMapState.ListMode);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    ConfirmPopup();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || 
                    Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    _isPopupYesSelected = !_isPopupYesSelected;
                    UpdatePopupHighlight();
                }
            }
        }

        private void PopulateList(List<FieldMapDestData> dataList)
        {
            foreach (var slot in _slots) Destroy(slot.gameObject);
            _slots.Clear();

            for (int i = 0; i < dataList.Count; i++)
            {
                GameObject go = Instantiate(slotPrefab, contentPanel);
                FieldMapSlotUI slot = go.GetComponent<FieldMapSlotUI>();
                slot.Initialize(i, dataList[i], this);
                _slots.Add(slot);
            }
            _currentIndex = 0;
        }

        private void ChangeListSelection(int direction)
        {
            if (_slots.Count == 0) return;
            _currentIndex = Mathf.Clamp(_currentIndex + direction, 0, _slots.Count - 1);
            UpdateListHighlight();
        }

        private void UpdateListHighlight()
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].SetFocus(i == _currentIndex);
        }

        public void OnSlotHovered(int index)
        {
            if (_currentState != FieldMapState.ListMode) return;
            _currentIndex = index;
            UpdateListHighlight();
        }

        public void OnSlotClicked(int index)
        {
            if (_currentState != FieldMapState.ListMode) return;
            _currentIndex = index;
            UpdateListHighlight();
            TrySelectDestination();
        }

        private void TrySelectDestination()
        {
            if (_slots.Count > 0 && _slots[_currentIndex].Data != null)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                SetState(FieldMapState.PopupMode);
            }
        }

        private void UpdatePopupHighlight()
        {
            if (yesButton.targetGraphic != null)
            {
                yesButton.targetGraphic.color = _isPopupYesSelected ? buttonHighlightColor : buttonNormalColor;
            }
            if (noButton.targetGraphic != null)
            {
                noButton.targetGraphic.color = !_isPopupYesSelected ? buttonHighlightColor : buttonNormalColor;
            }
        }

        public void OnPopupYesClicked()
        {
            if (_currentState != FieldMapState.PopupMode) return; 

            _isPopupYesSelected = true;
            ConfirmPopup();
        }

        public void OnPopupNoClicked()
        {
            if (_currentState != FieldMapState.PopupMode) return; 

            _isPopupYesSelected = false;
            ConfirmPopup();
        }

        private void ConfirmPopup()
        {
            if (_isPopupYesSelected)
            {
                SelectedDestination = _slots[_currentIndex].Data;
                IsCanceled = false;
                IsSelectionComplete = true;

                // UI 창들만 가리고 gameObject는 살려둠 (RaycastingController가 연출 코루틴을 돌릴 수 있게)
                SetState(FieldMapState.TransitionMode); 
            }
            else
            {
                SetState(FieldMapState.ListMode); 
            }
        }

        public void OnCancel()
        {
            SelectedDestination = null;
            IsCanceled = true;
            IsSelectionComplete = true;
            gameObject.SetActive(false); // 취소 시에는 연출이 필요 없으므로 즉각 비활성화
        }

        // 전용 도로 이동 연출 코루틴
        public IEnumerator ExecuteRoadTransitionRoutine(float totalDistance, float totalGameHours, Action onMapLoadAction)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            // 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            // 도로 연출 세팅
            roadContainer.SetActive(true);
            roadScroller.isMoving = true;
            progressBar.value = 0f;
            distanceText.text = $"0.0 km / {totalDistance:F1} km";
            timeText.text = "경과 시간: 0 시간";

            // 도로 보여주기 (페이드 인)
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();

            Material roadMat = roadScroller.GetComponent<RawImage>().material;
            roadMat.DOFloat(0.4f, "_CurveAmount", 1.0f).SetEase(Ease.InOutSine);
            yield return new WaitForSeconds(1.0f);
            roadMat.DOFloat(0.0f, "_CurveAmount", 1.0f).SetEase(Ease.InOutSine);

            // 진행도 애니메이션
            float progressValue = 0f;
            Tween progressTween = DOTween.To(() => progressValue, x => 
            {
                progressValue = x;
                progressBar.value = progressValue;
                
                float currentDist = Mathf.Lerp(0, totalDistance, progressValue);
                float currentHours = Mathf.Lerp(0, totalGameHours, progressValue);
                
                distanceText.text = $"{currentDist:F1} km / {totalDistance:F1} km";
                timeText.text = $"경과 시간: {Mathf.FloorToInt(currentHours)} 시간";
            }, 1f, roadTransitionRealTime).SetEase(Ease.InOutSine);

            yield return progressTween.WaitForCompletion();
            yield return new WaitForSeconds(0.5f);

            // 도착 후 다시 암전
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            
            roadScroller.isMoving = false;
            roadContainer.SetActive(false);

            // 새로운 지역 맵 로드 
            onMapLoadAction?.Invoke();

            // 던전이 보이도록 다시 페이드 인
            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;

            // 연출이 모두 끝난 뒤 스스로 비활성화
            gameObject.SetActive(false);
        }
    }
}