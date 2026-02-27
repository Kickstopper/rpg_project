using UnityEngine;
using Controller;
using Helper;
using UI.DungeonMapScene;

namespace Data.AI
{
    public abstract class MonsterAIProfile : ScriptableObject
    {
        public abstract BattleAction DecideAction(MonsterController self, BattleContext context);
    }
}