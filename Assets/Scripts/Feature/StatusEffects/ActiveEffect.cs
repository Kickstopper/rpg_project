using UnityEngine;

namespace RPGProject.Feature.StatusEffects
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
}
