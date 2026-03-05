using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Controller;
using Data;
using Helper;
using System.Collections;
using DG.Tweening;

namespace UI.Battle
{
    public class LevelUpUI : MonoBehaviour
    {
        [Header("Settings")]
        public int pointsPerLevel = 3; 

        [Header("Character Info UI")]
        public GameObject LeveUpUI;
        public Image portraitImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;    
        public TextMeshProUGUI hpText;       
        public TextMeshProUGUI mpText;       
        public TextMeshProUGUI nextExpText;  
        public TextMeshProUGUI pointsText;   

        [Header("Stat Rows")]
        public TextMeshProUGUI statText;
        public StatUIRow strRow;
        public StatUIRow magRow;
        public StatUIRow intRow;
        public StatUIRow vitRow;
        public StatUIRow agiRow;
        public StatUIRow lucRow;

        [Header("Skill UI")]
        public TextMeshProUGUI skillText;
        public Transform skillContent;      
        public GameObject skillSlotPrefab;
        public TextMeshProUGUI descriptionText;

        [Header("Controls")]
        public Button confirmButton; 

        private Queue<PlayerController> levelUpQueue = new Queue<PlayerController>();
        private Dictionary<PlayerController, int> oldLevelsDict;
        
        private PlayerController currentTarget;
        private int oldLevel;
        private int availablePoints;
        
        private StatData baseStats;
        private StatData allocatedStats;

        private System.Action onAllFinished;

        private enum StatType { STR, MAG, INT, VIT, AGI, LUC }

        private enum FocusSection { Stat, Skill }
        private FocusSection currentSection = FocusSection.Stat;

        // 방향키 롱 프레스 상태 처리를 위한 변수
        private float inputTimer = 0f;
        private float inputDelay = 0.4f;      
        private float inputRepeatRate = 0.1f; 
        private bool isKeyHeld = false;

        private GameObject lastSelectedObject; // 마지막 선택했던 오브젝트

        [System.Serializable]
        public class StatUIRow
        {
            public TextMeshProUGUI valueText;
            public Slider slider;
            public Button upButton;
            public Button downButton;
        }

        public void Show(List<PlayerController> leveledUpPlayers, Dictionary<PlayerController, int> oldLevels, System.Action onFinished)
        {
            LeveUpUI.SetActive(true);
            
            levelUpQueue.Clear();
            foreach (var p in leveledUpPlayers) levelUpQueue.Enqueue(p);
            
            this.oldLevelsDict = oldLevels;
            this.onAllFinished = onFinished;

            BindButtons();
            ProcessNextCharacter();
        }

