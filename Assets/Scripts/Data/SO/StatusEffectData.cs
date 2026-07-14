using UnityEngine;

namespace Data
{
    // 행동 제약의 종류를 정의합니다.
    public enum RestrictionType
    {
        None,           // 제약 없음
        SkipTurn,       // 행동을 아예 넘김 (수면, 기절, 마비 등)
        Silence,        // 기본 공격/아이템은 가능하나, 스킬(마법) 불가
        Confusion,      // 조작 불가 & 무작위 타겟 공격 (혼란)
        Charm           // 조작 불가 & 아군을 공격하거나 적을 회복 (매료)
    }

    public enum EffectDurationType { BattleOnly, Persistent }
    public enum EffectCureType { ExplicitOnly, TurnBased, ChancePerTurn }

    [CreateAssetMenu(fileName = "New Status Effect", menuName = "Game Data/Status Effect")]
    public class StatusEffectData : ScriptableObject
    {
        public string id;
        public string effectName;

        [Header("Duration & Cure")]
        public EffectDurationType durationType;
        public EffectCureType cureType;
        public int maxTurns;
        [Range(0, 1f)] public float cureChancePerTurn;

        [Header("Stat Multipliers")]
        public float atkMultiplier = 1.0f;
        public float defMultiplier = 1.0f;
        public float evaMultiplier = 1.0f;
        public float accMultiplier = 1.0f;

        [Header("Action Restrictions")]
        public RestrictionType restrictionType = RestrictionType.None;
        
        [Tooltip("제약이 발동할 확률 (0: 절대 발동 안함, 1: 100% 발동)")]
        [Range(0, 1f)] public float restrictionChance = 0f; 

        [Header("Damage Over Time (DoT)")]
        public int dotDamage;
    }
}