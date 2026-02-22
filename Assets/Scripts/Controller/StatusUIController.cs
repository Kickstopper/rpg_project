using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using static Data.Database.CharacterDatabase;

namespace Controller
{
    public class StatusUIController : MonoBehaviour
    {
        public PlayerMenuController menuController;
        public GameObject spiritStatusUI;
        public SpiritStatusUIController spiritUIController;

        [Header("Header Info")]
        public TextMeshProUGUI nameText;
        public Image portraitImage; // 초상화
        public TextMeshProUGUI statusFxText;
        public TextMeshProUGUI resistPhysText;
        public TextMeshProUGUI resistFireText;
        public TextMeshProUGUI resistIceText;
        public TextMeshProUGUI resistElecText;
        public TextMeshProUGUI resistForceText;
        public TextMeshProUGUI resistPsychText;
        
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI expText;
        public TextMeshProUGUI nextExpText;
        public TextMeshProUGUI alignText;
        public TextMeshProUGUI spiritText;

        [Header("Vitals (Slider + Text)")]
        public Slider hpSlider;
        public TextMeshProUGUI hpText;
        public Slider mpSlider;
        public TextMeshProUGUI mpText;

        [Header("Base Stats (Slider + Text)")]
        // Inspector에서 각 능력치에 맞는 UI를 할당하세요
        public Slider strSlider; public TextMeshProUGUI strText;
        public Slider magSlider; public TextMeshProUGUI magText;
        public Slider intSlider; public TextMeshProUGUI intText;
        public Slider vitSlider; public TextMeshProUGUI vitText;
        public Slider agiSlider; public TextMeshProUGUI agiText;
        public Slider lucSlider; public TextMeshProUGUI lucText;

        [Header("Combat Stats (Text Only)")]
        public TextMeshProUGUI atkText;
        public TextMeshProUGUI atkHitText;
        public TextMeshProUGUI gunText;
        public TextMeshProUGUI gunHitText;
        public TextMeshProUGUI defText;
        public TextMeshProUGUI evaText;
        public TextMeshProUGUI magPowText;
        public TextMeshProUGUI magFxText;

        [Header("Skills")]
        public Transform skillContent;      // ScrollView의 Content 오브젝트
        public GameObject skillItemPrefab;  // 스킬 목록에 들어갈 텍스트 프리팹

        private List<RuntimeCharacterData> partyMembers;
        private int currentIndex = 0;

        private bool hasSpirit;

        void OnEnable()
        {
            if (PartyManager.Instance != null)
            {
                partyMembers = PartyManager.Instance.partyData;
                
                if (currentIndex >= partyMembers.Count) currentIndex = 0;
                RefreshUI();
            }
        }

        void Update()
        {
            if (spiritStatusUI.activeSelf) return;
            HandleInput();
        }

        public void SetTargetCharacter(RuntimeCharacterData targetChar)
        {
            if (PartyManager.Instance == null) return;
            partyMembers = PartyManager.Instance.partyData;

            // 전달받은 캐릭터가 파티 리스트의 몇 번째 인덱스인지 찾음
            int foundIndex = partyMembers.IndexOf(targetChar);
            
            if (foundIndex != -1)
            {
                currentIndex = foundIndex;
                RefreshUI();
            }
        }

        private void HandleInput()
        {
            // Q, E 키로 캐릭터 전환
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ChangeCharacter(-1);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                ChangeCharacter(1);
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ShowSpiritStatusUI();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (menuController != null)
                {
                    menuController.CloseStatusUI();
                }
            }
        }

        private void ChangeCharacter(int direction)
        {
            if (partyMembers == null || partyMembers.Count == 0) return;

            currentIndex += direction;

            // 리스트 순환 (Loop)
            if (currentIndex < 0) currentIndex = partyMembers.Count - 1;
            else if (currentIndex >= partyMembers.Count) currentIndex = 0;

            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            RefreshUI();
        }

