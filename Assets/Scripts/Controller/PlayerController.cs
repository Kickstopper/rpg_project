using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using Manager;
using Data;
using DG.Tweening;
using System.Linq;
using Helper;

namespace Controller
{
    public class PlayerController : BattleEntity, IBattleTarget
    {
        [SerializeField]
        private ExpTable expTable;

        [Header("UI Objects")]
        public Image bgImage;         // 배경의 사각형 이미지
        public Image portraitImage;
        public Image highlightImage;         // 하이라이트 사각형 이미지
        public Slider hpSlider;       // HP 게이지
        public Slider mpSlider;       // MP 게이지
        public GameObject messagePanel;
        public TextMeshProUGUI messageText;
        public TextMeshProUGUI nameText;         // 이름 텍스트
        public TextMeshProUGUI alignText;        // 성향
        public TextMeshProUGUI resonanceText;       // 빙의한 신의 이름
        public GameObject dim;

        public Button selectButton;
        
        [Header("Runtime Data")]
        public RuntimeCharacterData sourceData; // 캐릭터의 고유 데이터
        public ResonanceData resonanceData;
        public StatData currentStats; // 캐릭터 단독 또는 캐릭터와 스피릿의 융합 스탯
        public ResistanceData resist; // 캐릭터 단독 또는 캐릭터와 스피릿의 융합 내성

        // 스킬 목록
        public List<string> learnedSkillIds = new List<string>();

        // 현재 장착 장비
        [Header("Equipment Slots")]
        public string equippedWeaponId; // 근접 무기
        public string equippedGunId;    // 총
        public string equippedAmmoId;   // 총알
        public List<string> equippedArmorIds = new List<string>();

        // 실제 계산을 위한 캐싱 데이터 (ID를 통해 로드된 실제 객체)
        public WeaponData currentWeapon; // 근접 무기 데이터
        public WeaponData currentGun;    // 총 데이터
        public AmmoData currentAmmo;     // 총알 데이터

        public int currentGunAmmo = 0; // 현재 탄환 수

        private List<ArmorData> currentArmors = new List<ArmorData>();
        
        // 상태 이상
        public List<string> currentStatusEffects = new List<string>();

        public bool isCommander;

        private BattleManager controller;

        // BattleEntity 구현. 스탯 반환 (스피릿 융합된 스탯 + 장비 보정)
        public override int GetTotalStr() => currentStats.str; 
        public override int GetTotalAgi() => currentStats.agi;
        public override int GetTotalMag() => currentStats.mag;
        public override int GetTotalLuc() => currentStats.luc;
        public override int GetTotalVit() => currentStats.vit;
        public override int GetTotalInt() => currentStats.intel;

        public int TotalArmorEvasion => totalArmorEvasion;
        private int totalArmorEvasion;

        // IBattleTarget 구현
        public bool IsAlive => currentHp > 0;
        public bool IsMaxHp => currentHp >= maxHp;
        public bool IsMaxMp => currentMp >= maxMp;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int CurrentMp => currentMp;
        public int MaxMp => maxMp;

        public bool IsEmpty { get; private set; } = false;

        private bool hasAnimation = false;

