using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using Controller;
using UnityEngine.EventSystems;
namespace UI.PlayerMenu
{
    public enum ExpansionBoardUIState
    {
        ModuleList,
        BoardPlacement
    }
    public class ExpansionBoardUI : MonoBehaviour
    {
        public PlayerMenuController menuController;
        
        [Header("UI References")]
        public Transform moduleListContent;
        public GameObject moduleItemPrefab;
        public Transform expansionBoardGrid;
        public GameObject gridSlotPrefab;
        public TextMeshProUGUI descriptionText;

        [Header("State & Data")]
        public ExpansionBoardUIState currentState = ExpansionBoardUIState.ModuleList;
        
        private List<GameModuleData> availableModules = new List<GameModuleData>();
        private List<ModuleItemUI> spawnedItems = new List<ModuleItemUI>();
        private Image[,] gridSlots; // 시각적 보드 슬롯 배열
        
        // 포커스 제어용 변수
        private int selectedListIndex = 0;
        private int cursorX = 0;
        private int cursorY = 0;
        private GameModuleData currentlyPlacingModule = null;

        private int currentRotation = 0; // 0 ~ 3

        void Start()
        {
            InitializeBoardUI();
            RefreshModuleList();
            UpdateFocus();
        }

        void Update()
        {
            if (menuController.IsPopupOpen) return;
            if (!menuController.CanProcessInput) return;
            switch (currentState)
            {
                case ExpansionBoardUIState.ModuleList:
                    HandleModuleListInput();
                    break;
                case ExpansionBoardUIState.BoardPlacement:
                    HandleBoardPlacementInput();
                    break;
            }
        }

