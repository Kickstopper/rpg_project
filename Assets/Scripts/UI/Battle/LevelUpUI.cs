using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Data;
using Helper;
using System.Collections;
using DG.Tweening;
using Manager;

namespace UI.Battle
{
    public class LevelUpUI : MonoBehaviour
    {
        [Header("Creation Mode")]
        public bool isCreationMode = false;
        private System.Action<StatData> onCreationFinished;

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
        public TextMeshProUGUI skillTitleText;
        public TextMeshProUGUI skillChanceText;

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
        public Button randomButton;
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

        // 불필요한 Skill 단계를 제거했습니다.
        private enum FocusSection { RandomStats, Stat, NewSkillSelect }
        private FocusSection currentSection = FocusSection.Stat;
        
        // 해금 조건을 만족한 스킬들을 순서대로 담아둘 큐
        private Queue<SkillUnlockNode> pendingSkillNodes = new Queue<SkillUnlockNode>();

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


        // 캐릭터 생성용
        public void ShowForCreation(string characterID, int bonusPoints, System.Action<StatData> onFinished)
        {
            isCreationMode = true;
            LeveUpUI.SetActive(true);
            
            var characterData  = ManagerRoot.Database.charDB.GetEntry(characterID);

            this.onCreationFinished = onFinished;
            this.availablePoints = bonusPoints;
            
            // 데이터 복사 및 초기화
            this.baseStats = characterData.stats;
            this.allocatedStats = new StatData(); 

            // UI 텍스트 초기화
            nameText.text = characterData.name;
            levelText.text = $"LV.{characterData.stats.level}";

            if (portraitImage != null)
            {
                var dbEntry = ManagerRoot.Database.charDB.GetEntry(characterID);
                if (dbEntry != null && dbEntry.portraitImage != null)
                {
                    portraitImage.sprite = dbEntry.portraitImage;
                    portraitImage.color = Color.white;
                }
                else portraitImage.color = Color.clear;
            }
            
            int nextExp = BattleCalculator.GetMaxExpForLevel(baseStats.level, characterData.race, characterData.gender);
            nextExpText.text = $"NEXT EXP {nextExp}";

            // 스킬 리스트 비우기
            foreach (Transform child in skillContent) Destroy(child.gameObject);
            ClearDescription();

            BindButtons();
            SetSectionFocus(FocusSection.RandomStats);
            RefreshUI();
            
            StartCoroutine(SelectFirstAvailableButton());
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
            if (randomButton != null)
            {
                randomButton.onClick.RemoveAllListeners();
                randomButton.onClick.AddListener(OnRandomButtonClicked);
            }

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
            
            if (portraitImage != null)
            {
                Sprite portrait = null;
                if (!currentTarget.sourceData.isMonster)
                {
                    var chrEntry = ManagerRoot.Database.charDB.GetEntry(currentTarget.sourceData.characterId);
                    if (chrEntry != null) portrait = chrEntry.portraitImage;
                }
                else
                {
                    var monEntry = ManagerRoot.Database.monsterDB.GetEntry(currentTarget.sourceData.characterId);
                    if (monEntry != null) portrait = monEntry.portrait;
                }
                
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.color = Color.white;
                }
                else portraitImage.color = Color.clear;
            }
            
            nameText.text = currentTarget.entityName;
            levelText.text = $"LV {oldLevel} -> {currentTarget.sourceData.stats.level}";
            skillTitleText.text = "SKILLS";
            skillChanceText.text = "";

            // 레벨, 종족, 성별 전달
            int nextExp = BattleCalculator.GetMaxExpForLevel(
                currentTarget.sourceData.stats.level, 
                currentTarget.sourceData.race, 
                currentTarget.sourceData.gender
            );
            nextExpText.text = $"NEXT EXP {nextExp - currentTarget.sourceData.currentExp}";

            PopulateSkillList();

            // 초기화 시 랜덤 스탯 섹션으로 포커스
            SetSectionFocus(FocusSection.RandomStats);
            RefreshUI();
            
