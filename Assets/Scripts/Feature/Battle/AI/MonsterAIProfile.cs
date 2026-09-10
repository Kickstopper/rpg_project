using UnityEngine;

namespace RPGProject.Feature.Battle
{
    public abstract class MonsterAIProfile : ScriptableObject
    {
        public abstract BattleAction DecideAction(MonsterController self, BattleContext context);
    }
}