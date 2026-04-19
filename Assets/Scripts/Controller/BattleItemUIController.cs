using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Manager;
using Data;
namespace Controller
{
    public class BattleItemUIController : MonoBehaviour
    {
        [Header("UI References")]
        public Transform contentTransform; 
        public GameObject itemSlotPrefab;
        public ItemInfoController itemInfoView;

        public BattleManager battleManager;
        
        [Header("Tabs")]
        public Button btnTabRecover;
        public Button btnTabBuff;
        public Button btnTabAttack;

        // 현재 선택된 탭 (0:Recover, 1:Buff, 2:Attack)
        private int currentTab = 0;
        
        // 생성된 아이템 슬롯 리스트
        private List<GameObject> currentSlots = new List<GameObject>();

        // 탭 버튼 색상 설정 (활성/비활성)
        private Color activeTabColor = new Color32(149, 0, 140, 255);
        private Color inactiveTabColor = new Color32(0,0,136, 255);

        void Start()
        {
            // 버튼 이벤트 연결
            btnTabRecover.onClick.AddListener(() => SwitchTab(0));
            btnTabBuff.onClick.AddListener(() => SwitchTab(1));
            btnTabAttack.onClick.AddListener(() => SwitchTab(2));
            
            // 초기 탭 색상 업데이트
            UpdateTabVisuals();

            gameObject.SetActive(false);
        }

        // 방향키 입력 감지
        void Update()
        {
            if (gameObject.activeSelf)
            {
                // 탭 전환 (왼쪽/오른쪽)
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) // Q키도 허용
                {
                    ChangeTab(-1);
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E)) // E키도 허용
                {
                    ChangeTab(1);
                }

                // 닫기 (Cancel)
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
                {
                    Close();
                }
            }
        }

        // 탭 변경 로직 (인덱스 순환)
        void ChangeTab(int direction)
        {
            // 0 -> 1 -> 2 -> 0 순환
            currentTab += direction;

            if (currentTab > 2) currentTab = 0;
            else if (currentTab < 0) currentTab = 2;

            SwitchTab(currentTab);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SwitchTab(0); // 열릴 때는 항상 첫 번째 탭부터
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (itemInfoView != null) itemInfoView.ResetText();
            battleManager.OnPopupMenuClosed();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
        }

        void SwitchTab(int categoryIndex)
        {
            currentTab = categoryIndex;
            
            // 탭 버튼 색상 갱신
            UpdateTabVisuals();

            // 리스트 새로고침
            RefreshList();

            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        // 현재 선택된 탭을 시각적으로 강조
        void UpdateTabVisuals()
        {
            SetTabColor(btnTabRecover, currentTab == 0);
            SetTabColor(btnTabBuff, currentTab == 1);
            SetTabColor(btnTabAttack, currentTab == 2);
        }

        void SetTabColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var image = btn.GetComponent<Image>();
            if (image != null)
            {
                image.color = isActive ? activeTabColor : inactiveTabColor;
            }
        }

        void RefreshList()
        {
            // 기존 리스트 삭제
            foreach (Transform child in contentTransform) Destroy(child.gameObject);
            currentSlots.Clear();

            // 아이템 생성
            List<string> allItemIds = InventoryManager.Instance.GetAllItemIds();
            foreach (string id in allItemIds)
            {
                ConsumableItemData data = DatabaseManager.Instance.GetConsumable(id);
                if (data == null) continue;

                if (data.GetCategoryIndex() == currentTab)
                {
                    CreateItemSlot(data);
                }
            }

            itemInfoView?.gameObject.SetActive(currentSlots.Count > 0);
            
            // 리스트 갱신 후 첫 번째 아이템 선택 (포커스 이동)
            StartCoroutine(SelectFirstItem());
        }

        void CreateItemSlot(ConsumableItemData data)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, contentTransform);
            
            // 이름/개수 표시
            var texts = slotObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
            int count = InventoryManager.Instance.GetItemCount(data.id);
            if(texts.Length > 1) texts[1].text = $"x{count}";

            // 버튼 클릭 이벤트
            Button btn = slotObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnItemClicked(data));
            
            // 마우스 호버 및 키보드 포커스 이벤트 연결
            EventTrigger trigger = slotObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slotObj.AddComponent<EventTrigger>();

            // Hover
            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((eventData) => OnItemSelect(data));
            trigger.triggers.Add(enterEntry);

            // Select
            EventTrigger.Entry selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((eventData) => OnItemSelect(data));
            trigger.triggers.Add(selectEntry);

            currentSlots.Add(slotObj);
        }

        void OnItemSelect(ConsumableItemData itemData)
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            if (itemInfoView != null) itemInfoView.UpdateInfo(itemData);
        }

        void OnItemClicked(ConsumableItemData itemData)
        {
            // 선택 후 닫기
            gameObject.SetActive(false);
            battleManager.OnPopupMenuClosed(); 
            battleManager.OnPopupItemSelected(itemData);
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
        }

        IEnumerator SelectFirstItem()
        {
            yield return null; 
            if (currentSlots.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(currentSlots[0]);
            }
            else
            {
                // 아이템이 없으면 탭 버튼으로 포커스
                EventSystem.current.SetSelectedGameObject(null);
                if (currentTab == 0) EventSystem.current.SetSelectedGameObject(btnTabRecover.gameObject);
                else if (currentTab == 1) EventSystem.current.SetSelectedGameObject(btnTabBuff.gameObject);
                else EventSystem.current.SetSelectedGameObject(btnTabAttack.gameObject);
            }
        }
    }
}