            StartCoroutine(SelectFirstAvailableButton());
        }

        private void SetSectionFocus(FocusSection section)
        {
            currentSection = section;
            // 랜덤 영역이거나 스탯 영역일 때 STATS 타이틀 유지
            if (statText != null) statText.color = (section == FocusSection.Stat || section == FocusSection.RandomStats) ? Color.gold : Color.white;
            
            // 스킬 영역은 보상 선택 모드(NewSkillSelect)일 때만 활성화
            if (skillText != null) skillText.color = (section == FocusSection.NewSkillSelect) ? Color.gold : Color.white;

            CanvasGroup skillGroup = skillContent.GetComponent<CanvasGroup>();
            if (skillGroup == null) skillGroup = skillContent.gameObject.AddComponent<CanvasGroup>();

            skillGroup.interactable = (section == FocusSection.NewSkillSelect);
            skillGroup.blocksRaycasts = (section == FocusSection.NewSkillSelect);
        }

        private void PopulateSkillList()
        {
            foreach (Transform child in skillContent) Destroy(child.gameObject);
            ClearDescription();

            foreach (string skillId in currentTarget.learnedSkillIds)
            {
                var skillData = ManagerRoot.Database.GetSkill(skillId);
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

            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((data) => { 
                ShowDescription(description); 
                ScrollToSlot(go.GetComponent<RectTransform>()); 
            });
            trigger.triggers.Add(selectEntry);

            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
            pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
            pointerEnterEntry.callback.AddListener((data) => { 
                ShowDescription(description); 
                ScrollToSlot(go.GetComponent<RectTransform>());
            });
            trigger.triggers.Add(pointerEnterEntry);

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

            float targetY = Mathf.Abs(target.anchoredPosition.y);
            float newY = targetY - (scrollRect.viewport.rect.height / 2f);

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

        private void OnRandomButtonClicked()
        {
            if (availablePoints <= 0) return;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            while (availablePoints > 0)
            {
                StatType randomStat = (StatType)Random.Range(0, 6);
                AddAllocatedStat(randomStat, 1);
                availablePoints--;
            }

            RefreshUI();

            // 분배 완료 후 즉시 Confirm 버튼으로 포커스 이동
            SetSectionFocus(FocusSection.Stat);
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
            lastSelectedObject = confirmButton.gameObject;
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
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            }
            else if (amount < 0 && currentAllocated > 0)
            {
                AddAllocatedStat(type, -1);
                availablePoints += 1;
                isChanged = true;
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
            }
            else 
            {
                if (currentSection == FocusSection.Stat)
                {
                    StatUIRow row = GetStatRow(type);
                    Button targetBtn = (amount > 0) ? row.upButton : row.downButton;
                    if (targetBtn.interactable && targetBtn.gameObject.activeInHierarchy)
                    {
                        EventSystem.current.SetSelectedGameObject(targetBtn.gameObject);
                        lastSelectedObject = targetBtn.gameObject;
                    }
                }
            }

            if (isChanged)
            {
                RefreshUI();

                // 포인트가 0이 되면 즉시 Confirm 버튼으로 쫀득하게 이동
                if (availablePoints == 0 && currentSection == FocusSection.Stat)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                    lastSelectedObject = confirmButton.gameObject;
                }
                else if (currentSection == FocusSection.Stat)
                {
                    StatUIRow row = GetStatRow(type);
                    Button targetBtn = (amount > 0) ? row.upButton : row.downButton;

                    if (!targetBtn.interactable)
                    {
                        targetBtn = (amount > 0) ? row.downButton : row.upButton;
                    }

                    if (targetBtn != null && targetBtn.interactable && targetBtn.gameObject.activeInHierarchy)
                    {
                        EventSystem.current.SetSelectedGameObject(targetBtn.gameObject);
                        lastSelectedObject = targetBtn.gameObject;
                    }
                    else
                    {
                        StartCoroutine(SelectFirstAvailableButton());
                    }
                }
            }
        }

