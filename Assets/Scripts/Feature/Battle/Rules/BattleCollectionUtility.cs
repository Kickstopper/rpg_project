using System;
using System.Collections.Generic;

namespace RPGProject.Feature.Battle
{
    public static class BattleCollectionUtility
    {
        public static int CountLiving(List<BattleEntity> source)
        {
            int count = 0;
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null && source[i].currentHp > 0) count++;
            return count;
        }

        public static void CopyLiving(List<BattleEntity> source, List<BattleEntity> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (ReferenceEquals(source, destination))
                throw new ArgumentException("Source and destination must be different lists.", nameof(destination));

            destination.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                BattleEntity entity = source[i];
                if (entity != null && entity.currentHp > 0) destination.Add(entity);
            }
        }

        public static BattleEntity FirstLiving(List<BattleEntity> source)
        {
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null && source[i].currentHp > 0) return source[i];
            return null;
        }

        public static int GetLivingAverages(List<BattleEntity> source, bool includeLuck,
            out float agility, out float luck, out float level)
        {
            long agilitySum = 0, luckSum = 0, levelSum = 0;
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                BattleEntity entity = source[i];
                if (entity == null || entity.currentHp <= 0) continue;
                agilitySum += entity.GetTotalAgi();
                if (includeLuck) luckSum += entity.GetTotalLuc();
                levelSum += entity.level;
                count++;
            }
            agility = count == 0 ? 0f : (float)((double)agilitySum / count);
            luck = count == 0 ? 0f : (float)((double)luckSum / count);
            level = count == 0 ? 0f : (float)((double)levelSum / count);
            return count;
        }

        public static void SortActionsBySpeed(List<BattleAction> actions)
        {
            for (int i = 1; i < actions.Count; i++)
            {
                BattleAction action = actions[i];
                int j = i - 1;
                while (j >= 0 && actions[j].speed < action.speed)
                {
                    actions[j + 1] = actions[j];
                    j--;
                }
                actions[j + 1] = action;
            }
        }
    }
}
