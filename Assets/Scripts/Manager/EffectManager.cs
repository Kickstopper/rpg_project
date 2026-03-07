using UnityEngine;
using Data;

namespace Manager
{
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// 아이템/스킬의 효과를 데이터(Model)에 반영하고 UI 갱신을 요청합니다.
        /// </summary>
        public bool ApplyEffect(IBattleTarget target, BaseRootData data)
        {
            if (target == null || data == null) return false;

            bool success = false;

            switch (data.effectType)
            {
                // --- 회복 계열 ---
                case EffectType.Recover_HP:
                    if (target.IsMaxHp) return false; // 이미 풀피면 실패
                    target.ApplyHpChange(data.effectValue);
                    success = true;
                    break;

                case EffectType.Recover_MP:
                    if (target.IsMaxMp) return false;
                    target.ApplyMpChange(data.effectValue);
                    success = true;
                    break;

                // --- 부활 계열 ---
                case EffectType.Revive_Empty: // HP 1로 부활 혹은 소량 부활
                case EffectType.Revive_Fully:
                    if (target.IsAlive) return false; // 살아있으면 실패
                    
                    int revivePercent = (data.effectType == EffectType.Revive_Fully) ? 100 : data.effectValue;
                    target.ApplyRevive(revivePercent);
                    success = true;
                    break;

                // --- 공격 계열 (전투 중 아이템) ---
                case EffectType.Special_Atk:
                case EffectType.Magic_Atk:
                    // 공격은 '음수 HP 회복'으로 처리하거나 별도 데미지 로직을 따름
                    // 여기서는 단순 수치 반영 예시
                    target.ApplyHpChange(-data.effectValue);
                    success = true;
                    break;

                // TODO: 버프/디버프 로직 추가
            }

            if (success)
            {
                // 데이터 변경 후 UI 갱신 요청
                target.RefreshView();
            }

            return success;
        }
    }
}