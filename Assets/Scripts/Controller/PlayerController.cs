using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using Manager;
using Data;

namespace Controller
{
    public class PlayerController : MonoBehaviour
    {
        [Header("UI Objects")]
        public Image bgImage;         // 배경의 사각형 이미지
        public Image faceImage;       // 캐릭터 얼굴
        public Slider hpSlider;       // HP 게이지
        public Slider mpSlider;       // SP 게이지
        public TextMeshProUGUI nameText;         // 이름 텍스트

        [Header("Runtime Data")]
        public CharacterDatabase.CharacterEntry sourceData; 

        [Header("Hit Feedback")]
        // 일반 피격 시 흔들림 강도/시간
        private float normalShakeMagnitude = 5f; 
        private float normalShakeDuration = 0.2f;

        // 크리티컬 피격 시 흔들림 강도/시간 (더 세고 길게)
        private float critShakeMagnitude = 15f;
        private float critShakeDuration = 0.5f;
        
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
        public int level = 1;
        public int currentHp;
        public int currentMp;

        // 현재 방어 태세인지 확인하는 플래그
        public bool isGuarding = false;

        // 현재 위치 (전열/후열) - 전투 매니저가 세팅해줌
        public RowType currentRow; 

        private Color originalColor; // 원래 색상
        private Coroutine highlightCoroutine; // 깜빡임 효과 제어용

        // 초기화 함수 (CombatManager가 호출)
        public void Initialize(CharacterDatabase.CharacterEntry data, RowType row)
        {
            sourceData = data;
            currentRow = row;
            
            // 이름 설정 (디버그용)
            gameObject.name = $"{data.name}_{data.id}";
            
            faceImage.sprite = data.portraitImage;
            faceImage.SetNativeSize();
            
            // 레벨 1 기준 스탯으로 초기화 (저장된 데이터가 있다면 거기서 로드)
            nameText.text = data.name;
            level = data.baseStats.level; 
            currentHp = data.maxHp; 
            currentMp = data.maxMp; // CharacterEntry의 프로퍼티 활용

            originalColor = bgImage.color;

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

        // 외부(CombatManager)에서 호출할 함수
        public void TriggerHitShake(bool isCritical)
        {
            // 혹시 이미 흔들리고 있다면 멈추고 새로 시작
            StopCoroutine("ProcessHitShake");

            float magnitude = isCritical ? critShakeMagnitude : normalShakeMagnitude;
            float duration = isCritical ? critShakeDuration : normalShakeDuration;

            StartCoroutine(ProcessHitShake(magnitude, duration));
        }

        // 실제 흔들림을 수행하는 코루틴
        private IEnumerator ProcessHitShake(float magnitude, float duration)
        {
            // 원래 위치 저장 (흔들린 후 돌아오기 위함)
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 랜덤한 방향으로 오프셋 생성
                // 2D 게임이므로 x, y축으로만 흔들림 적용
                float xOffset = Random.Range(-1f, 1f) * magnitude;
                float yOffset = Random.Range(-1f, 1f) * magnitude;

                // 위치 적용 (원래 위치 기준)
                transform.localPosition = originalPos + new Vector3(xOffset, yOffset, 0);

                elapsed += Time.deltaTime;
                // 다음 프레임까지 대기 (프레임마다 위치 변경 = 떨림 효과)
                yield return null; 
            }

            // 원래 위치로 복구
            transform.localPosition = originalPos;
        }

        // 턴 시작 시 방어 상태 초기화용 함수
        public void ResetStatus()
        {
            isGuarding = false;
            // 추후 다른 일시적 상태이상 해제도 여기서 처리 가능
        }

        public void SetHighlightColor(Color color)
        {
            if (bgImage != null)
            {
                bgImage.color = color;
            }
        }

        public void ResetHighlightColor()
        {
            if (bgImage != null)
            {
                bgImage.color = originalColor;
            }
        }

