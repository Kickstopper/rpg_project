using System.Collections;
using System.Collections.Generic;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Controller
{
    [System.Serializable]
    public class ActiveEffect
    {
        public StatusEffectData data;
        public int turnsRemaining; // 남은 턴 수

        public ActiveEffect(StatusEffectData data)
        {
            this.data = data;
            this.turnsRemaining = data.maxTurns;
        }
    }
    // 공통 기능을 담은 추상 클래스
    public abstract class BattleEntity : MonoBehaviour
    {
        [Header("Entity Status")]
        public string entityName;
        public int level = 1;
        public Align align;

        [SerializeField] private int _currentHp; // 인스펙터 확인용 실제 변수

        public int currentHp
        {
            get => _currentHp;
            set
            {
                // 값이 실제로 변했을 때만 로직 수행
                if (_currentHp != value)
                {
                    _currentHp = Mathf.Clamp(value, 0, maxHp);
                    
                    UpdateUI(); 
                }
            }
        }
        [SerializeField] private int _currentMp; // 인스펙터 확인용 실제 변수

        public int currentMp
        {
            get => _currentMp;
            set
            {
                if (_currentMp != value)
                {
                    _currentMp = Mathf.Clamp(value, 0, maxMp);
                    
                    UpdateUI(); 
                }
            }
        }

        public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

        public int maxHp;
        public int maxMp;
        public int columnIndex;
        public int nextTurnSpeedPenalty = 0; // 이번 턴에 무리해서 다음 턴 속도가 느려질 값

        [Header("UI Reference")]
        public Image preferredImage;
        public TextMeshProUGUI turnOrderText; 

        [Header("State Flags")]
        public bool isGuarding = false;
        public bool isPhysicalReflect = false;
        public bool isMagicReflect = false;
        public bool isPhysicalAbsorb = false;
        public bool isMagicAbsorb = false;

        [Header("Hit Feedback")]
        protected float normalShakeMagnitude = 5f;
        protected float normalShakeDuration = 0.2f;
        protected float critShakeMagnitude = 15f;
        protected float critShakeDuration = 0.5f;

        // 공통 코루틴 참조
        protected Coroutine highlightCoroutine;
        protected Color originalColor; 

        [Header("Buff/Debuff Stacks (-4 ~ +4)")]
        public int buffPhysAtk = 0;
        public int buffMagAtk = 0;
        public int buffPhysDef = 0;
        public int buffMagDef = 0;

        // 버프 스택 증감 함수 (4중첩 제한)
        public bool ChangeBuffStack(ref int currentStack, int amount)
        {
            int before = currentStack;
            // -4(최대 디버프)에서 4(최대 버프) 사이로 값 고정
            currentStack = Mathf.Clamp(currentStack + amount, -4, 4);
            
            // 이미 풀 스택이라 변화가 없다면 false 반환 (UI 갱신 생략용)
            return currentStack != before; 
        }

        // 스택을 실제 스탯 배율로 변환 (기획에 맞춰 1스택당 25% 증감으로 설정)
        public float GetBuffMultiplier(int stack)
        {
            // 예: 4스택 = 2.0배, -4스택 = 0.25배 (최소 0.25배 보장)
            return Mathf.Max(0.25f, 1.0f + (stack * 0.25f));
        }

        public void ResetStatus()
        {
            isGuarding = false;
            isPhysicalReflect = false;
            isMagicReflect = false;
            isPhysicalAbsorb = false;
            isMagicAbsorb = false;
        }

        // 피격 시 흔들림 연출 (두 클래스에서 완전히 동일한 코드)
        public void TriggerHitShake(bool isCritical)
        {
            // 오브젝트가 꺼져있으면 코루틴을 시작하지 않음
            if (!gameObject.activeInHierarchy) return;

            StopCoroutine("ProcessHitShake");
            float magnitude = isCritical ? critShakeMagnitude : normalShakeMagnitude;
            float duration = isCritical ? critShakeDuration : normalShakeDuration;
            StartCoroutine(ProcessHitShake(magnitude, duration));
        }

        protected IEnumerator ProcessHitShake(float magnitude, float duration)
        {
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float xOffset = Random.Range(-1f, 1f) * magnitude;
                float yOffset = Random.Range(-1f, 1f) * magnitude;
                transform.localPosition = originalPos + new Vector3(xOffset, yOffset, 0);
                elapsed += Time.deltaTime;
                yield return null; 
            }
            transform.localPosition = originalPos;
        }
        
        // 상태이상 부여
        public void ApplyStatusEffect(StatusEffectData effectData)
        {
            // 이미 같은 상태이상이 있는지 확인 (갱신 처리)
            var existing = activeEffects.Find(e => e.data.id == effectData.id);
            if (existing != null)
            {
                existing.turnsRemaining = effectData.maxTurns; // 턴 수 초기화
            }
            else
            {
                activeEffects.Add(new ActiveEffect(effectData));
            }
            
            Debug.Log($"{this.name}에게 {effectData.effectName} 부여됨!");
            UpdateUI();
        }

        // 매 턴 시작 또는 종료 시 호출할 함수
        public void TickStatusEffects()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeEffects[i];

                // 지속 데미지 처리
                if (effect.data.dotDamage > 0)
                {
                    // ApplyDamage 로직 호출 (HP 감소)
                }

                // 해제 조건 체크
                bool isCured = false;
                
                if (effect.data.cureType == EffectCureType.TurnBased)
                {
                    effect.turnsRemaining--;
                    if (effect.turnsRemaining <= 0) isCured = true;
                }
                else if (effect.data.cureType == EffectCureType.ChancePerTurn)
                {
                    if (Random.value < effect.data.cureChancePerTurn) isCured = true;
                }

                // 해제 처리
                if (isCured)
                {
                    Debug.Log($"{this.name}의 {effect.data.effectName}이(가) 해제되었습니다.");
                    activeEffects.RemoveAt(i);
                }
            }
        }

        // 전투 종료 시 호출할 함수
        public void ClearBattleOnlyEffects()
        {
            // durationType이 BattleOnly인 것만 리스트에서 제거
            activeEffects.RemoveAll(e => e.data.durationType == EffectDurationType.BattleOnly);

            // 전투가 끝나면 버프/디버프 스택 초기화
            buffPhysAtk = 0;
            buffMagAtk = 0;
            buffPhysDef = 0;
            buffMagDef = 0;
        }

        // 턴 시작 시 또는 행동 실행 직전에 호출하여 제약이 발동했는지 확인합니다.
        public RestrictionType CheckActionRestriction()
        {
            foreach (var effect in activeEffects)
            {
                if (effect.data.restrictionType == RestrictionType.None) continue;

                // 설정된 확률(restrictionChance)에 따라 제약 발동 여부 결정
                if (Random.value < effect.data.restrictionChance)
                {
                    return effect.data.restrictionType; // 가장 먼저 걸린 제약 반환
                }
            }
            
            return RestrictionType.None; // 무사통과
        }

        // 데미지 처리는 연출과 로직(UI갱신 vs 사망처리)이 다르므로 추상화
        public abstract IEnumerator OnDamageTaken(int damage);

        protected abstract void UpdateUI();
        
        // 스탯 계산 방식이 다르므로(장비 유무) 추상화
        public abstract int GetTotalStr();
        public abstract int GetTotalAgi();
        public abstract int GetTotalMag();
        public abstract int GetTotalLuc();
        public abstract int GetTotalVit();
        public abstract int GetTotalInt();
        public abstract int GetAttack();
        public abstract int GetDefense();
        
        public abstract int GetHitRate();
        public abstract int GetEvasion();

        public abstract int GetMagicAttack();
        public abstract int GetMagicDefense();
        
        public abstract ResistanceData GetResistances();
        public abstract void SetSelectionState(bool isSelected);
    }
}