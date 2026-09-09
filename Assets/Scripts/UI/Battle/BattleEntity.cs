using System.Collections;
using System.Collections.Generic;
using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Battle
{
    [System.Serializable]
    public class ActiveEffect
    {
        public StatusEffectData data;
        public int turnsRemaining; // 남은 행동 기회 수
        public int stepsElapsed;

        public ActiveEffect(StatusEffectData data)
        {
            this.data = data;
            this.turnsRemaining = Mathf.Max(1, data.maxTurns);
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
                int clampedValue = Mathf.Clamp(value, 0, maxHp);
                if (_currentHp != clampedValue)
                {
                    _currentHp = clampedValue;
                    
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
                int clampedValue = Mathf.Clamp(value, 0, maxMp);
                if (_currentMp != clampedValue)
                {
                    _currentMp = clampedValue;
                    
                    UpdateUI(); 
                }
            }
        }

        public StatusEffectSet StatusEffects { get; private set; } = new StatusEffectSet();
        public IReadOnlyList<ActiveEffect> activeEffects => StatusEffects.Effects;
        [Tooltip("Optional status badge anchor. Otherwise a badge is created on this card.")]
        public RectTransform statusEffectAnchor;
        public bool IsPetrified => StatusEffects.Has(StatusEffectID.Petrify);
        public bool CanCooperate => currentHp > 0 &&
            !StatusEffects.HasRestriction(RestrictionType.SkipTurn) &&
            !StatusEffects.HasRestriction(RestrictionType.Charm) && !StatusEffects.HasRestriction(RestrictionType.Panic);
        public bool CanUseSkills => !StatusEffects.HasRestriction(RestrictionType.Silence, true);
        public float StatusAttackMultiplier => StatusEffects.Multiplier(d => d.atkMultiplier);
        public float StatusDefenseMultiplier => StatusEffects.Multiplier(d => d.defMultiplier);
        public float StatusAccuracyMultiplier => StatusEffects.Multiplier(d => d.accMultiplier);
        public float StatusEvasionMultiplier => StatusEffects.Multiplier(d => d.evaMultiplier);

        protected void BindStatusEffects(StatusEffectSet effects)
        {
            StatusEffects = effects ?? new StatusEffectSet();
            StatusEffectHUD.Attach(this);
        }

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
        private Coroutine hitShakeCoroutine;
        private Vector3 hitShakeOrigin;
        private bool isHitShaking;
        public Color originalColor { get; protected set; }

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
            if (!isActiveAndEnabled) return;
            StopHitShake();
            float magnitude = isCritical ? critShakeMagnitude : normalShakeMagnitude;
            float duration = isCritical ? critShakeDuration : normalShakeDuration;
            hitShakeCoroutine = StartCoroutine(ProcessHitShake(magnitude, duration));
        }

        private void StopHitShake()
        {
            if (hitShakeCoroutine != null) StopCoroutine(hitShakeCoroutine);
            hitShakeCoroutine = null;
            if (!isHitShaking) return;
            transform.localPosition = hitShakeOrigin;
            isHitShaking = false;
        }

        protected virtual void OnDisable()
        {
            StopHitShake();
        }

        protected IEnumerator ProcessHitShake(float magnitude, float duration)
        {
            hitShakeOrigin = transform.localPosition;
            isHitShaking = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float xOffset = Random.Range(-1f, 1f) * magnitude;
                float yOffset = Random.Range(-1f, 1f) * magnitude;
                transform.localPosition = hitShakeOrigin + new Vector3(xOffset, yOffset, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = hitShakeOrigin;
            isHitShaking = false;
            hitShakeCoroutine = null;
        }
        
        public void ApplyStatusEffect(StatusEffectData effectData)
        {
            if (currentHp <= 0 || !StatusEffects.Apply(effectData)) return;
            UpdateUI();
        }

        public void TickStatusEffects(System.Action<int> onDotDamageTaken = null)
        {
            int damage = StatusEffects.CompleteAction(StatusEffects.Snapshot(), maxHp, () => Random.value);
            if (damage > 0) onDotDamageTaken?.Invoke(damage);
        }

        public void ClearBattleOnlyEffects()
        {
            StatusEffects.ClearBattleOnly();
            buffPhysAtk = buffMagAtk = buffPhysDef = buffMagDef = 0;
            ResetStatus();
        }

        public RestrictionType CheckActionRestriction() => StatusEffects.ResolveRestriction(() => Random.value);

        // 타겟팅 모드(초상화 UI 등)를 켜고 끄기 위한 가상 함수
        public virtual void SetTargetingMode(bool isTargeting) { } 


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
        public abstract VfxID GetBasicAttackVfx();
        public abstract VfxID GetGunAttackVfx();
    }
}
