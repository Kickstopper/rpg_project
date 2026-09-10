using System;
using System.Collections.Generic;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Inventory;
using RPGProject.Feature.Skills;
using RPGProject.Feature.StatusEffects;
using UnityEngine;

namespace RPGProject.Balance
{
    public enum SimPolicy { BasicAttack, AggressiveSkills, SupportFirst, RandomSkills }
    public enum SimTarget { Random, LowestHpRatio, CommanderFirst }
    public enum SimOpening { PartyFirst, EnemyFirst }
    [Serializable]
    public sealed class BalanceSlot
    {
        public string label = "Unit";
        public string monsterId;
        public StatData stats = new StatData { level = 10, str = 15, vit = 10, agi = 12, luc = 5, mag = 10, intel = 10 };
        public ResistanceData resistance;
        public WeaponData weapon;
        public List<SkillData> skills = new List<SkillData>();
        public List<StatusEffectData> startingStatuses = new List<StatusEffectData>();
        [Range(0, 1)] public float hpRatio = 1, mpRatio = 1;
        [Range(0, 5)] public int position;
        public bool commander;
        public SimPolicy policy = SimPolicy.SupportFirst;
        public SimTarget target = SimTarget.LowestHpRatio;
    }
    [CreateAssetMenu(menuName = "RPG/Balance Scenario")]
    public sealed class BalanceScenario : ScriptableObject
    {
        public string scenarioName = "새 시나리오";
        public MonsterDatabase monsterDatabase;
        public List<BalanceSlot> party = new List<BalanceSlot> { new BalanceSlot { label = "P1", commander = true }, new BalanceSlot { label = "P2", position = 1 } };
        public List<BalanceSlot> enemies = new List<BalanceSlot> { new BalanceSlot { label = "Enemy" } };
        public SimOpening opening;
        public bool useDatabaseAI = true;
        [Tooltip("현재 BattleManager는 적 스킬 비용을 차감하지 않습니다. 켜면 별도 밸런스 실험입니다.")]
        public bool chargeEnemySkillCost;
        [Min(1)] public int trials = 500;
        public int seed = 12345;
        [Range(1, 500)] public int maxRounds = 100;
        [Range(.1f, 3)] public float partyHpScale = 1, enemyHpScale = 1;
        [Range(.1f, 3)] public float enemyStatScale = 1;
        [Tooltip("공격/마법 스킬 사용 확률. SupportFirst의 긴급 회복은 우선합니다.")]
        [Range(0, 1)] public float skillChance = .7f;
        [Range(.05f, 1)] public float healThreshold = .45f;
        [Range(100, 10000)] public int replayFrameLimit = 2000;
    }
    [Serializable] public sealed class SimUnitFrame
    {
        public string name, statuses;
        public bool party;
        public int hp, maxHp, mp, maxMp;
    }
    [Serializable] public sealed class SimFrame
    {
        public int round, action;
        public string message;
        public List<SimUnitFrame> units = new List<SimUnitFrame>();
    }
    public enum SimOutcome { Running, Win, Loss, Timeout }
    [Serializable] public sealed class SimTrial
    {
        public int seed, rounds, actions;
        public SimOutcome outcome;
        public float remainingHpRatio;
        public int survivors;
    }
    [Serializable] public sealed class SimReport
    {
        public string name, configuration, assetManifest, unityVersion;
        public int requested, baseSeed;
        public bool cancelled;
        public List<SimTrial> trials = new List<SimTrial>();
        public int Wins => trials.FindAll(t => t.outcome == SimOutcome.Win).Count;
        public int Losses => trials.FindAll(t => t.outcome == SimOutcome.Loss).Count;
        public int Timeouts => trials.FindAll(t => t.outcome == SimOutcome.Timeout).Count;
        public float WinRate => trials.Count == 0 ? 0 : (float)Wins / trials.Count;
        public static Vector2 Wilson(int wins, int total)
        {
            if (total == 0) return Vector2.zero;
            double p = (double)wins / total, z = 1.95996398454, z2 = z * z;
            double center = (p + z2 / (2 * total)) / (1 + z2 / total);
            double half = z * Math.Sqrt(p * (1 - p) / total + z2 / (4 * total * total)) / (1 + z2 / total);
            return new Vector2((float)(center - half), (float)(center + half));
        }
    }
}
