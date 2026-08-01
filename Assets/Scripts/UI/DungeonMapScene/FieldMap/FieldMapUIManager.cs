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

    [System.Serializable]
    public class ParallaxLayer
    {
        [Tooltip("Wrap Mode가 Repeat인 RawImage를 연결하세요.")]
        public RawImage image;
        [Tooltip("커브 시 스크롤되는 감도 (먼 배경일수록 작게, 가까울수록 크게 설정)")]
        public float sensitivity;
        
        [HideInInspector] 
        public float currentUvOffset = 0f;
    }

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
        public float roadTransitionRealTime = 10.0f;
        
        [Header("Road Curve Settings")]
        private float maxCurveAmount = 1f; 
        private Vector2 curveDurationRange = new Vector2(2f, 2.5f);
        private Vector2 curveDelayRange = new Vector2(4f, 7f);

        [Header("City Skyline Settings (Parallax)")]
        public ParallaxLayer[] cityLayers;

        [System.Serializable]
        public struct TimePalette
        {
            public Color skyTop;
            public Color skyBottom;
            public Color roadTint;
            // 도시 배경(Parallax 이미지)에 적용할 색상 추가
            public Color cityTint; 
        }

        [Header("Time Palettes (4등분)")]
        // 각 시간대별 cityTint 기본값 추가
        public TimePalette morningPalette = new TimePalette { 
            skyTop = new Color(0.4f, 0.6f, 0.8f), skyBottom = new Color(1f, 0.9f, 0.7f), 
            roadTint = new Color(1f, 0.9f, 0.8f), cityTint = new Color(0.9f, 0.8f, 0.7f) };
        public TimePalette dayPalette = new TimePalette { 
            skyTop = new Color(0.1f, 0.4f, 0.8f), skyBottom = new Color(0.6f, 0.8f, 0.9f), 
            roadTint = Color.white, cityTint = Color.white };
        public TimePalette eveningPalette = new TimePalette { 
            skyTop = new Color(0.2f, 0.1f, 0.4f), skyBottom = new Color(0.8f, 0.3f, 0.1f), 
            roadTint = new Color(0.8f, 0.5f, 0.5f), cityTint = new Color(0.7f, 0.5f, 0.4f) };
        public TimePalette nightPalette = new TimePalette { 
            skyTop = new Color(0.05f, 0.05f, 0.1f), skyBottom = new Color(0.1f, 0.1f, 0.3f), 
            roadTint = new Color(0.3f, 0.3f, 0.5f), cityTint = new Color(0.2f, 0.2f, 0.3f) };

        private FieldMapState _currentState;
        private List<FieldMapSlotUI> _slots = new List<FieldMapSlotUI>();
        private int _currentIndex = 0;
        private bool _isPopupYesSelected = true;
        
        private Sequence _curveSequence; 
        private float _currentCurve = 0f;
        private bool _skipRequested = false;

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
                listContainer.SetActive(false);
                popupContainer.SetActive(false);
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            if (_currentState == FieldMapState.TransitionMode)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    _skipRequested = true;
                }

                if (roadScroller != null && roadScroller.isMoving && cityLayers != null)
                {
                    for (int i = 0; i < cityLayers.Length; i++)
                    {
                        var layer = cityLayers[i];
                        if (layer.image != null)
                        {
                            layer.currentUvOffset = (layer.currentUvOffset + _currentCurve * layer.sensitivity * Time.deltaTime) % 1f;
                            
                            Rect uvRect = layer.image.uvRect;
                            uvRect.x = layer.currentUvOffset;
                            layer.image.uvRect = uvRect;
                        }
                    }
                }
                return; 
            }

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
            gameObject.SetActive(false); 
        }

        private TimePalette GetPaletteForHour(float hour)
        {
            hour = hour % 24f; 

            if (hour >= 6f && hour < 12f)
            {
                float t = (hour - 6f) / (12f - 6f);
                return LerpPalette(morningPalette, dayPalette, t);
            }
            else if (hour >= 12f && hour < 17f)
            {
                float t = (hour - 12f) / (17f - 12f);
                return LerpPalette(dayPalette, eveningPalette, t);
            }
            else if (hour >= 17f && hour < 20f)
            {
                float t = (hour - 17f) / (20f - 17f);
                return LerpPalette(eveningPalette, nightPalette, t);
            }
            else 
            {
                if (hour >= 20f)
                {
                    float t = (hour - 20f) / 10f; 
                    return LerpPalette(nightPalette, morningPalette, t);
                }
                else
                {
                    float t = (hour + 4f) / 10f; 
                    return LerpPalette(nightPalette, morningPalette, t);
                }
            }
        }

        private TimePalette LerpPalette(TimePalette a, TimePalette b, float t)
        {
            return new TimePalette
            {
                skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
                skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t),
                roadTint = Color.Lerp(a.roadTint, b.roadTint, t),
                cityTint = Color.Lerp(a.cityTint, b.cityTint, t)
            };
        }

        private void StartRandomCurveRoutine(Material roadMat)
        {
            if (_curveSequence != null && _curveSequence.IsActive())
            {
                _curveSequence.Kill();
            }

            _curveSequence = DOTween.Sequence();

            float targetCurve = 0f;
            if (UnityEngine.Random.value > 0.25f)
            {
                targetCurve = UnityEngine.Random.Range(-maxCurveAmount, maxCurveAmount);
            }
            
            float duration = UnityEngine.Random.Range(curveDurationRange.x, curveDurationRange.y);
            float delay = UnityEngine.Random.Range(curveDelayRange.x, curveDelayRange.y);

            _curveSequence.Append(DOTween.To(() => _currentCurve, x => 
            {
                _currentCurve = x;
                roadMat.SetFloat("_CurveAmount", _currentCurve);
            }, targetCurve, duration).SetEase(Ease.InOutSine));
            
            _curveSequence.AppendInterval(delay);

            _curveSequence.OnComplete(() => StartRandomCurveRoutine(roadMat));
        }

        private void StopCurveRoutine(Material roadMat, float duration)
        {
            if (_curveSequence != null && _curveSequence.IsActive())
            {
                _curveSequence.Kill();
            }

            DOTween.To(() => _currentCurve, x => 
            {
                _currentCurve = x;
                roadMat.SetFloat("_CurveAmount", _currentCurve);
            }, 0f, duration).SetEase(Ease.OutSine);
        }

        public IEnumerator ExecuteRoadTransitionRoutine(float totalDistance, float totalGameHours, Action onMapLoadAction)
        {
            _skipRequested = false;

            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            float startHour = ((float)ManagerRoot.Time.CurrentSteps / ManagerRoot.Time.stepsPerDay) * 24f;
            float endHour = startHour + totalGameHours;

            TimePalette startPalette = GetPaletteForHour(startHour);
            TimePalette targetPalette = GetPaletteForHour(endHour);

            roadContainer.SetActive(true);
            Material roadMat = roadScroller.GetComponent<RawImage>().material;

            roadMat.SetColor("_SkyTopColor", startPalette.skyTop);
            roadMat.SetColor("_SkyBottomColor", startPalette.skyBottom);
            roadMat.SetColor("_Color", startPalette.roadTint);
            
            _currentCurve = 0f;
            roadMat.SetFloat("_CurveAmount", 0f);

            // Parallax 도시 이미지의 위치 초기화 및 현재 출발 시간의 색상(cityTint) 적용
            if (cityLayers != null)
            {
                for (int i = 0; i < cityLayers.Length; i++)
                {
                    if (cityLayers[i].image != null)
                    {
                        cityLayers[i].currentUvOffset = 0f;
                        Rect uvRect = cityLayers[i].image.uvRect;
                        uvRect.x = 0f;
                        cityLayers[i].image.uvRect = uvRect;
                        
                        // 현재 출발 시간대의 도시 색상 강제 초기화
                        cityLayers[i].image.color = startPalette.cityTint;
                    }
                }
            }

            roadScroller.isMoving = true;
            progressBar.value = 0f;
            distanceText.text = $"0.0 km / {totalDistance:F1} km";
            timeText.text = "Elapsed Time: 0.0 hour";

            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();

            StartRandomCurveRoutine(roadMat);

            roadMat.DOColor(targetPalette.skyTop, "_SkyTopColor", roadTransitionRealTime).SetEase(Ease.InOutSine);
            roadMat.DOColor(targetPalette.skyBottom, "_SkyBottomColor", roadTransitionRealTime).SetEase(Ease.InOutSine);
            roadMat.DOColor(targetPalette.roadTint, "_Color", roadTransitionRealTime).SetEase(Ease.InOutSine);

            // 3장의 도시 배경 역시 도착 시간에 맞춰 색상이 서서히 바뀌도록 애니메이션 추가
            if (cityLayers != null)
            {
                for (int i = 0; i < cityLayers.Length; i++)
                {
                    if (cityLayers[i].image != null)
                    {
                        cityLayers[i].image.DOColor(targetPalette.cityTint, roadTransitionRealTime).SetEase(Ease.InOutSine);
                    }
                }
            }

            float progressValue = 0f;
            Tween progressTween = DOTween.To(() => progressValue, x => 
            {
                progressValue = x;
                progressBar.value = progressValue;
                
                float currentDist = Mathf.Lerp(0, totalDistance, progressValue);
                float currentHours = Mathf.Lerp(0, totalGameHours, progressValue);
                
                distanceText.text = $"{currentDist:F1} km / {totalDistance:F1} km";
                timeText.text = $"Elapsed Time: {currentHours:F1} hour";
            }, 1f, roadTransitionRealTime).SetEase(Ease.InOutSine);

            while (progressTween.IsActive() && !progressTween.IsComplete())
            {
                if (_skipRequested)
                {
                    progressTween.Kill();
                    roadMat.DOKill(); 
                    
                    // 스킵 시 도시 이미지의 애니메이션도 정지하고 도착 시간의 색상으로 갱신
                    if (cityLayers != null)
                    {
                        for (int i = 0; i < cityLayers.Length; i++)
                        {
                            if (cityLayers[i].image != null)
                            {
                                cityLayers[i].image.DOKill();
                                cityLayers[i].image.color = targetPalette.cityTint;
                            }
                        }
                    }
                    
                    progressBar.value = 1f;
                    distanceText.text = $"{totalDistance:F1} km / {totalDistance:F1} km";
                    timeText.text = $"Elapsed Time: {totalGameHours:F1} hour";
                    break; 
                }
                yield return null; 
            }

            float stopDuration = 3f;
            StopCurveRoutine(roadMat, stopDuration);

            float pauseTimer = 0f;
            while (pauseTimer < stopDuration)
            {
                if (_skipRequested) break;
                pauseTimer += Time.deltaTime;
                yield return null;
            }

            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            
            roadScroller.isMoving = false;
            roadContainer.SetActive(false);

            float stepsPerHour = (float)ManagerRoot.Time.stepsPerDay / 24f;
            int stepsToAdvance = Mathf.RoundToInt(totalGameHours * stepsPerHour);
            ManagerRoot.Time.AddStep(stepsToAdvance);

            onMapLoadAction?.Invoke();

            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }
    }
}