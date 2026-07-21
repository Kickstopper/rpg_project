using UnityEngine;
using Helper;
using UI.DungeonMapScene;
using UI.Battle;

namespace Data.AI
{
    public abstract class MonsterAIProfile : ScriptableObject
    {
        public abstract BattleAction DecideAction(MonsterController self, BattleContext context);
    }
}