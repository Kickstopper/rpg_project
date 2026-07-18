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

        private enum FocusSection { RandomStats, Stat, Skill }
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


        // 캐릭터 생성용
        public void ShowForCreation(string characterID, int bonusPoints, System.Action<StatData> onFinished)
        {
            isCreationMode = true;
            LeveUpUI.SetActive(true);
            
            var characterData  = ManagerRoot.Party.GetCharacterByID(characterID);

            this.onCreationFinished = onFinished;
            this.availablePoints = bonusPoints;
            
            // 데이터 복사 및 초기화
            this.baseStats = characterData.stats;
            this.allocatedStats = new StatData(); 

            // UI 텍스트 초기화
            nameText.text = characterData.name;
            levelText.text = $"LV{characterData.stats.level}";
            nextExpText.text = "";
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
            
            int nextExp = BattleCalculator.GetMaxExpForLevel(baseStats.level);
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
            
            int nextExp = BattleCalculator.GetMaxExpForLevel(currentTarget.sourceData.stats.level);
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
            // 랜덤 영역이거나 스탯 영역일 때 STATS 타이틀 노란색 유지
            if (statText != null) statText.color = (section == FocusSection.Stat || section == FocusSection.RandomStats) ? Color.gold : Color.white;
            if (skillText != null) skillText.color = (section == FocusSection.Skill) ? Color.gold : Color.white;

            CanvasGroup skillGroup = skillContent.GetComponent<CanvasGroup>();
            if (skillGroup == null) skillGroup = skillContent.gameObject.AddComponent<CanvasGroup>();

            skillGroup.interactable = (section == FocusSection.Skill);
            skillGroup.blocksRaycasts = (section == FocusSection.Skill);
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

        private void OnRandomButtonClicked()
        {
            if (availablePoints <= 0) return;

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            // 잔여 포인트가 0이 될 때까지 랜덤한 스탯에 1씩 분배
            while (availablePoints > 0)
            {
                // 0~5는 STR부터 LUC까지 매칭
                StatType randomStat = (StatType)Random.Range(0, 6);
                AddAllocatedStat(randomStat, 1);
                availablePoints--;
            }

            RefreshUI();

            // 분배 완료 후 스킬 섹션으로 포커스 이동
            SetSectionFocus(FocusSection.Skill);
            StartCoroutine(SelectFirstAvailableButton());
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

                if (currentSection == FocusSection.Skill)
                {
                    SetSectionFocus(FocusSection.Stat);
                }
            }
            // 값의 증감은 없지만, 좌/우 방향키 조작 시 하이라이트만 자연스럽게 이동시켜주는 처리
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

                // 포인트가 0이 되면 스킬 섹션으로 포커스 이동
                if (availablePoints == 0 && currentSection == FocusSection.Stat)
                {
                    SetSectionFocus(FocusSection.Skill);
                    StartCoroutine(SelectFirstAvailableButton());
                }
                else if (currentSection == FocusSection.Stat)
                {
                    // 변경된 스탯 방향(up/down)의 버튼으로 포커스 시각적 유지
                    StatUIRow row = GetStatRow(type);
                    Button targetBtn = (amount > 0) ? row.upButton : row.downButton;

                    // 방금 누른 버튼이 비활성화(interactable=false) 상태가 되었다면 반대편 버튼을 강제 지정
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
                    nav.selectOnRight = null; // 수정됨
                    nav.selectOnLeft = null;  // 수정됨
                    downBtn.navigation = nav;
                }

                if (upBtn != null)
                {
                    Navigation nav = upBtn.navigation;
                    nav.selectOnLeft = null;  // 수정됨
                    nav.selectOnRight = null; // 수정됨
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
                        
                        // 맨 위 스킬에서 위를 누르면 스탯 영역(취소 버튼)으로 탈출하도록 연결
                        Button upTarget;
                        if (i == 0) 
                        {
                            upTarget = (activeDownBtns.Count > 0) ? activeDownBtns[activeDownBtns.Count - 1] : confirmButton;
                        }
                        else 
                        {
                            upTarget = skillContent.GetChild(i - 1).GetComponent<Button>();
                        }

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
                nav.mode = Navigation.Mode.Explicit;

                // 확인 버튼에서 '위(Up)'를 눌렀을 때 이동할 목표 지정
                Button upTarget = null;
                if (skillContent.childCount > 0)
                {
                    // 스킬이 있다면 마지막 스킬로 이동
                    upTarget = skillContent.GetChild(skillContent.childCount - 1).GetComponent<Button>();
                }
                else if (activeDownBtns.Count > 0)
                {
                    // 스킬이 없다면, 방금 랜덤으로 분배되어 활성화된 스탯 내림 버튼 중 가장 아래쪽 버튼으로 이동
                    upTarget = activeDownBtns[activeDownBtns.Count - 1]; 
                }

                nav.selectOnLeft = confirmButton;
                nav.selectOnRight = confirmButton;
                nav.selectOnUp = (upTarget != null) ? upTarget : confirmButton;// 확인 버튼 갇힘 방지
                nav.selectOnDown = confirmButton;
                confirmButton.navigation = nav;
            }

            if (randomButton != null && randomButton.interactable && randomButton.gameObject.activeInHierarchy)
            {
                Navigation randNav = randomButton.navigation;
                randNav.mode = Navigation.Mode.Explicit;

                // STR 라인의 가장 우선순위 높은 버튼을 아래 방향 타겟으로 지정
                Button topUpBtn = (strRow.upButton.interactable && strRow.upButton.gameObject.activeInHierarchy) ? strRow.upButton : null;
                Button topDownBtn = (strRow.downButton.interactable && strRow.downButton.gameObject.activeInHierarchy) ? strRow.downButton : null;
                Button firstTarget = topUpBtn != null ? topUpBtn : topDownBtn;

                randNav.selectOnDown = firstTarget;
                randNav.selectOnUp = randomButton; // 맨 위이므로 위로 갈 곳이 없게 설정
                randNav.selectOnLeft = randomButton;
                randNav.selectOnRight = randomButton;
                randomButton.navigation = randNav;

                // STR 버튼 위쪽으로 RANDOM 버튼을 연결
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
            if (LeveUpUI != null && !LeveUpUI.activeSelf) return;
            
            // 생성 모드가 아닐 때만 전투 상태 체크
            if (!isCreationMode && ManagerRoot.GameState.CurrentState != GameState.Battle) return;
            
            // 포커스 강제 유지 로직 (마우스 클릭 방어)
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                Selectable sel = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
                if (sel != null && sel.interactable)
                {
                    // 포커스가 살아있다면 계속 기록해 둠
                    lastSelectedObject = EventSystem.current.currentSelectedGameObject;
                }
            }
            else
            {
                // 포커스가 날아갔다면, 강제로 이전 포커스 복구
                if (lastSelectedObject != null && lastSelectedObject.activeInHierarchy)
                {
                    Selectable sel = lastSelectedObject.GetComponent<Selectable>();
                    if (sel != null && sel.interactable) // 비활성화된 버튼(RANDOM 등)은 복구하지 않음
                    {
                        EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                    }
                }
            }

            GameObject currentObj = EventSystem.current.currentSelectedGameObject;
            if (currentObj != null)
            {
                // 현재 포커스된 오브젝트가 RANDOM 버튼일 경우
                if (currentObj == randomButton.gameObject && currentSection != FocusSection.RandomStats)
                {
                    SetSectionFocus(FocusSection.RandomStats);
                }
                // 현재 포커스된 오브젝트가 스탯(STR~LUC) 중 하나일 경우
                else if (GetFocusedStatType(currentObj).HasValue && currentSection != FocusSection.Stat)
                {
                    SetSectionFocus(FocusSection.Stat);
                }
            }

            // 스킬 리스트가 포커스 되어 있는 상태에서 취소 처리
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape) || UI.Common.GameInput.GetCancelDown())
            {
                GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

                // 현재 포커스가 ConfirmButton에 있는 경우 스킬(있으면) 또는 스탯으로
                if (currentSelected == confirmButton.gameObject)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    EventSystem.current.SetSelectedGameObject(null); 
                    lastSelectedObject = null; // 복구 방지
                    
                    if (skillContent.childCount > 0) SetSectionFocus(FocusSection.Skill);
                    else SetSectionFocus(FocusSection.Stat);
                    
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }

                // 현재 포커스가 스킬 리스트 중 하나인 경우 스탯으로
                if (currentSection == FocusSection.Skill)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    EventSystem.current.SetSelectedGameObject(null); 
                    lastSelectedObject = null; // [핵심 추가] 강제 복구 방지
                    
                    SetSectionFocus(FocusSection.Stat);
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }

                // 현재 포커스가 스탯 영역인 경 분배된 스탯 롤백 후 RANDOM으로
                if (currentSection == FocusSection.Stat)
                {
                    ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
                    
                    int totalAllocated = allocatedStats.str + allocatedStats.mag + allocatedStats.intel + allocatedStats.vit + allocatedStats.agi + allocatedStats.luc;
                    if (totalAllocated > 0)
                    {
                        UndoAllAllocatedStats(); // 할당된 스탯 롤백 및 UI 갱신
                    }

                    EventSystem.current.SetSelectedGameObject(null); 
                    lastSelectedObject = null;
                    
                    SetSectionFocus(FocusSection.RandomStats);
                    StartCoroutine(SelectFirstAvailableButton());
                    return;
                }
            }

            // ConfirmButton 표시 중일 때의 확인 키를 통한 진행 처리
            if (currentSection == FocusSection.Skill && availablePoints == 0)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    currentObj = EventSystem.current.currentSelectedGameObject;
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
                            // 방향키 입력에 따라 -1 또는 +1 실행
                            if (isLeftPressed) ChangeStat(focusedType.Value, -1);
                            else if (isRightPressed) ChangeStat(focusedType.Value, 1);
                            
                            // 좌/우 입력 후 상하 이동 타이머가 꼬이지 않도록 초기화
                            inputTimer = 0f;
                            isKeyHeld = false;
                            return; 
                        }
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
            allocatedStats = new StatData(); // 할당 스탯 데이터 초기화
            RefreshUI();
        }

        private void OnConfirmClicked()
        {
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);

            if (isCreationMode)
            {
                // 생성 모드일 경우 델리게이트로 최종 할당된 스탯만 전달 후 UI 종료
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