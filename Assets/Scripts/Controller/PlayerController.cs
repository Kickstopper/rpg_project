using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using Manager;
using Data;
using Data.Database;

namespace Controller
{
    public class PlayerController : BattleEntity
    {
        [Header("UI Objects")]
        public Image bgImage;         // 배경의 사각형 이미지
        public Image highlightImage;         // 하이라이트 사각형 이미지
        public Image portraitImage;       // 캐릭터 얼굴
        public Slider hpSlider;       // HP 게이지
        public Slider mpSlider;       // SP 게이지
        public TextMeshProUGUI nameText;         // 이름 텍스트
        public TextMeshProUGUI alignText;        // 성향

        [Header("Runtime Data")]
        public CharacterDatabase.CharacterEntry sourceData; 

        // 현재 배운 스킬 목록 (ID)
        public List<string> learnedSkillIds = new List<string>();

        // 현재 장착 장비 (ID)
        [Header("Equipment Slots")]
        public string equippedWeaponId; // 근접 무기
        public string equippedGunId;    // 총
        public string equippedAmmoId;   // 총알
        public List<string> equippedArmorIds = new List<string>();

        // 실제 계산을 위한 캐싱 데이터 (ID를 통해 로드된 실제 객체)
        public WeaponData currentWeapon; // 근접 무기 데이터
        public WeaponData currentGun;    // 총 데이터
        public AmmoData currentAmmo;     // 총알 데이터
        private List<ArmorData> currentArmors = new List<ArmorData>();
        
        // 상태 이상 (Status Effect)
        public List<string> currentStatusEffects = new List<string>();

        // 현재 위치 (전열/후열) - 전투 매니저가 세팅해줌
        public RowType currentRow; 

        // [BattleEntity 구현] 스탯 반환 (레벨 및 장비 보정 포함)
        public override int GetTotalStr() => sourceData.stats.str + level; 
        public override int GetTotalAgi() => sourceData.stats.agi + level;
        public override int GetTotalMag() => sourceData.stats.mag + level;
        public override int GetTotalLuc() => sourceData.stats.luc + level;
        public override int GetTotalVit() => sourceData.stats.vit + level;

        public bool IsEmpty { get; private set; } = false;

        // 빈 슬롯용 초기화 함수
        public void InitializeEmpty(int colIndex)
        {
            IsEmpty = true;
            this.columnIndex = colIndex;

            align = Align.None;

            currentHp = 0;
            currentMp = 0;
            currentWeapon = null;
            currentAmmo = null;
            currentGun = null;
            learnedSkillIds.Clear();
            equippedArmorIds.Clear();
            RefreshArmorStats();

            sourceData = null;
            currentStatusEffects.Clear();
            
            // 1. 그래픽 숨기기 (스프라이트, UI 등)
            UpdateUI();

            // 2. 이름표 변경 (디버깅용)
            this.name = $"Empty_Slot_{colIndex}";
        }

        // 초기화 함수 (CombatManager가 호출)
        public void Initialize(CharacterDatabase.CharacterEntry data, RowType row)
        {
            IsEmpty = false; // 데이터가 있으면 False

            sourceData = data;
            currentRow = row;
            entityName = data.name;
            
            align = data.align;
            
            // HP/MP 설정
            maxHp = data.maxHp;
            maxMp = data.maxMp;
            currentHp = maxHp;
            currentMp = maxMp;

            // UI 설정
            if (portraitImage && data.portraitImage)
            {
                portraitImage.gameObject.SetActive(true);
                portraitImage.sprite = data.portraitImage;
                portraitImage.color = Color.white;
                portraitImage.SetNativeSize();
            }
            
            nameText.text = data.name;
            nameText.alignment = TextAlignmentOptions.TopLeft;

            alignText.text = GetAlignString(align);
            
            level = data.stats.level;

            highlightImage.gameObject.SetActive(true);
            highlightImage.color = Color.clear;
            
            UpdateUI();

            // -----------------------------------------------------
            // 초기 스킬 및 장비 로드
            // -----------------------------------------------------
            
            // 1. 스킬 복사 (Reference가 아닌 값 복사를 위해 리스트 새로 생성)
            learnedSkillIds = new List<string>(data.initialSkillIds);

            // 2. 무기 장착
            EquipWeapon(data.initialWeaponId);
            EquipGun(data.initialGunId, data.initialAmmoId);

            // 3. 방어구 장착
            equippedArmorIds = new List<string>(data.initialArmorIds);
            RefreshArmorStats();
        }

        private string GetAlignString(Align align)
        {
            switch(align)
            {
                case Align.Chaotic_Evil:
                    return "C/E";
                case Align.Chaotic_Neutral:
                    return "C/N";
                case Align.Chaotic_Good:
                    return "C/G";

                case Align.Lawful_Evil:
                    return "L/E";
                case Align.Lawful_Neutral:
                    return "L/N";
                case Align.Lawful_Good:
                    return "L/G";

                case Align.Neutral_Evil:
                    return "N/E";
                case Align.True_Neutral:
                    return "T/N";
                case Align.Neutral_Good:
                    return "N/G";
                
                default:
                    return "None";
            }
        }

        // 회복 함수
        public void Recover(int hpAmount, int mpAmount)
        {
            if (hpAmount > 0)
            {
                currentHp = Mathf.Min(currentHp + hpAmount, sourceData.maxHp);
                // 회복 연출 (텍스트 등)
                Debug.Log($"{sourceData.name} HP {hpAmount} 회복");
            }
            if (mpAmount > 0)
            {
                currentMp = Mathf.Min(currentMp + mpAmount, sourceData.maxMp);
            }
            UpdateUI();
        }

