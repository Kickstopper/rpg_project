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
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
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
            battleManager.OnPopupMenuClosed();
        }

        void SwitchTab(int categoryIndex)
        {
            if (currentSkillIds == null) return;
            currentTab = categoryIndex;
            UpdateTabVisuals();
            RefreshList();
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

            // ---------------------------------------------------------
            // 1. 사용 가능 여부 판별 (로직은 그대로 유지)
            // ---------------------------------------------------------
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

            // ---------------------------------------------------------
            // 버튼 상태 및 클릭 이벤트 처리
            // ---------------------------------------------------------
            
            // 1. 버튼 자체는 항상 활성화 (그래야 포커스가 이동됨)
            btn.interactable = true; 

            // 2. 시각적 구분: 사용 불가면 글자색을 회색으로, 가능하면 흰색으로
            Color textColor = canUse ? Color.white : Color.gray;
            foreach (var txt in texts) txt.color = textColor;

            // 3. 클릭 리스너 내부에서 분기 처리
            btn.onClick.AddListener(() => 
            {
                if (canUse)
                {
                    // 조건 만족 시 정상 실행
                    OnItemClicked(data);
                }
                else
                {
                    // 조건 불만족 시 거부 반응 (효과음 등)
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel); // 띵~ 소리
                    // 필요하다면 로그 출력: "MP가 부족합니다" 등
                }
            });
            
            currentSlots.Add(slotObj);
        }

        void OnItemClicked(BaseRootData itemData)
        {
            gameObject.SetActive(false);
            battleManager.OnPopupMenuClosed(); 
            battleManager.OnPopupItemSelected(itemData);
        }

        IEnumerator SelectFirstItem()
        {
            yield return null; 
            
            // 사용 가능한 첫 번째 슬롯을 찾을지, 그냥 첫 번째를 잡을지 결정
            // 여기서는 단순히 첫 번째 슬롯을 잡지만, interactable이 false면 포커스는 가되 클릭은 안됨
            
            if (currentSlots.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null);
                
                // 첫 번째 슬롯이 비활성화 상태여도 포커스는 갈 수 있어야 키보드로 다른 스킬을 고를 수 있음. 
                // 만약 '사용 가능한 첫 번째'를 원한다면 반복문으로 interactable == true인 것을 찾아야 함.
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