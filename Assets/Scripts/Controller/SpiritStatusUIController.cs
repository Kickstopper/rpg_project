using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using static Data.Database.CharacterDatabase;

namespace Controller
{
    public class SpiritStatusUIController : MonoBehaviour
    {
        public StatusUIController charStatusController;

        [Header("Header Info")]
        public TextMeshProUGUI nameText;
        public Image portraitImage; // 초상화
        public TextMeshProUGUI resistPhysText;
        public TextMeshProUGUI resistFireText;
        public TextMeshProUGUI resistIceText;
        public TextMeshProUGUI resistElecText;
        public TextMeshProUGUI resistForceText;
        public TextMeshProUGUI resistPsychText;
        
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI alignText;

        [Header("Base Stats (Slider + Text)")]
        // Inspector에서 각 능력치에 맞는 UI를 할당하세요
        public Slider strSlider; public TextMeshProUGUI strText;
        public Slider magSlider; public TextMeshProUGUI magText;
        public Slider intSlider; public TextMeshProUGUI intText;
        public Slider vitSlider; public TextMeshProUGUI vitText;
        public Slider agiSlider; public TextMeshProUGUI agiText;
        public Slider lucSlider; public TextMeshProUGUI lucText;

        [Header("Skills")]
        public Transform skillContent;      // ScrollView의 Content 오브젝트
        public GameObject skillItemPrefab;  // 스킬 목록에 들어갈 텍스트 프리팹

        void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                Close();
            }
        }

        private void Close()
        {
            if (charStatusController != null)
            {
                charStatusController.CloseSpiritStatusUI();
            }
        }

        public void OnClick_CloseButton()
        {
            Close();
        }

        public void Initialze(SpiritData data)
        {
            UpdatePortraitImage(data);

            // 1. Header Info
            nameText.text = data.entityName;
            levelText.text = data.stats.level.ToString();
            alignText.text = data.align.ToString().ToUpper().Replace("_", " ");

            resistPhysText.text = ((int)data.resistances.phys).ToString();
            resistFireText.text = ((int)data.resistances.fire).ToString();
            resistIceText.text = ((int)data.resistances.ice).ToString();
            resistElecText.text = ((int)data.resistances.elec).ToString();
            resistForceText.text = ((int)data.resistances.force).ToString();
            resistPsychText.text = ((int)data.resistances.psyche).ToString();

            // 2. Base Stats
            float maxStatVal = 50f;
            UpdateStat(strSlider, strText, data.stats.str, maxStatVal);
            UpdateStat(magSlider, magText, data.stats.mag, maxStatVal);
            UpdateStat(intSlider, intText, data.stats.intel, maxStatVal);
            UpdateStat(vitSlider, vitText, data.stats.vit, maxStatVal);
            UpdateStat(agiSlider, agiText, data.stats.agi, maxStatVal);
            UpdateStat(lucSlider, lucText, data.stats.luc, maxStatVal);

            // 3. Spirit 및 Skills (ScrollView 갱신)
            UpdateSkillList(data.skills);
        }

        private void UpdatePortraitImage(SpiritData data)
        {
            if (data == null || data.portraitImage == null) return;

            if (data.portraitImage != null)
            {
                portraitImage.sprite = data.portraitImage;
                portraitImage.color = Color.white;
            }
            else
            {
                portraitImage.color = Color.black;
            }
        }

        // 스탯 슬라이더 헬퍼 함수
        private void UpdateStat(Slider slider, TextMeshProUGUI text, int value, float maxLimit)
        {
            slider.maxValue = maxLimit;
            slider.value = value;
            text.text = $"{value}/{maxLimit}";
        }

        // 스킬 리스트 갱신 함수
        private void UpdateSkillList(List<SkillData> skills)
        {
            // 1. 기존 목록 삭제 (초기화)
            foreach (Transform child in skillContent)
            {
                Destroy(child.gameObject);
            }

            // 2. 스킬 목록 생성
            if (skills == null) return;

            foreach (var skillData in skills)
            {
                GameObject item = Instantiate(skillItemPrefab, skillContent);
                SimpleListItemController slotUI = item.GetComponent<SimpleListItemController>();
                
                if (slotUI != null)
                {
                    slotUI.SetData(skillData.dataName, skillData.costValue);
                }
                else
                {
                    TextMeshProUGUI itemText = item.GetComponentInChildren<TextMeshProUGUI>();
                    if (itemText != null)
                    {
                        itemText.text = $"{skillData.dataName} (MP {skillData.costValue})";
                    }
                }
            }
        }
    }
}