        // 부활 함수
        public void Revive(int percent)
        {
            if (currentHp > 0) return; // 이미 살아있음

            int healAmount = Mathf.FloorToInt(sourceData.maxHp * (percent / 100f));
            currentHp = healAmount;
            
            // 죽은 상태(Dead) 플래그 해제 및 UI 복구 로직 필요
            gameObject.SetActive(true); 
            UpdateUI();
            Debug.Log($"{sourceData.name} 부활!");
        }

        public void SetHighlightColor(Color color)
        {
            highlightImage.color = color;
        }

        public void ResetHighlightColor()
        {
            highlightImage.color = Color.clear;
        }

        // [BattleEntity 구현] 선택 강조
        public override void SetSelectionState(bool isSelected)
        {
            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);

            if (isSelected)
            {
                highlightCoroutine = StartCoroutine(AnimateHighlight());
            }
            else
            {
                highlightImage.color = Color.clear;
                transform.localScale = Vector3.one; 
            }
        }

        IEnumerator AnimateHighlight()
        {
            while (true)
            {
                float time = Mathf.PingPong(Time.time * 5f, 1f); 
                highlightImage.color = Color.Lerp(Color.clear, Color.yellow, time);
                yield return null;
            }
        }
        
        // 총 공격력 계산 (총 데미지 + 총알 데미지 + 스탯)
        public int GetGunAttack()
        {
            if (currentGun == null || currentAmmo == null) return 0;

            int gunAtk = currentGun.attackPower;
            int ammoAtk = currentAmmo.damageBonus;
            int statBonus = 0;

            // 총은 LUC이나 DEX(AGI) 영향을 받음
            if (currentGun.scalingStatName == "AGI") statBonus = GetTotalAgi();
            else if (currentGun.scalingStatName == "LUC") statBonus = GetTotalLuc();
            
            return gunAtk + ammoAtk + statBonus;
        }
        
        // 발사 가능 여부 확인
        public bool CanShootGun()
        {
            return currentGun != null && currentAmmo != null;
        }

        // =========================================================
        // [장비 관리 메서드]
        // =========================================================
        public void EquipWeapon(string weaponId)
        {
            equippedWeaponId = weaponId;

            if (!string.IsNullOrEmpty(weaponId))
                currentWeapon = DatabaseManager.Instance.GetWeapon(weaponId);
            else
                currentWeapon = null;
        }

        // 총 장비 함수
        public void EquipGun(string gunId, string ammoId)
        {
            equippedGunId = gunId;
            equippedAmmoId = ammoId;

            if (!string.IsNullOrEmpty(gunId))
                currentGun = DatabaseManager.Instance.GetWeapon(gunId); // GetWeapon 재활용 (WeaponData 타입이므로)
            else
                currentGun = null;

            if (!string.IsNullOrEmpty(ammoId))
                // 총알 데이터
                currentAmmo = DatabaseManager.Instance.GetAmmo(ammoId);
            else
                currentAmmo = null;
        }

        public void RefreshArmorStats()
        {
            currentArmors.Clear();
            foreach (var id in equippedArmorIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                
                // DatabaseManager를 사용하여 실제 데이터 로드
                ArmorData armor = DatabaseManager.Instance.GetArmor(id);
                if (armor != null) currentArmors.Add(armor);
            }
        }

        // =========================================================
        // 장비 스탯 반영
        // =========================================================
        public override int GetAttack()
        {
            int statAtk = sourceData.stats.str;
            int levelBonus = level;

            // 무기 공격력 합산
            int weaponBonus = (currentWeapon != null) ? currentWeapon.attackPower : 0;

            return statAtk + levelBonus + weaponBonus;
        }

        public override int GetMagicAttack()
        {
            //차후 구체화
            return GetTotalMag();
        }

        public override int GetDefense()
        {
            int statDef = sourceData.stats.vit;
            int levelBonus = Mathf.FloorToInt(level * 0.5f);

            // 모든 방어구의 방어력 합산
            int armorBonus = 0;
            foreach (var armor in currentArmors)
            {
                armorBonus += armor.defense;
            }

            return statDef + levelBonus + armorBonus;
        }
        
        public override ResistanceData GetResistances()
        {
            return sourceData.resistances; 
        }

        // [BattleEntity 구현] 데미지 처리
        public override IEnumerator OnDamageTaken(int damage)
        {
            currentHp = Mathf.Max(0, currentHp - damage);
            UpdateUI(); 

            Debug.Log($"<color=red>{entityName}에게 {damage} 데미지!</color>");

            // 플레이어 전용 피격 연출 (얼굴 붉어짐)
            if (portraitImage && portraitImage.sprite)
            {
                portraitImage.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                portraitImage.color = Color.white;
            }

            if (currentHp <= 0)
            {
                Debug.Log($"{entityName} 쓰러짐...");
                // Player는 비활성화 대신 사망 상태(Dead State) 처리 필요
                InitializeEmpty(this.columnIndex);
            }
        }
        
        void UpdateUI()
        {
            if (IsEmpty)
            {
                hpSlider.gameObject.SetActive(false);
                mpSlider.gameObject.SetActive(false);
                nameText.text = "EMPTY";
                nameText.alignment = TextAlignmentOptions.Center;
                alignText.text = string.Empty;
                portraitImage.gameObject.SetActive(false);
            }
            else
            {
                hpSlider.gameObject.SetActive(true);
                mpSlider.gameObject.SetActive(true);
                hpSlider.maxValue = maxHp;
                hpSlider.value = currentHp;
                mpSlider.maxValue = maxMp;
                mpSlider.value = currentMp;
            }
        }
    }
}
