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
        private Color activeTabColor = Color.white;
        private Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 약간 어둡게

        private List<string> currentSkillIds;

        void Start()
        {
            // 버튼 이벤트 연결
            btnTabMagic.onClick.AddListener(() => SwitchTab(0));
            btnTabRecover.onClick.AddListener(() => SwitchTab(1));
            btnTabAssist.onClick.AddListener(() => SwitchTab(2));
            btnTabSpecial.onClick.AddListener(() => SwitchTab(3));
            
            // 초기 탭 색상 업데이트
            UpdateTabVisuals();

            gameObject.SetActive(false);
        }

        // 방향키 입력 감지
        void Update()
        {
            if (gameObject.activeSelf)
            {
                // 1. 탭 전환 (왼쪽/오른쪽)
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) // Q키도 허용
                {
                    ChangeTab(-1);
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E)) // E키도 허용
                {
                    ChangeTab(1);
                }

                // 2. 닫기 (Cancel)
                if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }
            }
        }

        // 탭 변경 로직 (인덱스 순환)
        void ChangeTab(int direction)
        {
            // 0 -> 1 -> 2 -> 3 -> 0 순환
            currentTab += direction;

            if (currentTab > 3) currentTab = 0;
            else if (currentTab < 0) currentTab = 3;

            SwitchTab(currentTab);
        }

        public void Show(List<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return;
            currentSkillIds = skillIds;
            gameObject.SetActive(true);
            SwitchTab(0); // 열릴 때는 항상 첫 번째 탭부터
        }

        public void Close()
        {
            currentSkillIds = null;
            gameObject.SetActive(false);
            CombatManager.Instance.OnPopupMenuClosed();
        }

        void SwitchTab(int categoryIndex)
        {
            if (currentSkillIds == null) return;
            
            currentTab = categoryIndex;
            
            // 탭 버튼 색상 갱신
            UpdateTabVisuals();

            // 리스트 새로고침
            RefreshList();
        }

        // 현재 선택된 탭을 시각적으로 강조
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
            if (image != null)
            {
                image.color = isActive ? activeTabColor : inactiveTabColor;
            }
        }

        void RefreshList()
        {
            // 1. 기존 리스트 삭제
            foreach (Transform child in contentTransform) Destroy(child.gameObject);
            currentSlots.Clear();

            // 2. 아이템 생성
            foreach (string id in currentSkillIds)
            {
                SkillData data = DatabaseManager.Instance.GetSkill(id);
                if (data == null) continue;

                if (data.GetCategoryIndex() == currentTab)
                {
                    CreateSkillSlot(data);
                }
            }
            
            // 3. 리스트 갱신 후 첫 번째 아이템 선택 (포커스 이동)
            StartCoroutine(SelectFirstItem());
        }

        void CreateSkillSlot(SkillData data)
        {
            GameObject slotObj = Instantiate(skillSlotPrefab, contentTransform);
            
            // 이름 표시
            var texts = slotObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
            if(texts.Length > 0) texts[0].text = data.dataName;
            
            // 소모량 표시
            string consumeText = data.useHpCost ? "HP" : "MP";
            string cost = data.costValue.ToString();
            if(texts.Length > 1) texts[1].text = $"{consumeText} {cost}";

            // 버튼 이벤트
            slotObj.GetComponent<Button>().onClick.AddListener(() => OnItemClicked(data));
            
            currentSlots.Add(slotObj);
        }

        void OnItemClicked(BaseRootData itemData)
        {
            // 선택 후 닫기
            gameObject.SetActive(false);
            CombatManager.Instance.OnPopupMenuClosed(); 
            CombatManager.Instance.OnPopupItemSelected(itemData);
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
                if (currentTab == 0) EventSystem.current.SetSelectedGameObject(btnTabMagic.gameObject);
                else if (currentTab == 1) EventSystem.current.SetSelectedGameObject(btnTabRecover.gameObject);
                else if (currentTab == 2) EventSystem.current.SetSelectedGameObject(btnTabAssist.gameObject);
                else  EventSystem.current.SetSelectedGameObject(btnTabSpecial.gameObject);
            }
        }
    }
}