        // 초기화 및 리스트 갱신
        private void InitializeBoardUI()
        {
            int width = ModuleManager.Instance.gridWidth;
            int height = ModuleManager.Instance.gridHeight;
            gridSlots = new Image[width, height];

            foreach (Transform child in expansionBoardGrid) Destroy(child.gameObject);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GameObject slot = Instantiate(gridSlotPrefab, expansionBoardGrid);
                    gridSlots[x, y] = slot.GetComponent<Image>();
                    gridSlots[x, y].color = Color.black; 

                    // 생성된 슬롯에 x, y 좌표를 기억하는 마우스 이벤트 부여
                    AddGridSlotMouseEvents(slot, x, y);
                }
            }
        }

        private void RefreshModuleList()
        {
            foreach (var item in spawnedItems) Destroy(item.gameObject);
            spawnedItems.Clear();
            availableModules.Clear();

            if (ModuleManager.Instance != null)
                ModuleManager.Instance.SyncexpansionBoardWithMountedModules(); 

            foreach (ModuleFeature feature in ModuleManager.Instance.ownedModules)
            {
                GameModuleData data = ModuleManager.Instance.GetModuleData(feature);
                if (data != null) availableModules.Add(data);
            }

            for (int i = 0; i < availableModules.Count; i++)
            {
                GameModuleData moduleData = availableModules[i];
                GameObject obj = Instantiate(moduleItemPrefab, moduleListContent);
                ModuleItemUI itemUI = obj.GetComponent<ModuleItemUI>();
                
                bool isMounted = ModuleManager.Instance.IsMounted(moduleData.feature);
                itemUI.Setup(moduleData, isMounted);
                spawnedItems.Add(itemUI);

                AddListItemMouseEvents(obj, i);
            }
            DrawBoard(); 
        }

        // 입력 처리 (모듈 리스트 포커스 상태)
        private void HandleModuleListInput()
        {
            if (spawnedItems.Count == 0) return;

            // 방향키 조작
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                selectedListIndex = Mathf.Max(0, selectedListIndex - 1);
                UpdateFocus();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                selectedListIndex = Mathf.Min(spawnedItems.Count - 1, selectedListIndex + 1);
                UpdateFocus();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                GameModuleData selectedModule = availableModules[selectedListIndex];

                if (ModuleManager.Instance.IsMounted(selectedModule.feature))
                {
                    // 이미 설치된 모듈이면 즉시 해제
                    ModuleManager.Instance.UnmountModule(selectedModule.feature);
                    RefreshModuleList(); // 상태 갱신
                    UpdateFocus();
                }
                else
                {
                    // 설치되지 않은 모듈이면 보드 배치 모드로 전환 시도
                    AttemptToMountModule(selectedModule);
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(1))
            {
                if (menuController != null)
                {
                    menuController.CloseMemoryUI();
                }
            }
        }

        private void AddListItemMouseEvents(GameObject go, int index)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            // 마우스가 올라가면 해당 아이템으로 포커스 이동
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => {
                if (currentState == ExpansionBoardUIState.ModuleList && !menuController.IsPopupOpen)
                {
                    selectedListIndex = index;
                    UpdateFocus();
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                }
            });
            trigger.triggers.Add(enter);

            // 마우스 좌클릭 시 설치/해제 (스페이스바와 동일)
            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((data) => {
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;

                if (currentState == ExpansionBoardUIState.ModuleList && !menuController.IsPopupOpen)
                {
                    menuController.ResetInputTimer();
                    GameModuleData selectedModule = availableModules[selectedListIndex];

                    if (ModuleManager.Instance.IsMounted(selectedModule.feature))
                    {
                        ModuleManager.Instance.UnmountModule(selectedModule.feature);
                        RefreshModuleList();
                        UpdateFocus();
                    }
                    else
                    {
                        AttemptToMountModule(selectedModule);
                    }
                }
            });
            trigger.triggers.Add(click);
        }

        // 보드의 각 그리드 슬롯에 마우스 이벤트 추가
        private void AddGridSlotMouseEvents(GameObject go, int x, int y)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            // 배치 모드일 때 마우스가 가리키는 곳으로 블록이 따라옴
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => {
                if (currentState == ExpansionBoardUIState.BoardPlacement && !menuController.IsPopupOpen)
                {
                    cursorX = x;
                    cursorY = y;
                    DrawBoard();
                }
            });
            trigger.triggers.Add(enter);

            // 배치 모드일 때 클릭하면 해당 위치에 설치
            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((data) => {
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;

                if (currentState == ExpansionBoardUIState.BoardPlacement && !menuController.IsPopupOpen)
                {
                    menuController.ResetInputTimer();
                    if (ModuleManager.Instance.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation))
                    {
                        ModuleManager.Instance.MountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
                        currentState = ExpansionBoardUIState.ModuleList; // 설치 후 목록으로 돌아감
                        RefreshModuleList();
                        UpdateFocus();
                        SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                    }
                    else
                    {
                        SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                        // menuController.ShowAlertPopup("여기에 설치할 수 없습니다."); 
                    }
                }
            });
            trigger.triggers.Add(click);
        }

        private void UpdateFocus()
        {
            // 리스트 UI 하이라이트 갱신
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                spawnedItems[i].SetHighlight(i == selectedListIndex);
            }

            // 설명 텍스트 갱신
            if (availableModules.Count > 0)
            {
                descriptionText.text = availableModules[selectedListIndex].description;
            }
        }

        // 입력 처리 (메모리 보드 포커스 상태)
        private void AttemptToMountModule(GameModuleData moduleData)
        {
            if (FindAutoPlaceCoordinate(moduleData, out Vector2Int startPos, out int foundRot))
            {
                currentlyPlacingModule = moduleData;
                cursorX = startPos.x;
                cursorY = startPos.y;
                currentRotation = foundRot; // 찾은 각도로 시작
                currentState = ExpansionBoardUIState.BoardPlacement;
                DrawBoard(); 
            }
            else
            {
                menuController.ShowAlertPopup("설치할 공간이 없습니다.");
            }
        }

        private void HandleBoardPlacementInput()
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(2)) // 마우스 휠 클릭 등
            {
                menuController.ResetInputTimer();
                currentRotation = (currentRotation + 1) % 4; // 90도 회전
                DrawBoard(); // 프리뷰 갱신
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            }

            int prevX = cursorX;
            int prevY = cursorY;

            // 방향키로 커서 이동
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) cursorY--;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) cursorY++;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) cursorX--;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) cursorX++;

            // 커서가 보드를 벗어나지 않도록 제한
            cursorX = Mathf.Clamp(cursorX, 0, ModuleManager.Instance.gridWidth - 1);
            cursorY = Mathf.Clamp(cursorY, 0, ModuleManager.Instance.gridHeight - 1);

            if (prevX != cursorX || prevY != cursorY)
            {
                DrawBoard(); // 커서 이동 시 보드 프리뷰 다시 그리기
            }

            // 설치
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // currentRotation 인자 추가
                if (ModuleManager.Instance.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation))
                {
                    // [수정] currentRotation 인자 추가
                    ModuleManager.Instance.MountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
                    currentState = ExpansionBoardUIState.ModuleList; // 설치 완료 후 리스트로 복귀
                    RefreshModuleList();
                    UpdateFocus();
                }
                else
                {
                    Debug.Log("여기에 설치할 수 없습니다!"); 
                }
            }
            
            // 취소 후 리스트로 복귀
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                currentState = ExpansionBoardUIState.ModuleList;
                DrawBoard(); // 프리뷰 제거
            }
        }

        // 보드 렌더링
        private void DrawBoard()
        {
            // 보드를 모두 검은색으로 초기화
            int width = ModuleManager.Instance.gridWidth;
            int height = ModuleManager.Instance.gridHeight;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    gridSlots[x, y].color = Color.black; 
                }
            }

            // 이미 설치된 모듈들의 블록의 색을 정의된 색으로 바꿈
            foreach (PlacedModuleData placedModule in ModuleManager.Instance.GetMountedModules())
            {
                GameModuleData data = ModuleManager.Instance.GetModuleData(placedModule.feature);
                if (data == null) continue;

                // 설치할 때 저장해둔 회전값을 적용하여 블록을 가져옴
                foreach (Vector2Int offset in data.GetRotatedBlocks(placedModule.rotation))
                {
                    int drawX = placedModule.x + offset.x;
                    int drawY = placedModule.y + offset.y;
                    
                    if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                    {
                        gridSlots[drawX, drawY].color = data.blockColor;
                    }
                }
            }

            // 현재 배치 진행 중인 모듈 표시
            if (currentState == ExpansionBoardUIState.BoardPlacement && currentlyPlacingModule != null)
            {
                bool canPlace = ModuleManager.Instance.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
                foreach (Vector2Int offset in currentlyPlacingModule.GetRotatedBlocks(currentRotation))
                {
                    int drawX = cursorX + offset.x;
                    int drawY = cursorY + offset.y;

                    if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                    {
                        // 설치 가능하면 반투명한 원래 색상, 불가능하면 반투명한 빨간색
                        Color previewColor = canPlace ? currentlyPlacingModule.blockColor : Color.red;
                        previewColor.a = 0.5f; // 반투명
                        
                        // 기존 색상 위에 덧씌움
                        gridSlots[drawX, drawY].color = previewColor; 
                    }
                }
            }
        }

        // 4방향을 모두 탐색하며 최초로 들어맞는 공간과 각도를 찾음
        private bool FindAutoPlaceCoordinate(GameModuleData moduleData, out Vector2Int pos, out int foundRotation)
        {
            pos = Vector2Int.zero;
            foundRotation = 0;

            // 회전 상태 0 -> 1 -> 2 -> 3 순으로 검사
            for (int r = 0; r < 4; r++)
            {
                for (int y = 0; y < ModuleManager.Instance.gridHeight; y++)
                {
                    for (int x = 0; x < ModuleManager.Instance.gridWidth; x++)
                    {
                        if (ModuleManager.Instance.CanMountModule(moduleData.feature, x, y, r))
                        {
                            pos = new Vector2Int(x, y);
                            foundRotation = r;
                            return true; // 맞는 각도와 자리를 찾으면 즉시 반환
                        }
                    }
                }
            }
            return false;
        }

        
    }
    
}