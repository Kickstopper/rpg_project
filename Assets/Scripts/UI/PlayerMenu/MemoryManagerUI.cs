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
    public enum MemoryUIState
    {
        AppList,
        BoardPlacement
    }
    public class MemoryManagerUI : MonoBehaviour
    {
        public PlayerMenuController menuController;
        
        [Header("UI References")]
        public Transform appListContent;
        public GameObject appItemPrefab;
        public Transform memoryBoardGrid;
        public GameObject gridSlotPrefab;
        public TextMeshProUGUI descriptionText;

        [Header("State & Data")]
        public MemoryUIState currentState = MemoryUIState.AppList;
        
        private List<GameAppData> availableApps = new List<GameAppData>();
        private List<AppItemUI> spawnedItems = new List<AppItemUI>();
        private Image[,] gridSlots; // 시각적 보드 슬롯 배열
        
        // 포커스 제어용 변수
        private int selectedListIndex = 0;
        private int cursorX = 0;
        private int cursorY = 0;
        private GameAppData currentlyPlacingApp = null;

        void Start()
        {
            InitializeBoardUI();
            RefreshAppList();
            UpdateFocus();
        }

        void Update()
        {
            if (menuController.IsPopupOpen) return;
            if (!menuController.CanProcessInput) return;
            switch (currentState)
            {
                case MemoryUIState.AppList:
                    HandleAppListInput();
                    break;
                case MemoryUIState.BoardPlacement:
                    HandleBoardPlacementInput();
                    break;
            }
        }

        // 초기화 및 리스트 갱신
        private void InitializeBoardUI()
        {
            int width = AppManager.Instance.gridWidth;
            int height = AppManager.Instance.gridHeight;
            gridSlots = new Image[width, height];

            foreach (Transform child in memoryBoardGrid) Destroy(child.gameObject);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GameObject slot = Instantiate(gridSlotPrefab, memoryBoardGrid);
                    gridSlots[x, y] = slot.GetComponent<Image>();
                    gridSlots[x, y].color = Color.black; 

                    // 생성된 슬롯에 x, y 좌표를 기억하는 마우스 이벤트 부여
                    AddGridSlotMouseEvents(slot, x, y);
                }
            }
        }

        private void RefreshAppList()
        {
            foreach (var item in spawnedItems) Destroy(item.gameObject);
            spawnedItems.Clear();
            availableApps.Clear();

            if (AppManager.Instance != null)
                AppManager.Instance.SyncMemoryBoardWithInstalledApps(); 

            foreach (AppFeature feature in AppManager.Instance.ownedFeatures)
            {
                GameAppData data = AppManager.Instance.GetAppData(feature);
                if (data != null) availableApps.Add(data);
            }

            for (int i = 0; i < availableApps.Count; i++)
            {
                GameAppData appData = availableApps[i];
                GameObject obj = Instantiate(appItemPrefab, appListContent);
                AppItemUI itemUI = obj.GetComponent<AppItemUI>();
                bool isInstalled = AppManager.Instance.IsInstalled(appData.feature);
                
                itemUI.Setup(appData, isInstalled);
                spawnedItems.Add(itemUI);

                // 생성된 리스트 아이템에 인덱스를 기억하는 마우스 이벤트 부여
                AddListItemMouseEvents(obj, i);
            }
            
            DrawBoard(); 
        }

        // 입력 처리 (앱 리스트 포커스 상태)
        private void HandleAppListInput()
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
                GameAppData selectedApp = availableApps[selectedListIndex];

                if (AppManager.Instance.IsInstalled(selectedApp.feature))
                {
                    // 이미 설치된 앱이면 즉시 해제
                    AppManager.Instance.Uninstall(selectedApp.feature);
                    RefreshAppList(); // 상태 갱신
                    UpdateFocus();
                }
                else
                {
                    // 설치되지 않은 앱이면 보드 배치 모드로 전환 시도
                    AttemptToPlaceApp(selectedApp);
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

            // Hover: 마우스가 올라가면 해당 아이템으로 포커스 이동
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => {
                if (currentState == MemoryUIState.AppList && !menuController.IsPopupOpen)
                {
                    selectedListIndex = index;
                    UpdateFocus();
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                }
            });
            trigger.triggers.Add(enter);

            // Click: 마우스 좌클릭 시 설치/해제 (스페이스바와 동일 역할)
            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((data) => {
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;

                if (currentState == MemoryUIState.AppList && !menuController.IsPopupOpen)
                {
                    menuController.ResetInputTimer();
                    GameAppData selectedApp = availableApps[selectedListIndex];

                    if (AppManager.Instance.IsInstalled(selectedApp.feature))
                    {
                        AppManager.Instance.Uninstall(selectedApp.feature);
                        RefreshAppList();
                        UpdateFocus();
                    }
                    else
                    {
                        AttemptToPlaceApp(selectedApp);
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

            // Hover: 배치 모드일 때 마우스가 가리키는 곳으로 블록이 따라옴
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => {
                if (currentState == MemoryUIState.BoardPlacement && !menuController.IsPopupOpen)
                {
                    cursorX = x;
                    cursorY = y;
                    DrawBoard();
                }
            });
            trigger.triggers.Add(enter);

            // Click: 배치 모드일 때 클릭하면 해당 위치에 설치
            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((data) => {
                PointerEventData pointerData = data as PointerEventData;
                if (pointerData != null && pointerData.button != PointerEventData.InputButton.Left) return;

                if (currentState == MemoryUIState.BoardPlacement && !menuController.IsPopupOpen)
                {
                    menuController.ResetInputTimer();
                    if (AppManager.Instance.CanPlaceApp(currentlyPlacingApp.feature, cursorX, cursorY))
                    {
                        AppManager.Instance.PlaceApp(currentlyPlacingApp.feature, cursorX, cursorY);
                        currentState = MemoryUIState.AppList; // 설치 후 목록으로 돌아감
                        RefreshAppList();
                        UpdateFocus();
                        SoundManager.Instance.PlaySFX(SfxID.UI_Click); // 설치 성공음 (선택)
                    }
                    else
                    {
                        SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); // 설치 실패 에러음
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
            if (availableApps.Count > 0)
            {
                descriptionText.text = availableApps[selectedListIndex].description;
            }
        }

        // 입력 처리 (메모리 보드 포커스 상태)
        private void AttemptToPlaceApp(GameAppData appData)
        {
            // 보드 전체를 스캔하여 빈 공간이 하나라도 있는지 확인
            if (FindAutoPlaceCoordinate(appData, out Vector2Int startPos))
            {
                // 겹치는 자리에 놓고 Space를 누르면 아래 코드에서 설치를 시도
                currentlyPlacingApp = appData;
                cursorX = startPos.x;
                cursorY = startPos.y;
                currentState = MemoryUIState.BoardPlacement;
                DrawBoard(); 
            }
            else
            {
                // 여유 공간이 없으면 경고 팝업
                menuController.ShowAlertPopup("설치할 공간이 없습니다.");
            }
        }

        private void HandleBoardPlacementInput()
        {
            int prevX = cursorX;
            int prevY = cursorY;

            // 방향키로 커서 이동
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) cursorY--;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) cursorY++;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) cursorX--;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) cursorX++;

            // 커서가 보드를 벗어나지 않도록 제한
            cursorX = Mathf.Clamp(cursorX, 0, AppManager.Instance.gridWidth - 1);
            cursorY = Mathf.Clamp(cursorY, 0, AppManager.Instance.gridHeight - 1);

            if (prevX != cursorX || prevY != cursorY)
            {
                DrawBoard(); // 커서 이동 시 보드 프리뷰 다시 그리기
            }

            // 확인 키: 설치
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (AppManager.Instance.CanPlaceApp(currentlyPlacingApp.feature, cursorX, cursorY))
                {
                    AppManager.Instance.PlaceApp(currentlyPlacingApp.feature, cursorX, cursorY);
                    currentState = MemoryUIState.AppList; // 설치 완료 후 리스트로 복귀
                    RefreshAppList();
                    UpdateFocus();
                }
                else
                {
                    Debug.Log("여기에 설치할 수 없습니다!"); 
                }
            }
            
            // 취소. 취소 후 리스트로 복귀
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                currentState = MemoryUIState.AppList;
                DrawBoard(); // 프리뷰 제거
            }
        }

        // 보드 렌더링
        private void DrawBoard()
        {
            // 보드를 모두 검은색으로 초기화
            int width = AppManager.Instance.gridWidth;
            int height = AppManager.Instance.gridHeight;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    gridSlots[x, y].color = Color.black; 
                }
            }

            // 이미 설치된 앱들의 블록의 색을 정의된 색으로 바꿈
            foreach (PlacedAppData placedApp in AppManager.Instance.GetPlacedApps())
            {
                GameAppData data = AppManager.Instance.GetAppData(placedApp.feature);
                if (data == null) continue;

                foreach (Vector2Int offset in data.shapeBlocks)
                {
                    int drawX = placedApp.x + offset.x;
                    int drawY = placedApp.y + offset.y;
                    gridSlots[drawX, drawY].color = data.blockColor;
                }
            }

            // 현재 배치 진행 중인 앱 표시
            if (currentState == MemoryUIState.BoardPlacement && currentlyPlacingApp != null)
            {
                bool canPlace = AppManager.Instance.CanPlaceApp(currentlyPlacingApp.feature, cursorX, cursorY);
                
                foreach (Vector2Int offset in currentlyPlacingApp.shapeBlocks)
                {
                    int drawX = cursorX + offset.x;
                    int drawY = cursorY + offset.y;

                    if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                    {
                        // 설치 가능하면 반투명한 원래 색상, 불가능하면 반투명한 빨간색
                        Color previewColor = canPlace ? currentlyPlacingApp.blockColor : Color.red;
                        previewColor.a = 0.5f; // 반투명
                        
                        // 기존 색상 위에 덧씌움
                        gridSlots[drawX, drawY].color = previewColor; 
                    }
                }
            }
        }

        // 좌상단부터 스캔하여 처음으로 설치 가능한 좌표를 찾는 함수
        private bool FindAutoPlaceCoordinate(GameAppData appData, out Vector2Int pos)
        {
            pos = Vector2Int.zero;
            for (int y = 0; y < AppManager.Instance.gridHeight; y++)
            {
                for (int x = 0; x < AppManager.Instance.gridWidth; x++)
                {
                    if (AppManager.Instance.CanPlaceApp(appData.feature, x, y))
                    {
                        pos = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            return false;
        }
    }
    
}