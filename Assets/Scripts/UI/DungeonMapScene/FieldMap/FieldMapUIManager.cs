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
using UnityEditor;
using Helper;

namespace UI
{
    public enum FieldMapState { ListMode, PopupMode, TransitionMode }

    [System.Serializable]
    public class ParallaxLayer
    {
        public RawImage image;
        public float sensitivity;
        public float autoScrollSpeed = 0f;
        
        public float targetScale = 1.2f;
        public float targetYOffset = 15f;
        
        [HideInInspector] public float currentUvOffset = 0f;
        [HideInInspector] public float initialY = 0f; 
        [HideInInspector] public bool isInitialized = false;
    }

    public class FieldMapUIManager : MonoBehaviour
    {
        public bool IsSelectionComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public FieldMapDestData SelectedDestination { get; private set; }

        public float CurrentSimulatedHour { get; private set; } // 가로등 매니저 등에서 현재 애니메이션 시간을 읽을 수 있도록

        public Image backgroundPanel;

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

        [Header("Road Transition UI")]
        public CanvasGroup fadeOverlay;
        public GameObject roadContainer; 
        public Pseudo3DRoad roadScroller; 
        public Pseudo3DRoad skyScroller; 
        public Pseudo3DMinigameManager minigameManager;
        
        public Slider progressBar;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI timeText;
        public float roadTransitionRealTime = 30f;
        
        [Header("Road Curve & Hill Settings")]
        public float maxCurveAmount = 0.5f; 
        public float maxHillAmount = 0.4f; 
        public Vector2 curveDurationRange = new Vector2(0.5f, 1.5f);
        public Vector2 curveDelayRange = new Vector2(1.0f, 2.5f);

        [Header("City Skyline Settings (Parallax)")]
        public float cityVerticalMultiplier = 150f; 
        public ParallaxLayer[] cityLayers;

        [System.Serializable]
        public struct TimePalette
        {
            public Color skyTop;
            public Color skyBottom;
            public Color roadTint;
            public Color cityTint; 
        }

        [Header("Time Palettes (Vivid Cyberpunk Edition)")]
        [Tooltip("인스펙터에서의 컬러 참고용")]
        public TimePalette morningPalette;
        public TimePalette dayPalette;
        public TimePalette eveningPalette;
        public TimePalette nightPalette;

        private FieldMapState _currentState;
        private List<FieldMapSlotUI> _slots = new List<FieldMapSlotUI>();
        private int _currentIndex = 0;
        private bool _isPopupYesSelected = true;
        
        private Sequence _curveSequence; 
        private float _currentCurve = 0f;
        private Sequence _hillSequence;
        private float _currentHill = 0f;
        private bool _skipRequested = false;
        
        private float _transitionProgress = 0f;
        private List<Material> _roadMaterials = new List<Material>();

