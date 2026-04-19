using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Manager;
using Data;

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
            if (gameObject.activeSelf)
            {
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
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
        }

        void SwitchTab(int categoryIndex)
        {
            if (currentSkillIds == null) return;
            currentTab = categoryIndex;
            UpdateTabVisuals();
            RefreshList();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
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
                SkillData data = DatabaseManager.Instance.GetSkill(id); 
                if (data == null) continue;

                if (data.GetCategoryIndex() == currentTab)
                {
                    CreateSkillSlot(data);
                }
            }
            skillInfoView?.gameObject.SetActive(currentSlots.Count > 0);
            StartCoroutine(SelectFirstItem());
        }

        void CreateSkillSlot(SkillData data)
        {
            GameObject slotObj = Instantiate(skillSlotPrefab, contentTransform);
            Button btn = slotObj.GetComponent<Button>();
            
            var texts = slotObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
            if(texts.Length > 0) texts[0].text = data.dataName;
            
            string consumeText = data.useHpCost ? "HP" : "MP";
            string cost = data.costValue.ToString();
            if(texts.Length > 1) texts[1].text = $"{consumeText} {cost}";

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


            // 버튼 상태 및 클릭 이벤트 처리. 버튼 자체는 항상 활성화 (그래야 포커스가 이동됨)
            btn.interactable = true;
            Color textColor = canUse ? Color.white : Color.gray;
            foreach (var txt in texts) txt.color = textColor;

            btn.onClick.AddListener(() => 
            {
                if (canUse)
                    OnItemClicked(data);
                else
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            });
            EventTrigger trigger = slotObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slotObj.AddComponent<EventTrigger>();

            // hover
            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((eventData) => OnItemSelect(data));
            trigger.triggers.Add(enterEntry);
            // select
            EventTrigger.Entry selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener((eventData) => OnItemSelect(data));
            trigger.triggers.Add(selectEntry);
            
            currentSlots.Add(slotObj);
        }

        void OnItemSelect(SkillData skillData)
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            if (skillInfoView != null) skillInfoView.UpdateInfo(skillData);
        }

        void OnItemClicked(BaseRootData itemData)
        {
            gameObject.SetActive(false);
            battleManager.OnPopupMenuClosed(); 
            battleManager.OnPopupItemSelected(itemData);
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);
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