        private void ShowSpiritStatusUI()
        {
            if (spiritStatusUI)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                spiritStatusUI.SetActive(true);
            }
            else SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
            
        }

        public void CloseSpiritStatusUI()
        {
            if (spiritStatusUI)
            {
                spiritStatusUI.SetActive(false);
            }
            SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
        }

        public void OnClick_SpiritViewButton()
        {
            ShowSpiritStatusUI();
        }

        private void RefreshUI()
        {
            if (partyMembers == null || partyMembers.Count == 0) return;

            RuntimeCharacterData charData = partyMembers[currentIndex];

            UpdatePortraitImage(charData);

            // Header Info
            nameText.text = charData.name;
            levelText.text = charData.stats.level.ToString();
            expText.text = charData.currentExp.ToString();
            nextExpText.text = charData.GetRequiredExpForNextLevel().ToString(); 
            alignText.text = charData.align.ToString().ToUpper().Replace("_", " ");

            if (charData.statusEffect != StatusEffect.Good)
            {
                statusFxText.text = charData.statusEffect.ToString().ToUpper();
            }
            else
            {
                statusFxText.text = string.Empty; 
            }

            resistPhysText.text = ((int)charData.resistances.phys).ToString();
            resistFireText.text = ((int)charData.resistances.fire).ToString();
            resistIceText.text = ((int)charData.resistances.ice).ToString();
            resistElecText.text = ((int)charData.resistances.elec).ToString();
            resistForceText.text = ((int)charData.resistances.force).ToString();
            resistPsychText.text = ((int)charData.resistances.psyche).ToString();

            // Vitals (HP/MP)
            UpdateSliderAndText(hpSlider, hpText, charData.currentHp, charData.maxHp);
            UpdateSliderAndText(mpSlider, mpText, charData.currentMp, charData.maxMp);

            // Base Stats
            float maxStatVal = 50f;
            UpdateStat(strSlider, strText, charData.stats.str, maxStatVal);
            UpdateStat(magSlider, magText, charData.stats.mag, maxStatVal);
            UpdateStat(intSlider, intText, charData.stats.intel, maxStatVal);
            UpdateStat(vitSlider, vitText, charData.stats.vit, maxStatVal);
            UpdateStat(agiSlider, agiText, charData.stats.agi, maxStatVal);
            UpdateStat(lucSlider, lucText, charData.stats.luc, maxStatVal);

            // Combat Stats
            atkText.text = charData.GetTotalAttack().ToString();
            atkHitText.text = charData.GetHitRate().ToString();
            gunText.text = charData.GetGunAttack().ToString();     // 총 공격력
            gunHitText.text = charData.GetGunHitRate().ToString(); // 총 명중률
            defText.text = charData.GetTotalDefense().ToString();
            evaText.text = charData.GetEvasion().ToString();       // 물리 회피율
            magPowText.text = charData.GetMagicPower().ToString(); // 마법 위력
            magFxText.text = charData.GetMagicEffect().ToString(); // 마법 효과(명중률 등)

            // Spirit 및 Skills (ScrollView 갱신)
            List<string> skills = new(charData.learnedSkills);
            if (!string.IsNullOrEmpty(charData.spiritId))
            {
                SpiritData spirit = DatabaseManager.Instance.GetSpirit(charData.spiritId);
                if (spirit != null)
                {
                    hasSpirit = true;
                    spiritUIController.Initialze(spirit);
                    spiritText.text = spirit.entityName;
                    // List<SkillData> spiritSkills = spirit.skills;
                    // foreach(var skill in spiritSkills)
                    // {
                    //     if (!skills.Contains(skill.id))
                    //     {
                    //         skills.Add(skill.id);
                    //     }
                    // }
                }
                else hasSpirit = false;
            }
            else hasSpirit = false;
            UpdateSkillList(skills);
        }

        private void UpdatePortraitImage(RuntimeCharacterData data)
        {
            if (data == null || string.IsNullOrEmpty(data.characterId)) return;

            CharacterEntry entry = PartyManager.Instance.charDB.GetEntry(data.characterId);
            if (entry != null)
            {
                if (entry.portraitImage != null)
                {
                    portraitImage.sprite = entry.portraitImage;
                    portraitImage.color = Color.white;
                }
                else
                {
                    portraitImage.color = Color.black;
                }
            }
        }


        // HP/MP 슬라이더 헬퍼 함수
        private void UpdateSliderAndText(Slider slider, TextMeshProUGUI text, int current, int max)
        {
            slider.maxValue = max;
            slider.value = current;
            text.text = $"{current}/{max}";
        }

        // 스탯 슬라이더 헬퍼 함수
        private void UpdateStat(Slider slider, TextMeshProUGUI text, int value, float maxLimit)
        {
            slider.maxValue = maxLimit;
            slider.value = value;
            text.text = $"{value}/{maxLimit}";
        }

        // 스킬 리스트 갱신 함수
        private void UpdateSkillList(List<string> skills)
        {
            // 기존 목록 삭제 (초기화)
            foreach (Transform child in skillContent)
            {
                Destroy(child.gameObject);
            }

            // 스킬 목록 생성
            if (skills == null) return;

            foreach (var skillId in skills)
            {
                // 프리팹 생성
                GameObject item = Instantiate(skillItemPrefab, skillContent);

                // 데이터 조회
                SkillData skillData = DatabaseManager.Instance.GetSkill(skillId);
                if (skillData == null) continue;

                // SkillSlotUI 컴포넌트 가져오기
                SimpleListItemController slotUI = item.GetComponent<SimpleListItemController>();
                
                if (slotUI != null)
                {
                    // 이름과 코스트를 각각 설정
                    slotUI.SetData(skillData.dataName, skillData.costValue);
                }
                else
                {
                    // 만약 스크립트를 안 붙였을 경우를 대비한 안전장치
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