        private void BindButtons()
        {
            strRow.upButton.onClick.RemoveAllListeners(); strRow.upButton.onClick.AddListener(() => ChangeStat(StatType.STR, 1));
            magRow.upButton.onClick.RemoveAllListeners(); magRow.upButton.onClick.AddListener(() => ChangeStat(StatType.MAG, 1));
            intRow.upButton.onClick.RemoveAllListeners(); intRow.upButton.onClick.AddListener(() => ChangeStat(StatType.INT, 1));
            vitRow.upButton.onClick.RemoveAllListeners(); vitRow.upButton.onClick.AddListener(() => ChangeStat(StatType.VIT, 1));
            agiRow.upButton.onClick.RemoveAllListeners(); agiRow.upButton.onClick.AddListener(() => ChangeStat(StatType.AGI, 1));
            lucRow.upButton.onClick.RemoveAllListeners(); lucRow.upButton.onClick.AddListener(() => ChangeStat(StatType.LUC, 1));

            strRow.downButton.onClick.RemoveAllListeners(); strRow.downButton.onClick.AddListener(() => ChangeStat(StatType.STR, -1));
            magRow.downButton.onClick.RemoveAllListeners(); magRow.downButton.onClick.AddListener(() => ChangeStat(StatType.MAG, -1));
            intRow.downButton.onClick.RemoveAllListeners(); intRow.downButton.onClick.AddListener(() => ChangeStat(StatType.INT, -1));
            vitRow.downButton.onClick.RemoveAllListeners(); vitRow.downButton.onClick.AddListener(() => ChangeStat(StatType.VIT, -1));
            agiRow.downButton.onClick.RemoveAllListeners(); agiRow.downButton.onClick.AddListener(() => ChangeStat(StatType.AGI, -1));
            lucRow.downButton.onClick.RemoveAllListeners(); lucRow.downButton.onClick.AddListener(() => ChangeStat(StatType.LUC, -1));

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void ProcessNextCharacter()
        {
            if (levelUpQueue.Count == 0)
            {
                LeveUpUI.SetActive(false);
                onAllFinished?.Invoke();
                return;
            }

            currentTarget = levelUpQueue.Dequeue();
            oldLevel = oldLevelsDict[currentTarget];

            int levelsGained = currentTarget.sourceData.stats.level - oldLevel;
            availablePoints = levelsGained * pointsPerLevel;

            baseStats = currentTarget.sourceData.stats; 
            allocatedStats = new StatData(); 

            if (currentTarget.portraitImage != null) portraitImage.sprite = currentTarget.portraitImage.sprite;
            nameText.text = currentTarget.entityName;
            levelText.text = $"LV {oldLevel} -> {currentTarget.sourceData.stats.level}";
            
            int nextExp = BattleCalculator.GetMaxExpForLevel(currentTarget.sourceData.stats.level);
            nextExpText.text = $"NEXT EXP {nextExp - currentTarget.sourceData.currentExp}";

            PopulateSkillList();

            // 1. 초기화 시 스탯 섹션으로 포커스
            SetSectionFocus(FocusSection.Stat);
            RefreshUI();
            
            StartCoroutine(SelectFirstAvailableButton());
        }

        private void SetSectionFocus(FocusSection section)
        {
            currentSection = section;
            if (statText != null) statText.color = (section == FocusSection.Stat) ? Color.yellow : Color.white;
            if (skillText != null) skillText.color = (section == FocusSection.Skill) ? Color.yellow : Color.white;

            CanvasGroup skillGroup = skillContent.GetComponent<CanvasGroup>();
            if (skillGroup == null) skillGroup = skillContent.gameObject.AddComponent<CanvasGroup>();

            // Stat 상태일 때는 스킬 슬롯 클릭 및 선택 차단
            skillGroup.interactable = (section == FocusSection.Skill);
            skillGroup.blocksRaycasts = (section == FocusSection.Skill);
        }

        private void PopulateSkillList()
        {
            foreach (Transform child in skillContent) Destroy(child.gameObject);
            ClearDescription();

            foreach (string skillId in currentTarget.learnedSkillIds)
            {
                var skillData = Manager.DatabaseManager.Instance.GetSkill(skillId);
                if (skillData != null && skillSlotPrefab != null)
                {
                    GameObject go = Instantiate(skillSlotPrefab, skillContent);
                    var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null) txt.text = skillData.dataName;

                    AddSelectionEvent(go, skillData.description);
                }
            }
        }

        private void AddSelectionEvent(GameObject go, string description)
        {
            Selectable selectable = go.GetComponent<Selectable>();
            if (selectable == null) selectable = go.AddComponent<Button>(); 

            EventTrigger trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();

            // 키보드로 선택
            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((data) => { 
                ShowDescription(description); 
                ScrollToSlot(go.GetComponent<RectTransform>()); // 포커스된 슬롯으로 자동 스크롤
            });
            trigger.triggers.Add(selectEntry);

            // 마우스로 선택
            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
            pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
            pointerEnterEntry.callback.AddListener((data) => { 
                ShowDescription(description); 
                ScrollToSlot(go.GetComponent<RectTransform>());
            });
            trigger.triggers.Add(pointerEnterEntry);

            // 포커스를 잃거나 마우스가 밖으로 나갔을 때
            EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
            deselectEntry.eventID = EventTriggerType.Deselect;
            deselectEntry.callback.AddListener((data) => { ClearDescription(); });
            trigger.triggers.Add(deselectEntry);

            EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
            pointerExitEntry.eventID = EventTriggerType.PointerExit;
            pointerExitEntry.callback.AddListener((data) => { ClearDescription(); });
            trigger.triggers.Add(pointerExitEntry);
        }

