using UnityEngine;
namespace Data
{
    [System.Serializable]
    public struct StatusEffectResistance
    {
        public StatusEffectID id;
        [Tooltip("0 = immune, 0.5 = resist, 1 = normal, 1.5 = weak. Missing entries use 1.")]
        [Range(0f, 2f)] public float inflictionMultiplier;
    }
}
