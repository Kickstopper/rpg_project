using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Controller;
using Data;
using Helper;
using System.Collections;
using Manager;

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
        public StatUIRow strRow;
        public StatUIRow magRow;
        public StatUIRow intRow;
        public StatUIRow vitRow;
        public StatUIRow agiRow;
        public StatUIRow lucRow;

        [Header("Skill UI")]
        public Transform skillContent;      
        public GameObject skillSlotPrefab;   // [신규] 스킬 이름을 표시할 간단한 Text 프리팹

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
            // 람다식 ref 캡처 문제를 피하기 위해 Enum 방식으로 변경
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

            // UI 텍스트 갱신
            if (currentTarget.portraitImage != null) portraitImage.sprite = currentTarget.portraitImage.sprite;
            nameText.text = currentTarget.entityName;
            levelText.text = $"LV {oldLevel} ▶ {currentTarget.sourceData.stats.level}";
            
            int nextExp = BattleCalculator.GetMaxExpForLevel(currentTarget.sourceData.stats.level);
            nextExpText.text = $"NEXT {nextExp - currentTarget.sourceData.currentExp}";

            PopulateSkillList();
            RefreshUI();

            // 처음 창이 켜졌을 때 첫 번째 활성화된 UP 버튼에 포커스
            StartCoroutine(SelectFirstAvailableButton());
        }

        private void PopulateSkillList()
        {
            // 기존에 생성된 스킬 목록 삭제
            foreach (Transform child in skillContent) Destroy(child.gameObject);

            // 현재 캐릭터가 배운 스킬만 리스트에 추가
            foreach (string skillId in currentTarget.learnedSkillIds)
            {
                var skillData = DatabaseManager.Instance.GetSkill(skillId);
                if (skillData != null && skillSlotPrefab != null)
                {
                    GameObject go = Instantiate(skillSlotPrefab, skillContent);
                    var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null) txt.text = skillData.dataName;
                }
            }
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
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            }
            else if (amount < 0 && currentAllocated > 0)
            {
                AddAllocatedStat(type, -1);
                availablePoints += 1;
                isChanged = true;
                SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            }

            if (isChanged)
            {
                // 방금 누른 버튼이 비활성화되었을 때 포커스가 날아가는 것을 방지
                GameObject lastSelected = EventSystem.current.currentSelectedGameObject;
                RefreshUI();

                if (lastSelected != null)
                {
                    Selectable sel = lastSelected.GetComponent<Selectable>();
                    if (sel != null && !sel.interactable)
                    {
                        if (availablePoints == 0) confirmButton.Select();
                        else strRow.upButton.Select(); // 기본 위치로 복귀
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

            // 업데이트된 스탯에 맞춰 HP 및 MP도 재계산
            int previewMaxHp = (baseStats.vit + allocatedStats.vit) * 20;
            int previewMaxMp = (baseStats.mag + allocatedStats.mag) * 30;

            hpText.text = $"HP {previewMaxHp}/{previewMaxHp}";
            mpText.text = $"MP {previewMaxMp}/{previewMaxMp}";

            confirmButton.gameObject.SetActive(availablePoints == 0);
        }

        private void UpdateStatRow(StatUIRow row, int baseVal, int allocated)
        {
            int total = baseVal + allocated;
            row.valueText.text = total.ToString();
            if (row.slider != null)
            {
                int maxStatValue = 50;
                row.slider.minValue = 0;
                row.slider.maxValue = maxStatValue;
                row.slider.value = total; 
            }
            // 상태에 따른 상호작용 활성화/비활성화
            row.upButton.interactable = (availablePoints > 0);
            row.downButton.interactable = (allocated > 0);
        }

        private IEnumerator SelectFirstAvailableButton()
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            
            if (availablePoints == 0) confirmButton.Select();
            else strRow.upButton.Select();
        }

        private void OnConfirmClicked()
        {
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);

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