        // 스크롤 자동 포커싱 로직
        private void ScrollToSlot(RectTransform target)
        {
            ScrollRect scrollRect = skillContent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            RectTransform contentPanel = skillContent.GetComponent<RectTransform>();

            // Content의 전체 높이가 Viewport의 높이보다 작으면 스크롤 무시
            if (contentPanel.rect.height <= scrollRect.viewport.rect.height) return;

            // 레이아웃의 크기와 위치 강제 업데이트
            Canvas.ForceUpdateCanvases();

            // 타겟을 Viewport의 중앙에 위치시키기 위한 Content의 Y좌표 계산
            float targetY = Mathf.Abs(target.anchoredPosition.y);
            float newY = targetY - (scrollRect.viewport.rect.height / 2f);

            // Content가 맨 위나 맨 아래를 벗어나지 않도록 최대 범위 제한
            float maxY = contentPanel.rect.height - scrollRect.viewport.rect.height;
            newY = Mathf.Clamp(newY, 0, maxY);

            contentPanel.DOKill();
            contentPanel.DOAnchorPosY(newY, 0.15f).SetEase(Ease.OutCubic);
        }

        private void ShowDescription(string desc)
        {
            if (descriptionText != null) descriptionText.text = desc;
        }

        private void ClearDescription()
        {
            if (descriptionText != null) descriptionText.text = ""; 
        }

        private void ChangeStat(StatType type, int amount)
        {
            int currentAllocated = GetAllocatedStat(type);
            bool isChanged = false;

            if (amount > 0 && availablePoints > 0)
            {
                AddAllocatedStat(type, 1);
                availablePoints -= 1;
                isChanged = true;
                Manager.SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            }
            else if (amount < 0 && currentAllocated > 0)
            {
                AddAllocatedStat(type, -1);
                availablePoints += 1;
                isChanged = true;
                Manager.SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);

                // 스킬 리스트에서 마우스로 스탯 취소 버튼을 클릭했을 때 다시 스탯 섹션으로 강제 이동
                if (currentSection == FocusSection.Skill)
                {
                    SetSectionFocus(FocusSection.Stat);
                }
            }

            if (isChanged)
            {
                GameObject lastSelected = EventSystem.current.currentSelectedGameObject;
                RefreshUI();

                // 포인트가 0이 되면 스킬 섹션으로 포커스 이동
                if (availablePoints == 0 && currentSection == FocusSection.Stat)
                {
                    SetSectionFocus(FocusSection.Skill);
                    StartCoroutine(SelectFirstAvailableButton());
                }
                else if (lastSelected != null && currentSection == FocusSection.Stat)
                {
                    Selectable sel = lastSelected.GetComponent<Selectable>();
                    if (sel != null && !sel.interactable)
                    {
                        StartCoroutine(SelectFirstAvailableButton());
                    }
                }
            }
        }

        private int GetAllocatedStat(StatType type)
        {
            switch (type) {
                case StatType.STR: return allocatedStats.str;
                case StatType.MAG: return allocatedStats.mag;
                case StatType.INT: return allocatedStats.intel;
                case StatType.VIT: return allocatedStats.vit;
                case StatType.AGI: return allocatedStats.agi;
                case StatType.LUC: return allocatedStats.luc;
                default: return 0;
            }
        }

        private void AddAllocatedStat(StatType type, int amount)
        {
            switch (type) {
                case StatType.STR: allocatedStats.str += amount; break;
                case StatType.MAG: allocatedStats.mag += amount; break;
                case StatType.INT: allocatedStats.intel += amount; break;
                case StatType.VIT: allocatedStats.vit += amount; break;
                case StatType.AGI: allocatedStats.agi += amount; break;
                case StatType.LUC: allocatedStats.luc += amount; break;
            }
        }

        private void RefreshUI()
        {
            UpdateStatRow(strRow, baseStats.str, allocatedStats.str);
            UpdateStatRow(magRow, baseStats.mag, allocatedStats.mag);
            UpdateStatRow(intRow, baseStats.intel, allocatedStats.intel);
            UpdateStatRow(vitRow, baseStats.vit, allocatedStats.vit);
            UpdateStatRow(agiRow, baseStats.agi, allocatedStats.agi);
            UpdateStatRow(lucRow, baseStats.luc, allocatedStats.luc);

            if (pointsText) pointsText.text = $"POINTS: {availablePoints}";

            int previewMaxHp = (baseStats.vit + allocatedStats.vit) * 20;
            int previewMaxMp = (baseStats.mag + allocatedStats.mag) * 30;

            hpText.text = $"HP {previewMaxHp}/{previewMaxHp}";
            mpText.text = $"MP {previewMaxMp}/{previewMaxMp}";

            confirmButton.gameObject.SetActive(availablePoints == 0);
            UpdateNavigation();
        }

