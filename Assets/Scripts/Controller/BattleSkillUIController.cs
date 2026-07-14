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
    public class BattleSkillUIController : MonoBehaviour
    {
        [Header("UI References")]
        public Transform contentTransform; 
        public GameObject skillSlotPrefab;
        public SkillInfoController skillInfoView;

        public BattleManager battleManager;
        
        [Header("Tabs")]
        public Button btnTabMagic;
        public Button btnTabRecover;
        public Button btnTabAssist;
        public Button btnTabSpecial;

        // 현재 선택된 탭 (0:Recover, 1:Buff, 2:Attack)
        private int currentTab = 0;
        
        // 생성된 아이템 슬롯 리스트
        private List<GameObject> currentSlots = new List<GameObject>();

        // 탭 버튼 색상 설정 (활성/비활성)
        private Color activeTabColor = new Color32(149, 0, 140, 255);
        private Color inactiveTabColor = new Color32(0,0,136, 255);

        private List<string> currentSkillIds;
        
        // 현재 스킬을 시전하려는 캐릭터 정보 저장
        private PlayerController currentActor;

        void Start()
        {
            // 버튼 이벤트 연결
            btnTabMagic.onClick.AddListener(() => SwitchTab(0));
            btnTabRecover.onClick.AddListener(() => SwitchTab(1));
            btnTabAssist.onClick.AddListener(() => SwitchTab(2));
            btnTabSpecial.onClick.AddListener(() => SwitchTab(3));
            
            UpdateTabVisuals();
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            // 탭 전환
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q))
            {
                ChangeTab(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
            {
                ChangeTab(1);
            }

            // 닫기
            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
            {
                Close();
            }
        }

        void ChangeTab(int direction)
        {
            currentTab += direction;
            if (currentTab > 3) currentTab = 0;
            else if (currentTab < 0) currentTab = 3;
            
            SwitchTab(currentTab);
        }
        
        public void Show(List<string> skillIds, PlayerController actor)
        {
            if (skillIds == null || skillIds.Count == 0) return;
            
            currentSkillIds = skillIds;
            currentActor = actor; // 시전 캐릭터 저장
            
            gameObject.SetActive(true);
            SwitchTab(0); 
        }

        public void Close()
        {
            currentSkillIds = null;
            currentActor = null; // 초기화
            gameObject.SetActive(false);
            if (skillInfoView != null) skillInfoView.ResetText();
            battleManager.OnPopupMenuClosed();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        void SwitchTab(int categoryIndex)
        {
            if (currentSkillIds == null) return;
            currentTab = categoryIndex;
            UpdateTabVisuals();
            RefreshList();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
        }

        void UpdateTabVisuals()
        {
            SetTabColor(btnTabMagic, currentTab == 0);
            SetTabColor(btnTabRecover, currentTab == 1);
            SetTabColor(btnTabAssist, currentTab == 2);
            SetTabColor(btnTabSpecial, currentTab == 3);
        }

        void SetTabColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var image = btn.GetComponent<Image>();
            if (image != null) image.color = isActive ? activeTabColor : inactiveTabColor;
        }

        void RefreshList()
        {
            foreach (Transform child in contentTransform) Destroy(child.gameObject);
            currentSlots.Clear();

            foreach (string id in currentSkillIds)
            {
                SkillData data = ManagerRoot.Database.GetSkill(id); 
                if (data == null) continue;

                if (data.GetCategoryIndex() == currentTab)
                {
                    CreateSkillSlot(data);
                }
            }

            LinkButtonNavigation();

            skillInfoView?.gameObject.SetActive(currentSlots.Count > 0);
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

        void CreateSkillSlot(SkillData data)
        {
            bool canUse = true;
            if (currentActor != null)
            {
                if (data.useHpCost)
                {
                    if (currentActor.currentHp <= data.costValue) canUse = false;
                }
                else
                {
                    if (currentActor.currentMp < data.costValue) canUse = false;
                }
            }

            GameObject slotObj = Instantiate(skillSlotPrefab, contentTransform);
            var itemView = slotObj.GetComponent<SimpleListItemView>();
            if (itemView != null)
            {
                string consumeText = data.useHpCost ? "HP" : "MP";
                string cost = data.costValue.ToString();
                itemView.SetData(data.dataName,  $"{consumeText} {cost}");

                Color textColor = canUse ? Color.white : Color.gray;
                itemView.SetNameTextColor(textColor);
                itemView.SetValueTextColor(textColor);
            }

            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
                btn.onClick.AddListener(() => {
                    if (canUse)
                        OnItemClicked(data);
                    else
                        ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                });
                
            }
            
            CommonListSlotTrigger trigger = slotObj.GetComponent<CommonListSlotTrigger>();
            if (trigger == null) trigger = slotObj.AddComponent<CommonListSlotTrigger>();

            trigger.onSelectAction = () => OnItemSelect(data);
            
            currentSlots.Add(slotObj);
        }

        void OnItemSelect(SkillData skillData)
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            if (skillInfoView != null) skillInfoView.UpdateInfo(skillData);
        }

        void OnItemClicked(BaseRootData itemData)
        {
            gameObject.SetActive(false);
            battleManager.OnPopupMenuClosed(); 
            battleManager.OnPopupItemSelected(itemData);
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
        }

        IEnumerator SelectFirstItem()
        {
            yield return null; 
            
            // 사용 가능한 첫 번째 슬롯을 찾을지, 그냥 첫 번째를 잡을지 결정
            if (currentSlots.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(currentSlots[0]);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (currentTab == 0) EventSystem.current.SetSelectedGameObject(btnTabMagic.gameObject);
                else if (currentTab == 1) EventSystem.current.SetSelectedGameObject(btnTabRecover.gameObject);
                else if (currentTab == 2) EventSystem.current.SetSelectedGameObject(btnTabAssist.gameObject);
                else  EventSystem.current.SetSelectedGameObject(btnTabSpecial.gameObject);
            }
        }
    }
}