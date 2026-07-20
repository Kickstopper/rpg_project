using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using static Data.Database.CharacterDatabase;
using UI.Common;
using static MonsterDatabase;

namespace Controller
{
    public class StatusUIController : MonoBehaviour
    {
        public PlayerMenuController menuController;
        public GameObject resonanceStatusUI;
        public ResonanceStatusUIController resonanceUIController;

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
        
        public TextMeshProUGUI raceText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI expText;
        public TextMeshProUGUI nextExpText;
        public TextMeshProUGUI alignText;

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

        [Header("Battle Stats (Text Only)")]
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

        private bool hasResonance;

        void OnEnable()
        {
            if (ManagerRoot.Party != null)
            {
                partyMembers  = ManagerRoot.Party.partyData;
                
                if (currentIndex >= partyMembers.Count) currentIndex = 0;
                RefreshUI();
            }
        }

        void Update()
        {
            if (resonanceStatusUI.activeSelf) return;
            HandleInput();
        }

        public void SetTargetCharacter(RuntimeCharacterData targetChar)
        {
            if (ManagerRoot.Party == null) return;
            partyMembers  = ManagerRoot.Party.partyData;

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

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ShowresonanceStatusUI();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
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

            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
            RefreshUI();
        }

        private void ShowresonanceStatusUI()
        {
            if (hasResonance && resonanceStatusUI)
            {
                ManagerRoot.Sound.PlaySFX(SfxID.UI_Click);
                resonanceStatusUI.SetActive(true);
            }
            else ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
            
        }

        public void CloseResonanceStatusUI()
        {
            if (resonanceStatusUI)
            {
                resonanceStatusUI.SetActive(false);
            }
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cancel);
        }

        public void OnClick_ResonanceViewButton()
        {
            ShowresonanceStatusUI();
        }

        private void RefreshUI()
        {
            if (partyMembers == null || partyMembers.Count == 0) return;

            RuntimeCharacterData charData = partyMembers[currentIndex];

            UpdatePortraitImage(charData);

            // Header Info
            nameText.text = charData.name;
            raceText.text = charData.race.ToString().ToUpper();
            levelText.text = charData.stats.level.ToString();
            expText.text = charData.currentExp.ToString();
            nextExpText.text = charData.GetRequiredExpForNextLevel().ToString(); 
            alignText.text = charData.align.ToString().ToUpper().Replace("_", " ");

            if (charData.CurrentStatusEffect != null)
            {
                // 상태이상의 이름을 출력
                statusFxText.text = charData.CurrentStatusEffect.effectName.ToUpper();
            }
            else
            {
                statusFxText.text = string.Empty;
            }

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

            // Battle Stats
            UpdateBattleStatDisplay(charData);

            // resonance 및 Skills (ScrollView 갱신)
            List<string> skills = new(charData.learnedSkills);
            // if (!string.IsNullOrEmpty(charData.resonanceId))
            // {
            //     ResonanceData resonance = ManagerRoot.Database.GetResonance(charData.resonanceId);
            //     if (resonance != null)
            //     {
            //         hasResonance = true;
            //         resonanceUIController.Initialze(resonance);
            //         resonanceText.text = resonance.entityName;
            //         // List<SkillData> resonanceSkills = resonance.skills;
            //         // foreach(var skill in resonanceSkills)
            //         // {
            //         //     if (!skills.Contains(skill.id))
            //         //     {
            //         //         skills.Add(skill.id);
            //         //     }
            //         // }
            //     }
            //     else hasResonance = false;
            // }
            // else hasResonance = false;
            UpdateSkillList(skills);
        }

        private void UpdateBattleStatDisplay(RuntimeCharacterData charData)
        {
            resistPhysText.text = ((int)charData.resistances.phys).ToString();
            resistFireText.text = ((int)charData.resistances.fire).ToString();
            resistIceText.text = ((int)charData.resistances.ice).ToString();
            resistElecText.text = ((int)charData.resistances.elec).ToString();
            resistForceText.text = ((int)charData.resistances.force).ToString();
            resistPsychText.text = ((int)charData.resistances.psyche).ToString();
            
            int str = charData.stats.str;
            int vit = charData.stats.vit;
            int mag = charData.stats.mag;
            int agi = charData.stats.agi;
            int luc = charData.stats.luc;
            int intel = charData.stats.intel;
            int lv = charData.stats.level;

            WeaponData weapon = ManagerRoot.Database.GetWeapon(charData.equippedWeaponId);
            WeaponData gun = ManagerRoot.Database.GetWeapon(charData.equippedGunId);
            AmmoData ammo = ManagerRoot.Database.GetAmmo(charData.equippedAmmoId);
            
            int armorDef = 0;
            int armorEva = 0;
            foreach(var id in charData.equippedArmorIds)
            {
                var a = ManagerRoot.Database.GetArmor(id);
                if(a) { armorDef += a.defense; armorEva += a.evasionMod; }
            }

            // 기획서 공식 적용
            int atk = str + (weapon != null ? weapon.attackPower : 0) + (lv / 4);
            int hit = agi + (weapon != null ? weapon.hitRateBonus : 0) + (luc / 2) + lv;
            
            int gunAtk = 0;
            int gunHit = 0;
            if (gun != null && ammo != null)
            {
                gunAtk = gun.attackPower + ammo.damageBonus + (lv / 4);
                gunHit = gun.hitRateBonus + ammo.hitRateBonus + agi + (luc / 2) + lv;
            }

            int def = armorDef + vit + agi;
            int eva = armorEva + agi + (intel / 4) + (luc / 4) + lv;

            int magPow = (mag * 2) + (intel / 2); // MATK
            int magFx = ((mag + vit + agi) / 4) + intel + (armorDef / 4); // MDEF

            // UI 텍스트 반영
            atkText.text = atk.ToString();
            atkHitText.text = hit.ToString();
            gunText.text = gunAtk.ToString();
            gunHitText.text = gunHit.ToString();
            magPowText.text = magPow.ToString();
            magFxText.text = magFx.ToString(); 
            defText.text = def.ToString();
            evaText.text = eva.ToString();
        }

        private void UpdatePortraitImage(RuntimeCharacterData data)
        {
            if (data == null || string.IsNullOrEmpty(data.characterId)) return;
            
            Sprite portrait = null;
            if (data.isMonster)
            {
                MonsterEntry entry = ManagerRoot.Database.monsterDB.GetEntry(data.characterId);
                if (entry != null) portrait = entry.portrait;
            }
            else
            {
                CharacterEntry entry = ManagerRoot.Database.charDB.GetEntry(data.characterId);
                if (entry != null) portrait = entry.portraitImage;
            }
            
            if (portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.color = Color.white;
            }
            else
            {
                portraitImage.color = Color.clear;
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
                SkillData skillData = ManagerRoot.Database.GetSkill(skillId);
                if (skillData == null) continue;

                // SkillSlotUI 컴포넌트 가져오기
                SimpleListItemView slotUI = item.GetComponent<SimpleListItemView>();
                
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