        private void UpdateStatRow(StatUIRow row, int baseVal, int allocated)
        {
            int total = baseVal + allocated;
            row.valueText.text = total.ToString();
            if (row.slider != null) row.slider.value = total; 

            row.upButton.interactable = (availablePoints > 0);
            row.downButton.interactable = (allocated > 0);
        }

        private void UpdateNavigation()
        {
            StatUIRow[] rows = new StatUIRow[] { strRow, magRow, intRow, vitRow, agiRow, lucRow };

            List<Button> activeUpBtns = new List<Button>();
            List<Button> activeDownBtns = new List<Button>();

            foreach (var row in rows)
            {
                if (row.upButton.interactable && row.upButton.gameObject.activeInHierarchy) activeUpBtns.Add(row.upButton);
                if (row.downButton.interactable && row.downButton.gameObject.activeInHierarchy) activeDownBtns.Add(row.downButton);
            }

            LinkVerticalNavigation(activeUpBtns, null);
            LinkVerticalNavigation(activeDownBtns, null);

            foreach (var row in rows)
            {
                Button downBtn = (row.downButton.interactable && row.downButton.gameObject.activeInHierarchy) ? row.downButton : null;
                Button upBtn = (row.upButton.interactable && row.upButton.gameObject.activeInHierarchy) ? row.upButton : null;

                if (downBtn != null)
                {
                    Navigation nav = downBtn.navigation;
                    nav.selectOnRight = upBtn != null ? upBtn : downBtn;
                    nav.selectOnLeft = upBtn != null ? upBtn : downBtn; 
                    downBtn.navigation = nav;
                }

                if (upBtn != null)
                {
                    Navigation nav = upBtn.navigation;
                    nav.selectOnLeft = downBtn != null ? downBtn : upBtn;
                    nav.selectOnRight = downBtn != null ? downBtn : upBtn; 
                    upBtn.navigation = nav;
                }
            }

            // 스킬 슬롯들의 세로 이동 강제
            if (skillContent.childCount > 0)
            {
                for (int i = 0; i < skillContent.childCount; i++)
                {
                    Button skillBtn = skillContent.GetChild(i).GetComponent<Button>();
                    if (skillBtn != null)
                    {
                        Navigation nav = skillBtn.navigation;
                        nav.mode = Navigation.Mode.Explicit;
                        
                        Button upTarget = (i == 0) ? skillContent.GetChild(skillContent.childCount - 1).GetComponent<Button>() : skillContent.GetChild(i - 1).GetComponent<Button>();
                        Button downTarget = (i == skillContent.childCount - 1) ? skillContent.GetChild(0).GetComponent<Button>() : skillContent.GetChild(i + 1).GetComponent<Button>();

                        nav.selectOnUp = upTarget;
                        nav.selectOnDown = downTarget;
                        nav.selectOnLeft = skillBtn;
                        nav.selectOnRight = skillBtn;
                        skillBtn.navigation = nav;
                    }
                }
            }

            if (confirmButton.gameObject.activeInHierarchy && confirmButton.interactable)
            {
                Navigation nav = confirmButton.navigation;
                nav.selectOnLeft = confirmButton;
                nav.selectOnRight = confirmButton;
                nav.selectOnUp = confirmButton;
                nav.selectOnDown = confirmButton;
                confirmButton.navigation = nav;
            }
        }

        private void LinkVerticalNavigation(List<Button> column, Button bottomButton)
        {
            List<Button> fullCol = new List<Button>(column);
            if (bottomButton != null) fullCol.Add(bottomButton);

            if (fullCol.Count == 0) return;

            for (int i = 0; i < fullCol.Count; i++)
            {
                Navigation nav = fullCol[i].navigation;
                nav.mode = Navigation.Mode.Explicit;

                Button upTarget = fullCol[(i - 1 + fullCol.Count) % fullCol.Count];
                Button downTarget = fullCol[(i + 1) % fullCol.Count];

                nav.selectOnUp = upTarget;
                nav.selectOnDown = downTarget;

                fullCol[i].navigation = nav;
            }
        }