        private StatType? GetFocusedStatType(GameObject obj)
        {
            if (obj == strRow.upButton.gameObject || obj == strRow.downButton.gameObject) return StatType.STR;
            if (obj == magRow.upButton.gameObject || obj == magRow.downButton.gameObject) return StatType.MAG;
            if (obj == intRow.upButton.gameObject || obj == intRow.downButton.gameObject) return StatType.INT;
            if (obj == vitRow.upButton.gameObject || obj == vitRow.downButton.gameObject) return StatType.VIT;
            if (obj == agiRow.upButton.gameObject || obj == agiRow.downButton.gameObject) return StatType.AGI;
            if (obj == lucRow.upButton.gameObject || obj == lucRow.downButton.gameObject) return StatType.LUC;
            return null;
        }

        private StatUIRow GetStatRow(StatType type)
        {
            switch (type) {
                case StatType.STR: return strRow;
                case StatType.MAG: return magRow;
                case StatType.INT: return intRow;
                case StatType.VIT: return vitRow;
                case StatType.AGI: return agiRow;
                case StatType.LUC: return lucRow;
                default: return strRow;
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

            int previewMaxHp = BattleCalculator.GetMaxHP(baseStats.level, 
                                                         baseStats.str + allocatedStats.str,
                                                         baseStats.vit + allocatedStats.vit);
            int previewMaxMp = BattleCalculator.GetMaxMP(baseStats.level,
                                                         baseStats.mag + allocatedStats.mag,
                                                         baseStats.intel + allocatedStats.intel);

            hpText.text = $"HP {previewMaxHp}/{previewMaxHp}";
            mpText.text = $"MP {previewMaxMp}/{previewMaxMp}";
            if (randomButton != null)
            {
                randomButton.interactable = (availablePoints > 0);
            }
            confirmButton.gameObject.SetActive(availablePoints == 0);
            UpdateNavigation();
        }

        private void UpdateStatRow(StatUIRow row, int baseVal, int allocated)
        {
            int maxStatValue = 40;
            int total = baseVal + allocated;
            if (total > maxStatValue) total = maxStatValue;
            row.valueText.text = total.ToString();
            if (row.slider != null) row.slider.value = (float)total / maxStatValue; 

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
                    nav.selectOnRight = null; 
                    nav.selectOnLeft = null;  
                    downBtn.navigation = nav;
                }

                if (upBtn != null)
                {
                    Navigation nav = upBtn.navigation;
                    nav.selectOnLeft = null;  
                    nav.selectOnRight = null; 
                    upBtn.navigation = nav;
                }
            }

            // 스킬 슬롯 세로 이동 연결 블록을 완전히 제거했습니다.
            
            if (confirmButton.gameObject.activeInHierarchy && confirmButton.interactable)
            {
                Navigation nav = confirmButton.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // 스킬을 거치지 않고, 확인 버튼 위쪽은 무조건 스탯 내림 버튼으로 직접 연결됩니다.
                Button upTarget = null;
                if (activeDownBtns.Count > 0)
                {
                    upTarget = activeDownBtns[activeDownBtns.Count - 1]; 
                }

                nav.selectOnLeft = confirmButton;
                nav.selectOnRight = confirmButton;
                nav.selectOnUp = (upTarget != null) ? upTarget : confirmButton;
                nav.selectOnDown = confirmButton;
                confirmButton.navigation = nav;
            }

