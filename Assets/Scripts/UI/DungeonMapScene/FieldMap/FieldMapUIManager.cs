using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;

namespace UI
{
    public enum FieldMapState { ListMode, PopupMode }

    public class FieldMapUIManager : MonoBehaviour
    {
        public static FieldMapUIManager Instance { get; private set; }

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

        private FieldMapState _currentState;
        private List<FieldMapSlotUI> _slots = new List<FieldMapSlotUI>();
        private int _currentIndex = 0;
        private bool _isPopupYesSelected = true;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void OpenFieldMap(string sourceMapID)
        {
            IsSelectionComplete = false;
            IsCanceled = false;
            SelectedDestination = null;
            
            gameObject.SetActive(true);
            
            // TODO: 실제 데이터베이스나 매니저에서 sourceMapID에 기반한 이동 가능 지역 목록을 불러옵니다.
            List<FieldMapDestData> availableDestinations = GetMockDestinations(); 

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
                listContainer.SetActive(true); // 리스트는 배경에 둔 채 팝업 띄움
                popupContainer.SetActive(true);
                _isPopupYesSelected = true;
                UpdatePopupHighlight();

                FieldMapDestData dest = _slots[_currentIndex].Data;
                popupMessageText.text = $"<color=#00FFFF>{dest.displayName}</color>(으)로 이동하시겠습니까?\n(소요 시간: {dest.timeHours}시간)";
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

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

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    ChangeListSelection(1);
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    ChangeListSelection(-1);
                }
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

        // ====== 리스트 제어 ======
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
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetFocus(i == _currentIndex);
            }
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

        // ====== 팝업 제어 ======
        private void UpdatePopupHighlight()
        {
            yesButton.transform.localScale = _isPopupYesSelected ? Vector3.one * 1.1f : Vector3.one;
            noButton.transform.localScale = !_isPopupYesSelected ? Vector3.one * 1.1f : Vector3.one;
        }

        public void OnPopupYesClicked()
        {
            _isPopupYesSelected = true;
            ConfirmPopup();
        }

        public void OnPopupNoClicked()
        {
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
                gameObject.SetActive(false); // UI 종료 후 RaycastingController에 턴 넘김
            }
            else
            {
                SetState(FieldMapState.ListMode); // 취소 시 리스트로 복귀
            }
        }

        public void OnCancel()
        {
            SelectedDestination = null;
            IsCanceled = true;
            IsSelectionComplete = true;
            gameObject.SetActive(false);
        }

        // 임시 테스트용 데이터 (실제로는 ManagerRoot에서 불러옵니다)
        private List<FieldMapDestData> GetMockDestinations()
        {
            return new List<FieldMapDestData>
            {
                new FieldMapDestData { mapID = "City_01", displayName = "네오 서울 중심가", distance = 15.5f, timeHours = 2, targetX = 10, targetY = 10, targetDir = Direction.North },
                new FieldMapDestData { mapID = "Wasteland_03", displayName = "버려진 공장 지대", distance = 120.0f, timeHours = 14, targetX = 5, targetY = 5, targetDir = Direction.East }
            };
        }
    }
}