        public static FieldMapUIManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null) Instance = this;
            ApplyHardcodedPalettes(); // 런타임에서도 혹시 모를 수정을 방지
        }

        private void OnValidate()
        {
            ApplyHardcodedPalettes();
        }
        
        // 하드코딩된 색상 데이터를 덮어씌우는 함수
        private void ApplyHardcodedPalettes()
        {
            morningPalette = new TimePalette { 
                skyTop = new Color(0.3f, 0.0f, 0.8f), skyBottom = new Color(1.0f, 0.3f, 0.0f), 
                roadTint = new Color(1.0f, 0.2f, 0.4f), cityTint = new Color(0.4f, 0.0f, 0.2f) 
            };
            
            dayPalette = new TimePalette { 
                skyTop = new Color(0.0f, 0.2f, 1.0f), skyBottom = new Color(0.0f, 1.0f, 1.0f), 
                roadTint = new Color(0.0f, 0.6f, 1.0f), cityTint = new Color(0.0f, 0.2f, 0.6f) 
            };
            
            eveningPalette = new TimePalette { 
                skyTop = new Color(0.2f, 0.0f, 0.5f), skyBottom = new Color(1.0f, 0.0f, 0.8f), 
                roadTint = new Color(1.0f, 0.0f, 1.0f), cityTint = new Color(0.3f, 0.0f, 0.5f) 
            };
            
            nightPalette = new TimePalette { 
                skyTop = new Color(0.02f, 0.0f, 0.05f), skyBottom = new Color(0.0f, 1.0f, 0.5f), 
                roadTint = new Color(0.4f, 0.0f, 1.0f), cityTint = new Color(0.0f, 0.1f, 0.2f) 
            };
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
            
            if (fadeOverlay != null) { fadeOverlay.alpha = 0f; fadeOverlay.blocksRaycasts = false; } 
            if (roadContainer != null) roadContainer.SetActive(false); 
            
            List<FieldMapDestData> availableDestinations = ManagerRoot.FieldMap.GetAvailableDestinations(sourceMapID); 
            PopulateList(availableDestinations); 
            SetState(FieldMapState.ListMode); 
        }

        private void SetState(FieldMapState state) 
        {
            _currentState = state;

            if (state == FieldMapState.ListMode) {
                listContainer.SetActive(true); 
                popupContainer.SetActive(false); 
                UpdateListHighlight(); 
            } 
            else if (state == FieldMapState.PopupMode) { 
                listContainer.SetActive(true);
                popupContainer.SetActive(true);
                _isPopupYesSelected = true;
                UpdatePopupHighlight(); 
                FieldMapDestData dest = _slots[_currentIndex].Data; 
                string josa = string.IsNullOrEmpty(dest.displayName) ? "" : dest.displayName.GetParticle("으로/로");
                popupMessageText.text = $"<color=#00FFFF>{dest.displayName}</color>{josa} 이동하시겠습니까?\n(소요 시간: {dest.timeHours}시간)"; 
            } 
            else if (state == FieldMapState.TransitionMode) 
            {
                backgroundPanel.color = Color.black;
                listContainer.SetActive(false);
                popupContainer.SetActive(false); 
            } 
        }

        private void Update() 
        {
            if (!gameObject.activeSelf) return;

            if (_currentState == FieldMapState.TransitionMode)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) _skipRequested = true;

                if (roadScroller != null && roadScroller.isMoving && cityLayers != null)
                {
                    for (int i = 0; i < cityLayers.Length; i++)
                    {
                        var layer = cityLayers[i];
                        if (layer.image != null)
                        {
                            float totalScroll = (layer.autoScrollSpeed + (_currentCurve * layer.sensitivity)) * Time.deltaTime;
                            layer.currentUvOffset = (layer.currentUvOffset + totalScroll) % 1f;
                            Rect uvRect = layer.image.uvRect; uvRect.x = layer.currentUvOffset; layer.image.uvRect = uvRect;
                            float currentScale = Mathf.Lerp(1f, layer.targetScale, _transitionProgress);
                            layer.image.rectTransform.localScale = new Vector3(currentScale, currentScale, 1f);
                            float approachYOffset = Mathf.Lerp(0f, layer.targetYOffset, _transitionProgress);
                            Vector2 pos = layer.image.rectTransform.anchoredPosition;
                            pos.y = layer.initialY + approachYOffset - (_currentHill * layer.sensitivity * cityVerticalMultiplier);
                            layer.image.rectTransform.anchoredPosition = pos;
                        }
                    }
                }
                return; 
            }

            if (_currentState == FieldMapState.ListMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || UI.Common.GameInput.GetCancelDown()) OnCancel();
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) TrySelectDestination();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ChangeListSelection(1);
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) ChangeListSelection(-1);
            }
            else if (_currentState == FieldMapState.PopupMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || UI.Common.GameInput.GetCancelDown()) SetState(FieldMapState.ListMode);
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ConfirmPopup();
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                { _isPopupYesSelected = !_isPopupYesSelected; UpdatePopupHighlight(); }
            }
        }

        private void PopulateList(List<FieldMapDestData> dataList) { foreach (var slot in _slots) Destroy(slot.gameObject); _slots.Clear(); for (int i = 0; i < dataList.Count; i++) { GameObject go = Instantiate(slotPrefab, contentPanel); FieldMapSlotUI slot = go.GetComponent<FieldMapSlotUI>(); slot.Initialize(i, dataList[i], this); _slots.Add(slot); } _currentIndex = 0; }
        private void ChangeListSelection(int direction) { if (_slots.Count == 0) return; _currentIndex = Mathf.Clamp(_currentIndex + direction, 0, _slots.Count - 1); UpdateListHighlight(); }
        private void UpdateListHighlight() 
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].SetFocus(i == _currentIndex);

            if (_slots.Count > 0 && _currentIndex >= 0 && _currentIndex < _slots.Count)
            {
                FieldMapDestData dest = _slots[_currentIndex].Data;
                if (dest != null)
                {
                    MapNodeData data = ManagerRoot.FieldMap.GetNodeData(dest.mapID);
                    if (data != null && data.backgroundImage != null)
                    {
                        backgroundPanel.sprite = data.backgroundImage;
                        backgroundPanel.color = Color.dimGray;
                    }
                    else
                    {
                        backgroundPanel.color = Color.black;
                    }
                }
                
            } 
        }
        public void OnSlotHovered(int index) { if (_currentState != FieldMapState.ListMode) return; _currentIndex = index; UpdateListHighlight(); }
        public void OnSlotClicked(int index) { if (_currentState != FieldMapState.ListMode) return; _currentIndex = index; UpdateListHighlight(); TrySelectDestination(); }
        private void TrySelectDestination() { if (_slots.Count > 0 && _slots[_currentIndex].Data != null) { ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor); SetState(FieldMapState.PopupMode); } }
        private void UpdatePopupHighlight() { if (yesButton.targetGraphic != null) yesButton.targetGraphic.color = _isPopupYesSelected ? buttonHighlightColor : buttonNormalColor; if (noButton.targetGraphic != null) noButton.targetGraphic.color = !_isPopupYesSelected ? buttonHighlightColor : buttonNormalColor; }
        public void OnPopupYesClicked() { if (_currentState != FieldMapState.PopupMode) return; _isPopupYesSelected = true; ConfirmPopup(); }
        public void OnPopupNoClicked() { if (_currentState != FieldMapState.PopupMode) return; _isPopupYesSelected = false; ConfirmPopup(); }
        private void ConfirmPopup() { if (_isPopupYesSelected) { SelectedDestination = _slots[_currentIndex].Data; IsCanceled = false; IsSelectionComplete = true; SetState(FieldMapState.TransitionMode); } else { SetState(FieldMapState.ListMode); } }
        public void OnCancel() { SelectedDestination = null; IsCanceled = true; IsSelectionComplete = true; gameObject.SetActive(false); }

        private TimePalette GetPaletteForHour(float hour) { hour = hour % 24f; if (hour >= 6f && hour < 12f) return LerpPalette(morningPalette, dayPalette, (hour - 6f) / 6f); else if (hour >= 12f && hour < 17f) return LerpPalette(dayPalette, eveningPalette, (hour - 12f) / 5f); else if (hour >= 17f && hour < 20f) return LerpPalette(eveningPalette, nightPalette, (hour - 17f) / 3f); else { if (hour >= 20f) return LerpPalette(nightPalette, morningPalette, (hour - 20f) / 10f); else return LerpPalette(nightPalette, morningPalette, (hour + 4f) / 10f); } }
        private TimePalette LerpPalette(TimePalette a, TimePalette b, float t) { return new TimePalette { skyTop = Color.Lerp(a.skyTop, b.skyTop, t), skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t), roadTint = Color.Lerp(a.roadTint, b.roadTint, t), cityTint = Color.Lerp(a.cityTint, b.cityTint, t) }; }

        private void SetMatFloat(string prop, float value) { foreach (var m in _roadMaterials) if (m != null) m.SetFloat(prop, value); }
        private void SetMatColor(string prop, Color value) { foreach (var m in _roadMaterials) if (m != null) m.SetColor(prop, value); }

        private void StartRandomCurveRoutine()
        {
            if (_curveSequence != null && _curveSequence.IsActive()) _curveSequence.Kill();
            _curveSequence = DOTween.Sequence();
            float targetCurve = 0f;
            if (UnityEngine.Random.value > 0.25f) targetCurve = UnityEngine.Random.Range(-maxCurveAmount, maxCurveAmount);
            float duration = UnityEngine.Random.Range(curveDurationRange.x, curveDurationRange.y);
            float delay = UnityEngine.Random.Range(curveDelayRange.x, curveDelayRange.y);
            _curveSequence.Append(DOTween.To(() => _currentCurve, x => { _currentCurve = x; SetMatFloat("_CurveAmount", _currentCurve); }, targetCurve, duration).SetEase(Ease.InOutSine))
            .OnStart(()=>ManagerRoot.Sound.PlaySFX(SfxID.Car_Brake));
            _curveSequence.AppendInterval(delay);
            _curveSequence.OnComplete(() => {StartRandomCurveRoutine(); ManagerRoot.Sound.StopAllSFX();});
        }

        private void StartRandomHillRoutine()
        {
            if (_hillSequence != null && _hillSequence.IsActive()) _hillSequence.Kill();
            _hillSequence = DOTween.Sequence();
            float targetHill = 0f;
            if (UnityEngine.Random.value > 0.35f) targetHill = UnityEngine.Random.Range(-maxHillAmount, maxHillAmount);
            float duration = UnityEngine.Random.Range(curveDurationRange.x, curveDurationRange.y) * 1.2f;
            float delay = UnityEngine.Random.Range(curveDelayRange.x, curveDelayRange.y);
            _hillSequence.Append(DOTween.To(() => _currentHill, x => { _currentHill = x; SetMatFloat("_HillAmount", _currentHill); }, targetHill, duration).SetEase(Ease.InOutSine));
            _hillSequence.AppendInterval(delay);
            _hillSequence.OnComplete(() => StartRandomHillRoutine());
        }

        private void StopMotionRoutines()
        {
            if (_curveSequence != null && _curveSequence.IsActive()) _curveSequence.Kill();
            if (_hillSequence != null && _hillSequence.IsActive()) _hillSequence.Kill();
            DOTween.To(() => _currentCurve, x => { _currentCurve = x; SetMatFloat("_CurveAmount", _currentCurve); }, 0f, 0.5f).SetEase(Ease.OutSine);
            DOTween.To(() => _currentHill, x => { _currentHill = x; SetMatFloat("_HillAmount", _currentHill); }, 0f, 0.5f).SetEase(Ease.OutSine);
        }

        public IEnumerator ExecuteRoadTransitionRoutine(float totalDistance, float totalGameHours, Action onMapLoadAction)
        {
            ManagerRoot.Sound.PlayBGM(BgmID.Drive);
            _skipRequested = false;
            _transitionProgress = 0f; 

            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;

            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();

            float startHour = ((float)ManagerRoot.Time.CurrentSteps / ManagerRoot.Time.stepsPerDay) * 24f;
            float endHour = startHour + totalGameHours;
            
            // 시뮬레이션 시간 초기화
            CurrentSimulatedHour = startHour;

            TimePalette startPalette = GetPaletteForHour(startHour);
            TimePalette targetPalette = GetPaletteForHour(endHour);

            roadContainer.SetActive(true);

            _roadMaterials.Clear();
            if (roadScroller != null) _roadMaterials.Add(roadScroller.GetComponent<RawImage>().material);
            if (skyScroller != null) _roadMaterials.Add(skyScroller.GetComponent<RawImage>().material);

            SetMatColor("_SkyTopColor", startPalette.skyTop);
            SetMatColor("_SkyBottomColor", startPalette.skyBottom);
            SetMatColor("_Color", startPalette.roadTint);
            
            _currentCurve = 0f;
            _currentHill = 0f;
            SetMatFloat("_CurveAmount", 0f);
            SetMatFloat("_HillAmount", 0f);

            if (cityLayers != null)
            {
                for (int i = 0; i < cityLayers.Length; i++)
                {
                    if (cityLayers[i].image != null)
                    {
                        cityLayers[i].currentUvOffset = 0f;
                        if (!cityLayers[i].isInitialized)
                        {
                            cityLayers[i].initialY = cityLayers[i].image.rectTransform.anchoredPosition.y;
                            cityLayers[i].isInitialized = true;
                        }

                        Rect uvRect = cityLayers[i].image.uvRect; uvRect.x = 0f; cityLayers[i].image.uvRect = uvRect;
                        cityLayers[i].image.rectTransform.localScale = Vector3.one;
                        Vector2 pos = cityLayers[i].image.rectTransform.anchoredPosition; 
                        pos.y = cityLayers[i].initialY;
                        cityLayers[i].image.rectTransform.anchoredPosition = pos;
                        cityLayers[i].image.color = startPalette.cityTint;
                    }
                }
            }

            roadScroller.isMoving = true;
            if (skyScroller != null) skyScroller.isMoving = true; 
            
            progressBar.value = 0f;
            distanceText.text = $"0.0 km / {totalDistance:F1} km";
            timeText.text = "ELAPSED TIME 0.0H";

            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();

            StartRandomCurveRoutine();
            StartRandomHillRoutine();

            foreach (var mat in _roadMaterials)
            {
                mat.DOColor(targetPalette.skyTop, "_SkyTopColor", roadTransitionRealTime).SetEase(Ease.InOutSine);
                mat.DOColor(targetPalette.skyBottom, "_SkyBottomColor", roadTransitionRealTime).SetEase(Ease.InOutSine);
                mat.DOColor(targetPalette.roadTint, "_Color", roadTransitionRealTime).SetEase(Ease.InOutSine);
            }

            if (cityLayers != null)
            {
                for (int i = 0; i < cityLayers.Length; i++)
                    if (cityLayers[i].image != null) cityLayers[i].image.DOColor(targetPalette.cityTint, roadTransitionRealTime).SetEase(Ease.InOutSine);
            }

            Tween progressTween = DOTween.To(() => _transitionProgress, x => 
            {
                _transitionProgress = x; 
                progressBar.value = _transitionProgress;
                
                float currentHours = Mathf.Lerp(0, totalGameHours, _transitionProgress);
                // 가상 시뮬레이션 시간 업데이트 (가로등 불빛 제어용)
                CurrentSimulatedHour = startHour + currentHours;
                
                distanceText.text = $"{Mathf.Lerp(0, totalDistance, _transitionProgress):F1} km / {totalDistance:F1} km";
                timeText.text = $"ELAPSED TIME {currentHours:F1}H";
            }, 1f, roadTransitionRealTime).SetEase(Ease.InOutSine);

            bool hasStoppedSpawning = false; 

            while (progressTween.IsActive() && !progressTween.IsComplete())
            {
                if (_skipRequested)
                {
                    // 스킵 시 즉시 스폰 중단
                    if (minigameManager != null) minigameManager.StopSpawning();

                    progressTween.Kill();
                    foreach (var mat in _roadMaterials) mat.DOKill(); 
                    
                    _transitionProgress = 1f; 
                    CurrentSimulatedHour = endHour; // 스킵 시 가상 시간도 즉시 목적지 시간으로 점프
                    
                    if (cityLayers != null)
                    {
                        for (int i = 0; i < cityLayers.Length; i++)
                        {
                            if (cityLayers[i].image != null)
                            {
                                cityLayers[i].image.DOKill();
                                cityLayers[i].image.color = targetPalette.cityTint;
                                cityLayers[i].image.rectTransform.localScale = new Vector3(cityLayers[i].targetScale, cityLayers[i].targetScale, 1f);
                                Vector2 pos = cityLayers[i].image.rectTransform.anchoredPosition;
                                pos.y = cityLayers[i].initialY + cityLayers[i].targetYOffset;
                                cityLayers[i].image.rectTransform.anchoredPosition = pos;
                            }
                        }
                    }
                    
                    progressBar.value = 1f;
                    distanceText.text = $"{totalDistance:F1} km / {totalDistance:F1} km";
                    timeText.text = $"ELAPSED TIME {totalGameHours:F1}H";
                    break; 
                }

                // 남은 시간을 실시간으로 계산하여 스폰 차단
                // _transitionProgress는 0에서 1로 증가하므로 남은 비율을 구해 실제 남은 Time으로 환산
                float remainingTime = roadTransitionRealTime * (1f - _transitionProgress);
                
                // 남은 시간이 5초 이하일 때 한 번만 호출
                if (!hasStoppedSpawning && remainingTime <= 5.0f)
                {
                    if (minigameManager != null) minigameManager.StopSpawning();
                    hasStoppedSpawning = true;
                }

                yield return null; 
            }

            StopMotionRoutines();

            float pauseTimer = 0f;
            while (pauseTimer < 0.5f)
            {
                if (_skipRequested) break;
                pauseTimer += Time.deltaTime;
                yield return null;
            }
            
            ManagerRoot.Sound.StopBGM(true, 0.6f);
            yield return fadeOverlay.DOFade(1f, 0.3f).WaitForCompletion();
            
            roadScroller.isMoving = false;
            if (skyScroller != null) skyScroller.isMoving = false;
            roadContainer.SetActive(false);

            float stepsPerHour = (float)ManagerRoot.Time.stepsPerDay / 24f;
            ManagerRoot.Time.AddStep(Mathf.RoundToInt(totalGameHours * stepsPerHour));
            onMapLoadAction?.Invoke();

            yield return fadeOverlay.DOFade(0f, 0.3f).WaitForCompletion();
            fadeOverlay.blocksRaycasts = false;
        }
    }
}