        // 선택 상태 켜기/끄기
        public void SetSelectionState(bool isSelected)
        {
            // 이미지가 없으면 무시
            if (bgImage == null) return;

            // 1. 선택 해제된 경우: 원래 색으로 복구하고 코루틴 정지
            if (!isSelected)
            {
                if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
                bgImage.color = originalColor;
                // 크기도 원래대로 (선택 시 커졌다면 복구)
                transform.localScale = Vector3.one; 
                return;
            }

            // 2. 선택된 경우: 깜빡임 코루틴 시작
            if (isSelected)
            {
                if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
                highlightCoroutine = StartCoroutine(AnimateHighlight());
            }
        }

        // 노란색 <-> 원래색 반복해서 부드럽게 깜빡이는 연출
        IEnumerator AnimateHighlight()
        {
            while (true)
            {
                // PingPong을 이용해 0~1 사이 값을 오가게 함
                float time = Mathf.PingPong(Time.time * 5f, 1f); 
                
                // 원래색과 선택색(Yellow) 사이를 부드럽게 섞음
                bgImage.color = Color.Lerp(originalColor, Color.yellow, time);
                
                // (선택 사항) 약간 커졌다 작아졌다 하는 연출 추가
                // float scale = Mathf.Lerp(1.0f, 1.1f, time);
                // transform.localScale = Vector3.one * scale;

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

        public int GetAttack()
        {
            int statAtk = sourceData.baseStats.str;
            int levelBonus = level;

            // 무기 공격력 합산
            int weaponBonus = (currentWeapon != null) ? currentWeapon.attackPower : 0;

            return statAtk + levelBonus + weaponBonus;
        }

        public int GetDefense()
        {
            int statDef = sourceData.baseStats.vit;
            int levelBonus = Mathf.FloorToInt(level * 0.5f);

            // 모든 방어구의 방어력 합산
            int armorBonus = 0;
            foreach (var armor in currentArmors)
            {
                armorBonus += armor.defense;
            }

            return statDef + levelBonus + armorBonus;
        }
        
        public ResistanceData GetResistances()
        {
            return sourceData.resistances; 
        }

        // 최종 민첩성(AGI) 반환 (기본 + 레벨 + 장비??)
        public int GetTotalAgi()
        {
            // 현재는 장비에 AGI가 없다고 가정하고 기본+레벨만 계산합니다.
            // 만약 장비에도 AGI가 붙는다면 GetAttack()처럼 장비 보정치를 더해주세요.
            return sourceData.baseStats.agi + level;
        }

        // 최종 행운(LUC) 반환
        public int GetTotalLuc()
        {
            return sourceData.baseStats.luc + level;
        }

        // 최종 힘(STR) 반환
        public int GetTotalStr()
        {
            // 장비 보정치가 있다면 여기서 더해줍니다.
            return sourceData.baseStats.str + level; 
        }

        // 최종 마력(MAG) 반환
        public int GetTotalMag()
        {
            return sourceData.baseStats.mag + level;
        }

        // 데미지 처리
        public IEnumerator OnDamageTaken(int damage)
        {
            currentHp -= damage;
            if (currentHp < 0) currentHp = 0;
            
            UpdateUI(); // HP바 갱신

            Debug.Log($"<color=red>{sourceData.name}에게 {damage} 데미지!</color> (남은 HP: {currentHp})");

            // 피격 연출 (얼굴이 흔들리거나 붉어짐)
            if (faceImage)
            {
                faceImage.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                faceImage.color = Color.white;
            }

            if (currentHp <= 0)
            {
                Debug.Log($"{sourceData.name} 사망...");
                // 사망 상태 아이콘 표시 등
            }
        }

        void UpdateUI()
        {
            if (hpSlider)
            {
                hpSlider.maxValue = sourceData.maxHp;
                hpSlider.value = currentHp;
            }

            if (mpSlider)
            {
                mpSlider.maxValue = sourceData.maxMp;
                mpSlider.value = currentMp;
            }
        }
    }
}