        // 상태에 맞춰 똑똑하게 포커스를 이동해주는 함수
        private IEnumerator SelectFirstAvailableButton()
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            
            if (currentSection == FocusSection.Stat)
            {
                // 스탯으로 돌아왔는데 가용 포인트가 0이면 취소 버튼 중 하나 선택
                if (availablePoints == 0)
                {
                    if (strRow.downButton.interactable) strRow.downButton.Select();
                    else if (magRow.downButton.interactable) magRow.downButton.Select();
                    else if (intRow.downButton.interactable) intRow.downButton.Select();
                    else if (vitRow.downButton.interactable) vitRow.downButton.Select();
                    else if (agiRow.downButton.interactable) agiRow.downButton.Select();
                    else if (lucRow.downButton.interactable) lucRow.downButton.Select();
                }
                else
                {
                    strRow.upButton.Select();
                }
            }
            else if (currentSection == FocusSection.Skill)
            {
                // 스킬 섹션 진입 시 스킬이 있으면 스킬 선택, 없으면 Confirm 선택
                if (skillContent.childCount > 0)
                {
                    var firstSkillBtn = skillContent.GetChild(0).GetComponent<Button>();
                    if (firstSkillBtn != null) firstSkillBtn.Select();
                }
                else
                {
                    confirmButton.Select();
                }
            }
        }

        private void Update()
        {
            // 포커스 강제 유지 로직 (마우스 클릭 방어)
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                // 포커스가 살아있다면 계속 기록해 둠
                lastSelectedObject = EventSystem.current.currentSelectedGameObject;
            }
            else
            {
                // 포커스가 날아갔다면, 강제로 이전 포커스 복구
                if (lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                }
            }

            // 스킬 리스트가 포커스 되어 있는 상태에서 취소 처리
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentSection == FocusSection.Skill && availablePoints == 0)
                {
                    Manager.SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    SetSectionFocus(FocusSection.Stat);
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }
            }

            // ConfirmButton 표시 중일 때의 확인 키를 통한 진행 처리
            if (currentSection == FocusSection.Skill && availablePoints == 0)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    GameObject currentObj = EventSystem.current.currentSelectedGameObject;
                    bool isSkillFocused = currentObj != null && currentObj.transform.IsChildOf(skillContent);
                    bool isConfirmFocused = currentObj == confirmButton.gameObject;

                    // 스킬 슬롯에 포커스 되어 있을 때 스페이스바를 누르면 강제로 confirmButton 클릭 처리
                    if (isSkillFocused || isConfirmFocused)
                    {
                        confirmButton.onClick.Invoke();
                        return;
                    }
                }
            }

            // 방향키 롱프레스 처리 로직
            bool isUpPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            bool isDownPressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);

            if ((!isUpPressed && !isDownPressed) || (isUpPressed && isDownPressed))
            {
                inputTimer = 0f;
                isKeyHeld = false;
                return;
            }

            if (!isKeyHeld)
            {
                isKeyHeld = true;
                inputTimer = inputDelay; 
            }
            else
            {
                inputTimer -= Time.deltaTime;

                if (inputTimer <= 0f)
                {
                    inputTimer = inputRepeatRate; 

                    GameObject currentObj = EventSystem.current.currentSelectedGameObject;
                    if (currentObj != null)
                    {
                        Selectable currentSelectable = currentObj.GetComponent<Selectable>();
                        if (currentSelectable != null)
                        {
                            Selectable nextSelectable = null;

                            if (isUpPressed) nextSelectable = currentSelectable.navigation.selectOnUp;
                            else if (isDownPressed) nextSelectable = currentSelectable.navigation.selectOnDown;

                            if (nextSelectable != null && nextSelectable.interactable)
                            {
                                nextSelectable.Select();
                            }
                        }
                    }
                }
            }
        }

        private void OnConfirmClicked()
        {
            Manager.SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            currentTarget.sourceData.stats.str += allocatedStats.str;
            currentTarget.sourceData.stats.mag += allocatedStats.mag;
            currentTarget.sourceData.stats.intel += allocatedStats.intel;
            currentTarget.sourceData.stats.vit += allocatedStats.vit;
            currentTarget.sourceData.stats.agi += allocatedStats.agi;
            currentTarget.sourceData.stats.luc += allocatedStats.luc;

            currentTarget.maxHp = currentTarget.sourceData.maxHp = currentTarget.sourceData.stats.vit * 20;
            currentTarget.maxMp = currentTarget.sourceData.maxMp = currentTarget.sourceData.stats.mag * 30;
            
            currentTarget.currentHp = currentTarget.sourceData.currentHp = currentTarget.maxHp;
            currentTarget.currentMp = currentTarget.sourceData.currentMp = currentTarget.maxMp;

            ProcessNextCharacter();
        }
    }
}