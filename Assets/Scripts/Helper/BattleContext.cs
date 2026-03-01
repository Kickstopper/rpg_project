using System.Collections.Generic;
using Controller;

namespace Helper
{
    public class BattleContext
    {
        public List<BattleEntity> activePlayers;
        public List<BattleEntity> activeMonsters;

        public BattleContext(List<BattleEntity> activePlayers, List<BattleEntity> activeMonsters)
        {
            this.activePlayers = activePlayers;
            this.activeMonsters = activeMonsters;
        }
    }
}