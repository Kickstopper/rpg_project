using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Manager;
using Data;
using UI.Common;
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
            if (!gameObject.activeSelf) return;

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
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        void SwitchTab(int categoryIndex)
        {
            currentTab = categoryIndex;
            
            // 탭 버튼 색상 갱신
            UpdateTabVisuals();

            // 리스트 새로고침
            RefreshList();

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
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
            List<string> allItemIds = ManagerRoot.Inventory.GetAllItemIds();
            foreach (string id in allItemIds)
            {
                ConsumableItemData data = ManagerRoot.Database.GetConsumable(id);
                if (data == null) continue;

                if (data.GetCategoryTabIndex() == currentTab)
                {
                    CreateItemSlot(data);
                }
            }

            LinkButtonNavigation();

            itemInfoView?.gameObject.SetActive(currentSlots.Count > 0);
            
            // 리스트 갱신 후 첫 번째 아이템 선택 (포커스 이동)
            StartCoroutine(SelectFirstItem());
        }

        // 리스트 내의 버튼들을 이동 가능하게 엮어주는 함수
        private void LinkButtonNavigation()
        {
            if (currentSlots == null || currentSlots.Count <= 1) return;

            for (int i = 0; i < currentSlots.Count; i++)
            {
                Button currentBtn = currentSlots[i].GetComponent<Button>();
                if (currentBtn == null) continue;

                // 명시적 네비게이션 모드 설정
                Navigation customNav = new Navigation();
                customNav.mode = Navigation.Mode.Explicit;

                // 위쪽 방향키를 누르면 이전 인덱스 버튼으로 (첫 번째면 마지막 버튼으로 순환)
                int upIndex = (i == 0) ? currentSlots.Count - 1 : i - 1;
                customNav.selectOnUp = currentSlots[upIndex].GetComponent<Button>();

                // 아래쪽 방향키를 누르면 다음 인덱스 버튼으로 (마지막이면 첫 번째 버튼으로 순환)
                int downIndex = (i == currentSlots.Count - 1) ? 0 : i + 1;
                customNav.selectOnDown = currentSlots[downIndex].GetComponent<Button>();

                customNav.selectOnLeft = null;
                customNav.selectOnRight = null;

                currentBtn.navigation = customNav;
            }
        }

        void CreateItemSlot(ConsumableItemData data)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, contentTransform);
            
            // 이름/개수 표시
            int count = ManagerRoot.Inventory.GetItemCount(data.id);
            
            var itemView = slotObj.GetComponent<SimpleListItemView>();
            if (itemView != null) itemView.SetData(data.dataName, count);

            // 버튼 클릭 이벤트
            Button btn = slotObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnItemClicked(data));
            
            CommonListSlotTrigger trigger = slotObj.GetComponent<CommonListSlotTrigger>();
            if (trigger == null) trigger = slotObj.AddComponent<CommonListSlotTrigger>();

            trigger.onSelectAction = () => OnItemSelect(data);

            currentSlots.Add(slotObj);
        }

        void OnItemSelect(ConsumableItemData itemData)
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            if (itemInfoView != null) itemInfoView.UpdateInfo(itemData);
        }

        void OnItemClicked(ConsumableItemData itemData)
        {
            // 선택 후 닫기
            gameObject.SetActive(false);
            battleManager.OnPopupMenuClosed(); 
            battleManager.OnPopupItemSelected(itemData);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
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