        void Start()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnClicked);
            }
        }

        // 빈 슬롯용 초기화 함수
        public void InitializeEmpty(int colIndex)
        {
            IsEmpty = true;
            columnIndex = colIndex;

            align = Align.None;

            isCommander = false;

            currentHp = 0;
            currentMp = 0;
            maxHp = 0;
            maxMp = 0;

            currentWeapon = null;
            
            currentGun = null;
            currentAmmo = null;
            currentGunAmmo = 0;

            resonanceData = null;
            
            // 그래픽 숨기기 (스프라이트, UI 등)
            UpdateUI();

            // 이름표 변경 (디버깅용)
            this.name = $"Empty_Slot_{colIndex}";
        }
        
        // RuntimeData를 받는 초기화 함수
        public void Initialize(RuntimeCharacterData runtimeData, BattleManager controller, bool hasAnimation = false)
        {
            this.hasAnimation = hasAnimation;
            this.controller = controller;

            if (runtimeData == null)
            {
                InitializeEmpty(columnIndex);
                return;
            }

            // 데이터 초기화
            this.sourceData = runtimeData;
            this.entityName = runtimeData.name;
            this.gameObject.name = entityName;

            this.resonanceData = DatabaseManager.Instance.GetResonance(runtimeData.resonanceId);

            // 데이터 융합
            if (this.resonanceData != null)
            {
                // 스탯 평균화
                this.currentStats = CalculateAverageStats(runtimeData.stats, resonanceData.stats);
                
                // 성향 평균화
                this.align = AlignmentSystem.GetAverageAlign(runtimeData.align, resonanceData.align);

                // 스킬 합치기
                this.learnedSkillIds = runtimeData.learnedSkills.Union(resonanceData.skills.Select(s => s.id)).ToList();

                // 내성 합치기
                this.resist = runtimeData.resistances;
            }
            else
            {
                // 스피릿이 없으면 본체 데이터 복사해서 사용
                this.currentStats = new StatData {
                    level = runtimeData.stats.level,
                    str = runtimeData.stats.str,
                    vit = runtimeData.stats.vit,
                    intel = runtimeData.stats.intel,
                    agi = runtimeData.stats.agi,
                    luc = runtimeData.stats.luc,
                    mag = runtimeData.stats.mag
                };
                this.align = runtimeData.align;
                this.resist = runtimeData.resistances;
                this.learnedSkillIds = new List<string>(runtimeData.learnedSkills);
            }

            // 스피릿과의 합성 상태에 따라 변화된 레벨값 할당
            this.level = currentStats.level;
            
            InitializeStats(); 

            this.currentHp = Mathf.Min(runtimeData.currentHp, this.maxHp);
            this.currentMp = Mathf.Min(runtimeData.currentMp, this.maxMp);

            // UI 초기화
            if (nameText) nameText.text = entityName;

            // DB에서 이미지 가져오기
            var dbEntry = PartyManager.Instance.charDB.GetEntry(runtimeData.characterId);
            if (dbEntry != null && portraitImage)
            {
                portraitImage.sprite = dbEntry.battlePortraitImg;
                portraitImage.color = new Color(1,1,1,0.1f);
            }
            
            // 장비 및 스킬 복구
            EquipWeapon(runtimeData.equippedWeaponId);
            EquipGun(runtimeData.equippedGunId, runtimeData.equippedAmmoId);
            
            this.equippedArmorIds = new List<string>(runtimeData.equippedArmorIds);

            RefreshArmorStats();

            // 파생 스탯 최종 계산
            InitializeStats(); 
            
            UpdateUI();
        }

        
        // 스피릿과 캐릭터의 스탯을 하나로
        private StatData CalculateAverageStats(StatData charStats, StatData resonanceStats)
        {
            StatData result = new StatData();
            result.level = Mathf.CeilToInt((charStats.level + resonanceStats.level) / 2f);
            result.str = charStats.str + resonanceStats.str;
            result.mag = charStats.mag + resonanceStats.mag;
            result.intel = charStats.intel + resonanceStats.intel;
            result.vit = charStats.vit + resonanceStats.vit;
            result.agi = charStats.agi + resonanceStats.agi;
            result.luc = charStats.luc + resonanceStats.luc;

            return result;
        }

        private void InitializeStats()
        {
            this.maxHp = ((GetTotalStr() + GetTotalVit()) * (level + 1)) / 4 + 14;
            this.maxMp = ((GetTotalMag() + GetTotalInt()) * (level + 4)) / 8 + 4;
        }
        
        // 회복 함수
        public void Recover(int hpAmount, int mpAmount)
        {
            if (!IsAlive) return;
            
            if (hpAmount > 0)
            {
                currentHp = Mathf.Min(currentHp + hpAmount, maxHp);
                // 회복 연출 (텍스트 등)
                Debug.Log($"{entityName} HP {hpAmount} 회복");
            }
            if (mpAmount > 0)
            {
                currentMp = Mathf.Min(currentMp + mpAmount, maxMp);
            }
            UpdateUI();
        }

        // 부활 함수
        public void Revive(int percent)
        {
            if (IsAlive) return;
            
            int healAmount = Mathf.FloorToInt(maxHp * (percent / 100f));
            currentHp = healAmount;
            
            gameObject.SetActive(true); 
            UpdateUI();
            Debug.Log($"{entityName} 부활!");
        }

        public void SetHighlightColor(Color color)
        {
            highlightImage.color = color;
        }

        public void ResetHighlightColor()
        {
            highlightImage.color = Color.clear;
        }

        // 선택 상태 표시
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
        
        // GATK = 총 공격력 + 총알 공격력 + (LV/4)
        public int GetGunAttack()
        {
            if (currentGun == null || currentAmmo == null) return 0;
            return currentGun.attackPower + currentAmmo.damageBonus + (level / 4);
        }
        
        // 발사 가능 여부 확인
        public bool CanShootGun()
        {
            return currentGun != null && currentAmmo != null;
        }

        
        // [장비 관리 메서드]
        
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
                currentGun = DatabaseManager.Instance.GetWeapon(gunId);
            else
                currentGun = null;

            if (!string.IsNullOrEmpty(ammoId))
                // 총알 데이터
                currentAmmo = DatabaseManager.Instance.GetAmmo(ammoId);
            else
                currentAmmo = null;
            
            if (currentGun != null) currentGunAmmo = currentGun.maxHits;
        }

        public void RefreshArmorStats()
        {
            currentArmors.Clear();
            totalArmorEvasion = 0;
            foreach (var id in equippedArmorIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                
                // DatabaseManager를 사용하여 실제 데이터 로드
                ArmorData armor = DatabaseManager.Instance.GetArmor(id);
                if (armor != null)
                {
                    totalArmorEvasion += armor.evasionMod;
                    currentArmors.Add(armor);  
                } 
            }
        }

        public override int GetAttack()
        {
            // ATK = STR + 무기 공격력 + (LV/4)
            int weaponBonus = (currentWeapon != null) ? currentWeapon.attackPower : 0;
            float baseAtk = GetTotalStr() + weaponBonus + (level / 4);
            return Mathf.RoundToInt(baseAtk * GetBuffMultiplier(buffPhysAtk));
        }

        public override int GetMagicAttack()
        {
            // 마법 공격력 (MATK = (MAG*2) + (INT/2))
            float baseMagAtk = (GetTotalMag() * 2f) + (GetTotalInt() / 2f);
            return Mathf.RoundToInt(baseMagAtk * GetBuffMultiplier(buffMagAtk));
        }

        public override int GetDefense()
        {
            // DEF = 장비방어력 + VIT + AGI
            int armorBonus = 0;
            foreach (var armor in currentArmors) armorBonus += armor.defense;
            float baseDef = armorBonus + GetTotalVit() + GetTotalAgi();
            return Mathf.RoundToInt(baseDef * GetBuffMultiplier(buffPhysDef));
        }

        public override int GetMagicDefense()
        {
            // 마법 방어력 (MDEF = (MAG+VIT+AGI)/4 + INT + 장비방어력/4)
            int armorBonus = 0;
            foreach (var armor in currentArmors) armorBonus += armor.defense;
            
            float baseMagDef = (GetTotalMag() + GetTotalVit() + GetTotalAgi()) / 4f + GetTotalInt() + armorBonus / 4f;
            return Mathf.RoundToInt(baseMagDef * GetBuffMultiplier(buffMagDef));
        }

        public override int GetHitRate()
        {
            int weaponHit = currentWeapon != null ? currentWeapon.hitRateBonus : 0;
            float baseHitRate = GetTotalAgi() + weaponHit + (GetTotalLuc() / 2f) + level;
            return Mathf.RoundToInt(baseHitRate);
        }

        public override int GetEvasion()
        {
            int armorEva = 0;
            foreach (var armor in currentArmors) armorEva += armor.evasionMod;
            float baseEvation = armorEva + GetTotalAgi() + (GetTotalInt() / 4) + (GetTotalLuc() / 4) + level;
            return Mathf.RoundToInt(baseEvation);
        }

        public override ResistanceData GetResistances()
        {
            return resist; 
        }

        // 경험치 획득 및 데이터 갱신 로직
        public void ApplyExperience(int earnedExp)
        {
            sourceData.currentExp += earnedExp;
            
            while (true)
            {
                int requiredExp = BattleCalculator.GetMaxExpForLevel(sourceData.stats.level);

                if (sourceData.currentExp >= requiredExp)
                {
                    // 레벨 업!
                    sourceData.currentExp -= requiredExp;
                    sourceData.stats.level++;
                }
                else
                {
                    break;
                }
            }

            Debug.Log($"[Level Up Logic] {entityName} Updated -> Lv.{level}, Exp: {sourceData.currentExp}");
        }

        public override IEnumerator OnDamageTaken(int damage)
        {
            currentHp = Mathf.Max(0, currentHp - damage);
            UpdateUI(); 

            Debug.Log($"<color=red>{entityName}에게 {damage} 데미지!</color>");

            if (portraitImage && portraitImage.sprite)
            {
                portraitImage.color = new Color(1f, 0f, 0f, 0.1f);
                yield return new WaitForSeconds(0.1f);
                // 사망 시 초상화를 어둡게, 살아있으면 원래 색으로
                portraitImage.color = currentHp <= 0 ? new Color(0.5f, 0.5f, 0.5f, 0.1f) : new Color(1f, 1f, 1f, 0.1f);
            }

            if (currentHp <= 0)
            {
                Debug.Log($"{entityName} 쓰러짐...");
                SetMessage("DEAD");
            }
        }
        
        public void SetMessage(string message)
        {
            messageText.SetText(message);
        }

        void OnClicked()
        {
            controller?.OnTargetSelected(this);
        }

        public void ApplyHpChange(int amount)
        {
            if (!IsAlive) return;
            if (sourceData != null) sourceData.currentHp = Mathf.Clamp(sourceData.currentHp + amount, 0, sourceData.maxHp);
            currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        }

        public void ApplyMpChange(int amount)
        {
            if (!IsAlive) return;
            if (sourceData != null) sourceData.currentMp = Mathf.Clamp(sourceData.currentMp + amount, 0, sourceData.maxMp);
            currentMp = Mathf.Clamp(currentMp + amount, 0, maxMp);
        }

        public void ApplyRevive(int percent)
        {
            if (IsAlive) return;

            int healAmount = Mathf.FloorToInt(maxHp * (percent / 100f));
            if (healAmount < 1) healAmount = 1;
            
            if (sourceData != null) sourceData.currentHp = healAmount;
            currentHp = healAmount;
        }

        public void ApplyStatusEffect(StatusEffect effect)
        {
            if (sourceData != null) sourceData.statusEffect = effect;
        }

        public void RefreshView()
        {
            UpdateUI(); 
        }
        
        protected override void UpdateUI()
        {
            if (IsEmpty)
            {
                if (dim) dim.SetActive(false);
                if (messagePanel) messagePanel.SetActive(false);
                if (messageText) messageText.SetText(string.Empty);
                // 비활성화 시 진행 중인 트윈(애니메이션)이 있다면 즉시 중단
                if (hpSlider != null) hpSlider.DOKill();
                if (mpSlider != null) mpSlider.DOKill();

                if (hpSlider) hpSlider.gameObject.SetActive(false);
                if (mpSlider) mpSlider.gameObject.SetActive(false);
                
                if (nameText)
                {
                    nameText.text = "EMPTY";
                    nameText.alignment = TextAlignmentOptions.Center;
                }
                if (alignText) alignText.text = string.Empty;
                if (resonanceText) resonanceText.text = string.Empty;
                if (portraitImage) portraitImage.gameObject.SetActive(false);
            }
            else
            {
                if (dim) dim.SetActive(!IsAlive);
                if (messagePanel) messagePanel.SetActive(true);
                if (messageText) messageText.SetText(string.Empty);
                if (alignText) alignText.text = AlignmentSystem.GetAlignString(align);
                if (resonanceText && resonanceData) resonanceText.text = resonanceData.entityName;

                if (hpSlider)
                {
                    hpSlider.gameObject.SetActive(true);
                    hpSlider.maxValue = maxHp;

                    // 즉시 변경 대신 DOValue 사용
                    hpSlider.DOKill(); // 기존 애니메이션이 있다면 취소하여 겹침 방지
                    if (hasAnimation) hpSlider.DOValue(currentHp, 0.3f).SetEase(Ease.OutCubic);
                    else hpSlider.value = currentHp;
                }

                if (mpSlider)
                {
                    mpSlider.gameObject.SetActive(true);
                    mpSlider.maxValue = maxMp;

                    // MP도 동일하게 적용
                    mpSlider.DOKill();
                    if (hasAnimation) mpSlider.DOValue(currentMp, 0.3f).SetEase(Ease.OutCubic);
                    else mpSlider.value = currentMp;
                }
            }
        }

    }
}
