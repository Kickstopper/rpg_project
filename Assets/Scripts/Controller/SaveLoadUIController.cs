using UnityEngine;
using System.Collections.Generic;
using Manager;
using TMPro;
using UI;
using Data;

namespace Controller
{
    public class SaveLoadUIController : MonoBehaviour
    {
        [Header("Settings")]
        public GameObject slotPrefab;
        public Transform contentContainer; // ScrollView의 Content
        public int maxSlots = 3; // 슬롯 개수

        [Header("UI Header")]
        public TextMeshProUGUI titleText; // "SAVE GAME" or "LOAD GAME"

        private bool isSaveMode = true;
        private List<SaveSlotUI> slots = new List<SaveSlotUI>();
        
        private int currentFocusIndex = 0; // 현재 포커스된 슬롯 인덱스
        private float inputCooldown = 0f; // 입력 쿨타임 (너무 빠른 입력 방지, 필요 시 사용)

        void Awake()
        {
            // 슬롯 미리 생성
            for (int i = 0; i < maxSlots; i++)
            {
                GameObject go = Instantiate(slotPrefab, contentContainer);
                SaveSlotUI slotUI = go.GetComponent<SaveSlotUI>();
                slotUI.Initialize(i, this);
                slots.Add(slotUI);
            }
        }

        public void Open(bool isSave)
        {
            gameObject.SetActive(true);
            currentFocusIndex = 0; // 항상 0번부터 시작하거나, 마지막 기억한 위치 사용
            
            isSaveMode = isSave;
            titleText.text = isSave ? "SAVE GAME" : "LOAD GAME";
            
            RefreshAllSlots();    // 데이터 갱신
            UpdateVisualFocus();  // 포커스 표시 갱신
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void RefreshAllSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                // SaveManager에게 해당 슬롯의 헤더 정보 요청
                var data = ManagerRoot.Save.GetSaveDataHeader(i);
                slots[i].SetData(data);
            }
        }

        // 슬롯 버튼이 클릭되었을 때 호출됨
        public void OnSlotSelected(int index, bool hasData)
        {
            if (isSaveMode)
            {
                // [저장 모드]
                // 데이터가 있으면 덮어쓰기 경고 팝업을 띄우는 것이 좋음 (여기선 생략하고 즉시 저장)
                ManagerRoot.Save.SaveGame(index);
                
                // 저장 후 UI 갱신 (시간 등 업데이트)
                RefreshAllSlots();
                Debug.Log($"{index}번 슬롯에 저장했습니다.");
            }
            else
            {
                // [로드 모드]
                if (hasData)
                {
                    ManagerRoot.Save.LoadGame(index);
                    // 로드하면 씬이 바뀌므로 Close() 호출 불필요 (자동 파괴됨)
                    // 만약 DontDestroyOnLoad UI라면 Close() 호출
                }
                else
                {
                    // 빈 슬롯 로드 시도
                    Debug.Log("빈 슬롯입니다.");
                }
            }
        }

        void Update()
        {
            // 창이 꺼져있으면 입력 무시 (혹시 모를 방어 코드)
            if (!gameObject.activeSelf) return;

            HandleInput();
        }

        void HandleInput()
        {
            if (inputCooldown > 0)
            {
                inputCooldown -= Time.deltaTime;
                return;
            }

            // 이동 (좌우 화살표)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                ChangeFocus(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                ChangeFocus(1);
            }

            // 선택 (Space / Enter / Z)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                SelectCurrentSlot();
            }

            // 취소 (Esc / Shift / X)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || UI.Common.GameInput.GetCancelDown())
            {
                OnCancel();
            }
        }

        void ChangeFocus(int direction)
        {
            int nextIndex = currentFocusIndex + direction;

            if (nextIndex >= 0 && nextIndex < slots.Count)
            {
                currentFocusIndex = nextIndex;
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
                UpdateVisualFocus();
            }
        }

        void SelectCurrentSlot()
        {
            // 현재 포커스된 슬롯의 데이터 유무 확인
            // (SaveSlotUI가 데이터를 들고 있지 않다면, 매니저에서 다시 확인)
            var header = ManagerRoot.Save.GetSaveDataHeader(currentFocusIndex);
            bool hasData = (header != null);

            // 기존 OnSlotSelected 함수 재활용
            OnSlotSelected(currentFocusIndex, hasData);
        }

        void OnCancel()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel); // 취소 효과음
            Close();
        }


        // 포커스 시각 효과 갱신
        void UpdateVisualFocus()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                // 내가 현재 인덱스면 true, 아니면 false
                slots[i].SetFocus(i == currentFocusIndex);
            }
        }
    }
}