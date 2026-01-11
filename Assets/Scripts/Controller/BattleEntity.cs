using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Controller
{
    // 공통 기능을 담은 추상 클래스
    public abstract class BattleEntity : MonoBehaviour
    {
        [Header("Entity Status")]
        public string entityName; // 이름 공통화
        public int currentHp;
        public int currentMp;
        public int maxHp; // 최대 체력 공통 필드 필요
        public int maxMp;

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

        // =========================================================
        // 1. 공통 로직 (그대로 상속받아 사용)
        // =========================================================

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

        // =========================================================
        // 2. 추상 메서드 (자식이 반드시 구현해야 함)
        // =========================================================
        
        // 데미지 처리는 연출과 로직(UI갱신 vs 사망처리)이 다르므로 추상화
        public abstract IEnumerator OnDamageTaken(int damage);
        
        // 스탯 계산 방식이 다르므로(장비 유무) 추상화
        public abstract int GetTotalStr();
        public abstract int GetTotalAgi();
        public abstract int GetTotalMag();
        public abstract int GetTotalLuc();

        public abstract int GetAttack();
        public abstract int GetDefense();
        public abstract ResistanceData GetResistances();
        public abstract void SetSelectionState(bool isSelected);
        
        // 생존 확인 헬퍼
        public bool IsAlive => currentHp > 0;
    }
}