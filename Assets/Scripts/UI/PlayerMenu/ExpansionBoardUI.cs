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

        [Header("Preview Overlay Settings")]
        public RectTransform previewContainer;
        public float smoothSpeed = 40f;

        private List<Image> previewImages = new List<Image>();
        private float targetRotationAngle = 0f;
        private float currentRotationAngle = 0f;

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

            // 애니메이션은 입력 쿨타임과 무관하게 매 프레임 실행
            if (currentState == ExpansionBoardUIState.BoardPlacement && currentlyPlacingModule != null)
            {
                UpdatePreviewAnimation();
            }

            // 입력을 감지하는 로직은 쿨타임이 지났을 때만 실행
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

        // 블럭의 부드러운 이동과 회전
        private void UpdatePreviewAnimation()
        {
            if (previewContainer == null || !previewContainer.gameObject.activeSelf) return;

            // 각도 보간
            currentRotationAngle = Mathf.Lerp(currentRotationAngle, targetRotationAngle, Time.deltaTime * smoothSpeed);
            previewContainer.localRotation = Quaternion.Euler(0, 0, currentRotationAngle);

            // 위치 보간
            Vector3 targetPos = gridSlots[cursorX, cursorY].transform.position;
            previewContainer.position = Vector3.Lerp(previewContainer.position, targetPos, Time.deltaTime * smoothSpeed);
        }

        // 초기화 및 리스트 갱신
        private void InitializeBoardUI()
        {
            int width = ManagerRoot.Module.gridWidth;
            int height = ManagerRoot.Module.gridHeight;
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

            if (ManagerRoot.Module != null)
                ManagerRoot.Module.SyncexpansionBoardWithMountedModules(); 

            foreach (ModuleFeature feature in ManagerRoot.Module.ownedModules)
            {
                GameModuleData data = ManagerRoot.Module.GetModuleData(feature);
                if (data != null) availableModules.Add(data);
            }

            for (int i = 0; i < availableModules.Count; i++)
            {
                GameModuleData moduleData = availableModules[i];
                GameObject obj = Instantiate(moduleItemPrefab, moduleListContent);
                ModuleItemUI itemUI = obj.GetComponent<ModuleItemUI>();
                
                bool isMounted = ManagerRoot.Module.IsMounted(moduleData.feature);
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

                if (ManagerRoot.Module.IsMounted(selectedModule.feature))
                {
                    // 이미 설치된 모듈이면 즉시 해제
                    ManagerRoot.Module.UnmountModule(selectedModule.feature);
                    RefreshModuleList(); // 상태 갱신
                    UpdateFocus();
                }
                else
                {
                    // 설치되지 않은 모듈이면 보드 배치 모드로 전환 시도
                    AttemptToMountModule(selectedModule);
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                if (menuController != null)
                {
                    menuController.CloseModuleUI();
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
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
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

                    if (ManagerRoot.Module.IsMounted(selectedModule.feature))
                    {
                        ManagerRoot.Module.UnmountModule(selectedModule.feature);
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
                    UpdatePreviewColor(); 
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
                    if (ManagerRoot.Module.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation))
                    {
                        ManagerRoot.Module.MountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
                        
                        // 마우스로 마운트 성공 시 반투명 프리뷰 끔
                        previewContainer.gameObject.SetActive(false); 

                        currentState = ExpansionBoardUIState.ModuleList; // 설치 후 목록으로 돌아감
                        RefreshModuleList();
                        UpdateFocus();
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                    }
                    else
                    {
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
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

        // 입력 처리 (확장보드 포커스 상태)
        private void AttemptToMountModule(GameModuleData moduleData)
        {
            if (FindAutoMountCoordinate(moduleData, out Vector2Int startPos, out int foundRot))
            {
                currentlyPlacingModule = moduleData;
                cursorX = startPos.x;
                cursorY = startPos.y;
                currentRotation = foundRot; 
                
                // 시작할 때 프리뷰 세팅 및 각도 즉시 스냅
                SetupPreviewOverlay(moduleData);
                
                // 논리적 각도와 시각적 각도를 동기화
                targetRotationAngle = currentRotation * 90f; 
                
                currentRotationAngle = targetRotationAngle; 
                previewContainer.localRotation = Quaternion.Euler(0, 0, currentRotationAngle);
                
                currentState = ExpansionBoardUIState.BoardPlacement;
            }
            else
            {
                menuController.ShowAlertPopup("설치할 공간이 없습니다.");
            }
        }

        private void HandleBoardPlacementInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            // 회전 입력 (Q, E, 휠)
            if (Input.GetKeyDown(KeyCode.Q) || scroll > 0f) 
            {
                menuController.ResetInputTimer();
                // 3에서 1로 변경: 시각적 +90도(반시계)와 수학적 변환 일치
                currentRotation = (currentRotation + 1) % 4; 
                targetRotationAngle += 90f; 
                UpdatePreviewColor(); 
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            }
            else if (Input.GetKeyDown(KeyCode.E) || scroll < 0f)
            {
                menuController.ResetInputTimer();
                // 1에서 3으로 변경: 시각적 -90도(시계)와 수학적 변환 일치
                currentRotation = (currentRotation + 3) % 4; 
                targetRotationAngle -= 90f; 
                UpdatePreviewColor();
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            }

            int prevX = cursorX;
            int prevY = cursorY;

            // 이동 입력
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) cursorY--;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) cursorY++;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) cursorX--;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) cursorX++;

            if (prevX != cursorX || prevY != cursorY)
            {
                menuController.ResetInputTimer(); // 키보드로 움직일 때도 쿨타임 적용
                cursorX = Mathf.Clamp(cursorX, 0, ManagerRoot.Module.gridWidth - 1);
                cursorY = Mathf.Clamp(cursorY, 0, ManagerRoot.Module.gridHeight - 1);
                UpdatePreviewColor(); 
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            }

            // 설치
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                // 스페이스바 연타로 인한 꼬임 방지
                menuController.ResetInputTimer(); 

                if (ManagerRoot.Module.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation))
                {
                    ManagerRoot.Module.MountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
                    previewContainer.gameObject.SetActive(false); 
                    currentState = ExpansionBoardUIState.ModuleList; 
                    RefreshModuleList(); 
                    UpdateFocus();
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                }
                else ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
            }
            
            // 취소
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
            {
                previewContainer.gameObject.SetActive(false);
                currentState = ExpansionBoardUIState.ModuleList;
            }
        }

        // 보드 렌더링
        private void DrawBoard()
        {
            int width = ManagerRoot.Module.gridWidth;
            int height = ManagerRoot.Module.gridHeight;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    gridSlots[x, y].color = Color.black; 

            foreach (PlacedModuleData placedModule in ManagerRoot.Module.GetMountedModules())
            {
                GameModuleData data = ManagerRoot.Module.GetModuleData(placedModule.feature);
                if (data == null) continue;

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
        }

        // 4방향을 모두 탐색하며 최초로 들어맞는 공간과 각도를 찾음
        private bool FindAutoMountCoordinate(GameModuleData moduleData, out Vector2Int pos, out int foundRotation)
        {
            pos = Vector2Int.zero;
            foundRotation = 0;

            // 회전 상태 0 ~ 3 순으로 검사
            for (int r = 0; r < 4; r++)
            {
                for (int y = 0; y < ManagerRoot.Module.gridHeight; y++)
                {
                    for (int x = 0; x < ManagerRoot.Module.gridWidth; x++)
                    {
                        if (ManagerRoot.Module.CanMountModule(moduleData.feature, x, y, r))
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

        private void SetupPreviewOverlay(GameModuleData moduleData)
        {
            previewContainer.gameObject.SetActive(true);
            
            // UI의 맨 앞으로 가져오기 (검은색 보드판 뒤에 가려지는 현상 방지)
            previewContainer.SetAsLastSibling(); 

            foreach (var img in previewImages) Destroy(img.gameObject);
            previewImages.Clear();

            // GridLayoutGroup에서 CellSize와 Spacing을 가져옴
            GridLayoutGroup gridLayout = expansionBoardGrid.GetComponent<GridLayoutGroup>();
            Vector2 slotSize = gridLayout != null ? gridLayout.cellSize : new Vector2(100, 100);
            
            float stepX = slotSize.x;
            float stepY = -slotSize.y;
            if (gridLayout != null)
            {
                stepX += gridLayout.spacing.x;
                stepY -= gridLayout.spacing.y;
            }

            // 회전하지 않은 상태의 블록들을 생성
            foreach (Vector2Int offset in moduleData.shapeBlocks)
            {
                GameObject obj = Instantiate(gridSlotPrefab, previewContainer);
                RectTransform rt = obj.GetComponent<RectTransform>();
                
                // 회전을 위해 앵커와 피벗을 중앙으로 맞춤
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Grid 밖에서 생성되었으므로 크기를 강제로 지정해 줌
                rt.sizeDelta = slotSize;

                rt.localPosition = new Vector3(offset.x * stepX, offset.y * stepY, 0);
                
                Image img = obj.GetComponent<Image>();
                img.raycastTarget = false;
                previewImages.Add(img);
            }
            UpdatePreviewColor();
        }

        // 겹침 여부에 따라 프리뷰 색상 업데이트
        private void UpdatePreviewColor()
        {
            if (currentlyPlacingModule == null) return;
            bool canPlace = ManagerRoot.Module.CanMountModule(currentlyPlacingModule.feature, cursorX, cursorY, currentRotation);
            
            Color previewColor = canPlace ? currentlyPlacingModule.blockColor : Color.red;
            previewColor.a = 0.6f;
            
            foreach (var img in previewImages) img.color = previewColor;
        }
    }
}