            if (randomButton != null && randomButton.interactable && randomButton.gameObject.activeInHierarchy)
            {
                Navigation randNav = randomButton.navigation;
                randNav.mode = Navigation.Mode.Explicit;

                Button topUpBtn = (strRow.upButton.interactable && strRow.upButton.gameObject.activeInHierarchy) ? strRow.upButton : null;
                Button topDownBtn = (strRow.downButton.interactable && strRow.downButton.gameObject.activeInHierarchy) ? strRow.downButton : null;
                Button firstTarget = topUpBtn != null ? topUpBtn : topDownBtn;

                randNav.selectOnDown = firstTarget;
                randNav.selectOnUp = randomButton; 
                randNav.selectOnLeft = randomButton;
                randNav.selectOnRight = randomButton;
                randomButton.navigation = randNav;

                if (topUpBtn != null)
                {
                    Navigation upNav = topUpBtn.navigation;
                    upNav.selectOnUp = randomButton;
                    topUpBtn.navigation = upNav;
                }
                if (topDownBtn != null)
                {
                    Navigation downNav = topDownBtn.navigation;
                    downNav.selectOnUp = randomButton;
                    topDownBtn.navigation = downNav;
                }
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

        private IEnumerator SelectFirstAvailableButton()
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            
            if (currentSection == FocusSection.RandomStats)
            {
                if (randomButton != null && randomButton.interactable && randomButton.gameObject.activeInHierarchy)
                {
                    randomButton.Select();
                }
                else
                {
                    SetSectionFocus(FocusSection.Stat);
                    StartCoroutine(SelectFirstAvailableButton());
                }
            }
            else if (currentSection == FocusSection.Stat)
            {
                // 스탯 영역에서 가용 포인트가 0일 때의 포커스 복구 로직
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
        }

        private void Update()
        {
            if (LeveUpUI != null && !LeveUpUI.activeSelf) return;
            if (!isCreationMode && ManagerRoot.GameState.CurrentState != GameState.Battle) return;
            
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                Selectable sel = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
                if (sel != null && sel.interactable)
                {
                    lastSelectedObject = EventSystem.current.currentSelectedGameObject;
                }
            }
            else
            {
                if (lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
                {
                    Selectable sel = lastSelectedObject.GetComponent<Selectable>();
                    if (sel != null && sel.interactable) 
                    {
                        EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                    }
                }
            }

            GameObject currentObj = EventSystem.current.currentSelectedGameObject;
            if (currentObj != null)
            {
                // 스킬 선택 모드일 때는 포커스와 상태를 절대 방어
                if (currentSection == FocusSection.NewSkillSelect)
                {
                    // 마우스 클릭 등으로 스킬창 밖으로 포커스가 나갔다면 멱살 잡고 끌고 옴
                    if (!currentObj.transform.IsChildOf(skillContent))
                    {
                        if (skillContent.childCount > 0)
                        {
                            var firstSkill = skillContent.GetChild(0).gameObject;
                            EventSystem.current.SetSelectedGameObject(firstSkill);
                            lastSelectedObject = firstSkill;
                        }
                    }
                }
                else
                {
                    // 스킬 선택 모드가 아닐 때만 자유로운 포커스 이동 허용
                    if (currentObj == randomButton.gameObject && currentSection != FocusSection.RandomStats)
                    {
                        SetSectionFocus(FocusSection.RandomStats);
                    }
                    else if (GetFocusedStatType(currentObj).HasValue && currentSection != FocusSection.Stat)
                    {
                        SetSectionFocus(FocusSection.Stat);
                    }
                }
            }

            // 스킬 선택 모드에서 스페이스/엔터 키 무조건 작동 보장
            if (currentSection == FocusSection.NewSkillSelect)
            {
                if (UI.Common.GameInput.GetSelectDown())
                {
                    if (currentObj != null && currentObj.transform.IsChildOf(skillContent))
                    {
                        Button btn = currentObj.GetComponent<Button>();
                        if (btn != null) 
                        {
                            btn.onClick.Invoke();
                            return; // 중복 입력 방지
                        }
                    }
                }
            }

            if (UI.Common.GameInput.GetCancelDown())
            {
                if (currentSection == FocusSection.NewSkillSelect)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    return;
                }

                GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

                if (currentSelected == confirmButton.gameObject)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    EventSystem.current.SetSelectedGameObject(null);
                    lastSelectedObject = null;
                    
                    SetSectionFocus(FocusSection.Stat);
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }

                if (currentSection == FocusSection.Stat)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    
                    int totalAllocated = allocatedStats.str + allocatedStats.mag + allocatedStats.intel + allocatedStats.vit + allocatedStats.agi + allocatedStats.luc;
                    if (totalAllocated > 0)
                    {
                        UndoAllAllocatedStats();
                    }

                    EventSystem.current.SetSelectedGameObject(null);
                    lastSelectedObject = null;
                    
                    SetSectionFocus(FocusSection.RandomStats);
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }
            }

            if (currentSection == FocusSection.Stat)
            {
                bool isLeftPressed = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                bool isRightPressed = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);

                if (isLeftPressed || isRightPressed)
                {
                    currentObj = EventSystem.current.currentSelectedGameObject;
                    if (currentObj != null)
                    {
                        StatType? focusedType = GetFocusedStatType(currentObj);
                        if (focusedType.HasValue)
                        {
                            if (isLeftPressed) ChangeStat(focusedType.Value, -1);
                            else if (isRightPressed) ChangeStat(focusedType.Value, 1);
                            
                            inputTimer = 0f;
                            isKeyHeld = false;
                            return;
                        }
                    }
                }
            }

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

                    currentObj = EventSystem.current.currentSelectedGameObject;
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

        private void UndoAllAllocatedStats()
        {
            availablePoints += (allocatedStats.str + allocatedStats.mag + allocatedStats.intel + allocatedStats.vit + allocatedStats.agi + allocatedStats.luc);
            allocatedStats = new StatData(); 
            RefreshUI();
        }

        private bool IsConditionMet(StatData stats, SkillUnlockNode node)
        {
            if (node == null) return false;

            return stats.level >= node.reqLevel &&
                   stats.str >= node.reqStr &&
                   stats.mag >= node.reqMag &&
                   stats.intel >= node.reqInt &&
                   stats.vit >= node.reqVit &&
                   stats.agi >= node.reqAgi &&
                   stats.luc >= node.reqLuc;
        }

        private List<string> GetUnlearnedSkills(SkillUnlockNode node, PlayerController character)
        {
            List<string> unlearned = new List<string>();
            if (node.rewardSkillChoices == null) return unlearned;

            foreach (string skillId in node.rewardSkillChoices)
            {
                if (!character.learnedSkillIds.Contains(skillId))
                {
                    unlearned.Add(skillId);
                }
            }
            return unlearned;
        }

        private void OnConfirmClicked()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            if (isCreationMode)
            {
                StatData finalStats = new StatData
                {
                    level = baseStats.level,
                    str = baseStats.str + allocatedStats.str,
                    mag = baseStats.mag + allocatedStats.mag,
                    intel = baseStats.intel + allocatedStats.intel,
                    vit = baseStats.vit + allocatedStats.vit,
                    agi = baseStats.agi + allocatedStats.agi,
                    luc = baseStats.luc + allocatedStats.luc
                };
                
                LeveUpUI.SetActive(false);
                isCreationMode = false;
                onCreationFinished?.Invoke(finalStats);
                return;
            }

            StatData newStats = new StatData {
                level = currentTarget.sourceData.stats.level,
                str = baseStats.str + allocatedStats.str,
                mag = baseStats.mag + allocatedStats.mag,
                intel = baseStats.intel + allocatedStats.intel,
                vit = baseStats.vit + allocatedStats.vit,
                agi = baseStats.agi + allocatedStats.agi,
                luc = baseStats.luc + allocatedStats.luc
            };

            currentTarget.sourceData.stats.str = newStats.str;
            currentTarget.sourceData.stats.mag = newStats.mag;
            currentTarget.sourceData.stats.intel = newStats.intel;
            currentTarget.sourceData.stats.vit = newStats.vit;
            currentTarget.sourceData.stats.agi = newStats.agi;
            currentTarget.sourceData.stats.luc = newStats.luc;

            currentTarget.maxHp = currentTarget.sourceData.maxHp = currentTarget.sourceData.stats.vit * 20;
            currentTarget.maxMp = currentTarget.sourceData.maxMp = currentTarget.sourceData.stats.mag * 30;
            currentTarget.currentHp = currentTarget.sourceData.currentHp = currentTarget.maxHp;
            currentTarget.currentMp = currentTarget.sourceData.currentMp = currentTarget.maxMp;

            pendingSkillNodes.Clear();

            var charEntry = ManagerRoot.Database.charDB.GetEntry(currentTarget.sourceData.characterId);
            if (charEntry != null && charEntry.skillTree != null)
            {
                foreach (var node in charEntry.skillTree.unlockNodes)
                {
                    bool isClaimed = currentTarget.sourceData.claimedSkillNodes.Contains(node.nodeId);
                    
                    if (!isClaimed && IsConditionMet(newStats, node))
                    {
                        List<string> availableSkills = GetUnlearnedSkills(node, currentTarget);

                        if (availableSkills.Count > 0)
                        {
                            pendingSkillNodes.Enqueue(node);
                        }
                        else
                        {
                            currentTarget.sourceData.claimedSkillNodes.Add(node.nodeId);
                        }
                    }
                }
            }

            if (pendingSkillNodes.Count > 0)
            {
                ShowNextSkillSelection();
            }
            else
            {
                ProcessNextCharacter();
            }
        }

        private void ShowNextSkillSelection()
        {
            SetSectionFocus(FocusSection.NewSkillSelect);
            
            skillTitleText.text = "CHOOSE ONE";
            skillChanceText.text = $"REMAINING: {pendingSkillNodes.Count}"; 
            
            // 확인 버튼을 끄기 전에 포커스를 날려 유니티가 스탯 버튼으로 포커스를 옮기는 것을 차단
            EventSystem.current.SetSelectedGameObject(null);
            lastSelectedObject = null;

            confirmButton.gameObject.SetActive(false); 
            
            foreach (Transform child in skillContent) Destroy(child.gameObject);
            ClearDescription();

            SkillUnlockNode currentNode = pendingSkillNodes.Peek();
            List<Button> choiceButtons = new List<Button>();

            foreach (string skillId in currentNode.rewardSkillChoices)
            {
                var skillData = ManagerRoot.Database.GetSkill(skillId);
                if (skillData != null && skillSlotPrefab != null)
                {
                    GameObject go = Instantiate(skillSlotPrefab, skillContent);
                    var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null) txt.text = skillData.dataName;

                    Button btn = go.GetComponent<Button>();
                    
                    // 좌/우 방향키를 눌러도 자기 자신에게 머물게 하여 탈출 방지
                    Navigation nav = btn.navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    nav.selectOnLeft = btn;
                    nav.selectOnRight = btn;
                    btn.navigation = nav;

                    choiceButtons.Add(btn);

                    btn.onClick.AddListener(() => OnSkillChoiceClicked(skillId));
                    AddSelectionEvent(go, skillData.description);
                }
            }

            LinkVerticalNavigation(choiceButtons, null);
            
            if (choiceButtons.Count > 0)
            {
                StartCoroutine(FocusNewSkillDelayed(choiceButtons[0].gameObject));
            }
        }

        private IEnumerator FocusNewSkillDelayed(GameObject target)
        {
            // UI 레이아웃이 완전히 구성될 때까지 2프레임 대기
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            
            if (target != null && target.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(target);
                lastSelectedObject = target;
                
                // 하이라이트 강제 활성화
                Selectable sel = target.GetComponent<Selectable>();
                if (sel != null) sel.Select();
            }
        }

        private void OnSkillChoiceClicked(string chosenSkillId)
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            SkillUnlockNode currentNode = pendingSkillNodes.Peek();

            if (!currentTarget.learnedSkillIds.Contains(chosenSkillId))
            {
                currentTarget.learnedSkillIds.Add(chosenSkillId);
                currentTarget.sourceData.learnedSkills.Add(chosenSkillId); 
            }

            if (!currentTarget.sourceData.claimedSkillNodes.Contains(currentNode.nodeId))
            {
                currentTarget.sourceData.claimedSkillNodes.Add(currentNode.nodeId);
            }

            pendingSkillNodes.Dequeue();

            if (pendingSkillNodes.Count > 0)
            {
                ShowNextSkillSelection();
            }
            else
            {
                ProcessNextCharacter();
            }
        }
    }
}