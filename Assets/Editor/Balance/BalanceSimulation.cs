using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Skills;
using RPGProject.Feature.StatusEffects;
using RPGProject.Shared.Gameplay;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPGProject.Balance
{
    // Uses real stat getters, damage/evasion/critical formulas and StatusEffectSet. No live scene or managers.
    public sealed class BalanceSimulation : IDisposable
    {
        sealed class Unit
        {
            public BattleEntity entity;
            public BalanceSlot slot;
            public bool party;
            public List<SkillData> skills;
            public BattleAction planned;
        }
        readonly BalanceScenario config;
        readonly List<Unit> units = new List<Unit>();
        readonly Queue<Unit> order = new Queue<Unit>();
        GameObject root;
        EffectManager effects;
        Random.State random;
        bool partyPhase;
        int phases;
        public SimTrial Result { get; private set; }
        public List<SimFrame> Frames { get; } = new List<SimFrame>();
        public bool TraceTruncated { get; private set; }
        readonly bool capture;
        public bool Done => Result.outcome != SimOutcome.Running;

        public static List<string> Validate(BalanceScenario c)
        {
            var issues = new List<string>();
            if (c == null) { issues.Add("시나리오가 없습니다."); return issues; }
            if (c.party == null || c.enemies == null || c.party.Count < 1 || c.party.Count > 6 || c.enemies.Count < 1 || c.enemies.Count > 6)
                issues.Add("양 진영은 각각 1~6명이어야 합니다.");
            if (c.trials < 1 || c.trials > 10000 || c.maxRounds < 1 || c.maxRounds > 500) issues.Add("반복 횟수 1~10000, 최대 라운드 1~500 범위를 사용하세요.");
            if (!Finite(c.partyHpScale) || !Finite(c.enemyHpScale) || !Finite(c.enemyStatScale) || c.partyHpScale <= 0 || c.enemyHpScale <= 0 || c.enemyStatScale <= 0 || c.partyHpScale > 10 || c.enemyHpScale > 10 || c.enemyStatScale > 10) issues.Add("HP/능력치 배율은 0보다 크고 10 이하여야 합니다.");
            foreach (var slot in (c.party ?? new List<BalanceSlot>()).Concat(c.enemies ?? new List<BalanceSlot>()))
            {
                if (slot == null) { issues.Add("빈 유닛 설정이 있습니다."); continue; }
                if (!Finite(slot.hpRatio) || !Finite(slot.mpRatio) || slot.hpRatio < 0 || slot.hpRatio > 1 || slot.mpRatio < 0 || slot.mpRatio > 1) issues.Add(slot.label + ": HP/MP 비율 범위 오류");
                if (slot.stats.level < 1 || slot.stats.level > 999 || slot.stats.vit < 1 || new[] { slot.stats.str, slot.stats.vit, slot.stats.agi, slot.stats.luc, slot.stats.mag, slot.stats.intel }.Any(v => v < 0 || v > 9999)) issues.Add(slot.label + ": Lv 1~999, VIT 1~9999, 나머지 능력치 0~9999 범위를 사용하세요.");
                if (!string.IsNullOrEmpty(slot.monsterId) && (c.monsterDatabase == null || c.monsterDatabase.entries == null ||
                    !c.monsterDatabase.entries.Any(e => e != null && e.id == slot.monsterId))) issues.Add(slot.label + ": 몬스터 ID를 찾을 수 없습니다: " + slot.monsterId);
                foreach (var s in slot.skills ?? new List<SkillData>())
                    if (s != null && (s.costValue < 0 || s.useType == UseType.Passive)) issues.Add(slot.label + ": 음수 비용/패시브 스킬은 실행 목록에서 제거하세요.");
            }
            foreach (var group in new[] { c.party, c.enemies })
                if (group != null && group.Where(s => s != null).GroupBy(s => s.position).Any(g => g.Count() > 1)) issues.Add("같은 진영의 position이 중복됩니다. 0~5에 각각 배치하세요.");
            if (c.useDatabaseAI && c.monsterDatabase != null && c.monsterDatabase.entries != null)
                foreach (var slot in c.enemies ?? new List<BalanceSlot>())
                {
                    if (slot == null) continue;
                    var entry = c.monsterDatabase.entries.FirstOrDefault(e => e != null && e.id == slot.monsterId);
                    var ai = entry?.aiProfile;
                    if (ai != null && !(ai is BasicAttackAI) && !(ai is SmartDefendAI) && !(ai is SmartJudgeAI) && !(ai is HealerAI))
                        issues.Add("지원하지 않는 사용자 AI입니다. DB AI를 끄고 명시적인 시뮬레이션 정책을 사용하세요: " + ai.name);
                }
            return issues;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public BalanceSimulation(BalanceScenario scenario, int seed, bool captureTrace)
        {
            config = scenario; capture = captureTrace;
            var issues = Validate(config); if (issues.Count > 0) throw new ArgumentException(string.Join("\n", issues));
            Result = new SimTrial { seed = seed };
            var previous = Random.state;
            try
            {
                Random.InitState(seed); random = Random.state;
                root = new GameObject("Balance simulation (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false); effects = root.AddComponent<EffectManager>();
                foreach (var slot in config.party) Create(slot, true);
                foreach (var slot in config.enemies) Create(slot, false);
                partyPhase = config.opening == SimOpening.PartyFirst;
                Capture("전투 시작"); CheckEnd();
            }
            catch { Dispose(); throw; }
            finally { Random.state = previous; }
        }
        void Create(BalanceSlot slot, bool party)
        {
            var go = new GameObject(slot.label) { hideFlags = HideFlags.HideAndDontSave }; go.transform.SetParent(root.transform);
            BattleEntity entity;
            List<SkillData> skills = slot.skills == null ? new List<SkillData>() : new List<SkillData>(slot.skills);
            if (!party && !string.IsNullOrEmpty(slot.monsterId))
            {
                var entry = config.monsterDatabase.entries.First(e => e != null && e.id == slot.monsterId);
                // Copy the mutable entry so no live database stats are ever changed.
                var copy = JsonUtility.FromJson<MonsterDatabase.MonsterEntry>(JsonUtility.ToJson(entry));
                copy.stats = Scale(copy.stats, config.enemyStatScale);
                var m = go.AddComponent<MonsterController>(); m.sourceData = copy; m.level = copy.stats.level;
                m.maxHp = Mathf.Max(1, Mathf.RoundToInt(copy.stats.vit * 5 * config.enemyHpScale));
                m.maxMp = Mathf.Max(0, copy.stats.mag * 3); entity = m;
                if (skills.Count == 0 && entry.skills != null) skills.AddRange(entry.skills);
            }
            else
            {
                var p = go.AddComponent<PlayerController>(); p.currentStats = Scale(slot.stats, party ? 1 : config.enemyStatScale);
                p.level = p.currentStats.level; p.resist = slot.resistance; p.currentWeapon = slot.weapon;
                p.maxHp = Mathf.Max(1, Mathf.RoundToInt(BattleCalculator.GetMaxHP(p.level, p.currentStats.str, p.currentStats.vit) *
                    (party ? config.partyHpScale : config.enemyHpScale)));
                p.maxMp = Mathf.Max(0, BattleCalculator.GetMaxMP(p.level, p.currentStats.mag, p.currentStats.intel)); entity = p;
            }
            entity.entityName = slot.label; entity.columnIndex = Mathf.Clamp(slot.position, 0, 5);
            entity.currentHp = Mathf.RoundToInt(entity.maxHp * slot.hpRatio); entity.currentMp = Mathf.RoundToInt(entity.maxMp * slot.mpRatio);
            if (slot.startingStatuses != null) foreach (var status in slot.startingStatuses) if (status != null) entity.StatusEffects.Apply(status);
            units.Add(new Unit { entity = entity, slot = slot, party = party, skills = skills.Where(s => s != null).ToList() });
        }
        static StatData Scale(StatData s, float f)
        {
            s.str = Mathf.Max(1, Mathf.RoundToInt(s.str * f)); s.vit = Mathf.Max(1, Mathf.RoundToInt(s.vit * f));
            s.agi = Mathf.Max(1, Mathf.RoundToInt(s.agi * f)); s.luc = Mathf.Max(0, Mathf.RoundToInt(s.luc * f));
            s.mag = Mathf.Max(0, Mathf.RoundToInt(s.mag * f)); s.intel = Mathf.Max(0, Mathf.RoundToInt(s.intel * f)); return s;
        }
        public void Step()
        {
            if (Done) return;
            var previous = Random.state; Random.state = random;
            try
            {
                if (order.Count == 0)
                {
                    if (phases >= config.maxRounds * 2) { Finish(SimOutcome.Timeout); return; }
                    foreach (var u in units.Where(u => u.party == partyPhase)) u.entity.ResetStatus();
                    foreach (var u in units.Where(u => u.party == partyPhase && u.entity.currentHp > 0)
                        .Select(Plan).OrderByDescending(u => u.planned != null ? u.planned.speed : u.entity.GetTotalAgi()).ThenBy(u => u.entity.columnIndex)) order.Enqueue(u);
                    phases++; Result.rounds = (phases + 1) / 2; partyPhase = !partyPhase;
                }
                if (order.Count == 0) { CheckEnd(); return; }
                var actor = order.Dequeue(); if (actor.entity.currentHp <= 0) return;
                Result.actions++;
                var snapshot = actor.entity.StatusEffects.Snapshot();
                var restriction = actor.entity.CheckActionRestriction();
                string message = actor.slot.label;
                if (restriction == RestrictionType.SkipTurn) message += " 행동 불가";
                else if (restriction == RestrictionType.Charm || restriction == RestrictionType.Panic)
                {
                    var targets = units.Where(u => u != actor && u.entity.currentHp > 0 &&
                        (restriction != RestrictionType.Charm || u.party == actor.party)).ToList();
                    if (targets.Count == 0 || (restriction == RestrictionType.Panic && Random.value < .5f)) message += " 혼란/매료: 대기";
                    else message += " " + restriction + " " + Hit(actor, targets[Random.Range(0, targets.Count)], null);
                }
                else
                {
                    var skill = actor.planned != null ? actor.planned.actionData as SkillData : ChooseSkill(actor);
                    if (actor.planned != null && actor.planned.type == ActionType.Guard)
                    {
                        actor.entity.isGuarding = true; message += " 방어";
                    }
                    else if (actor.planned != null && actor.planned.type == ActionType.Next) message += " 대기";
                    else if (skill != null && restriction == RestrictionType.Silence) message += " 침묵: 스킬 실패";
                    else if (skill != null) message += " " + UseSkill(actor, skill);
                    else
                    {
                        var target = units.FirstOrDefault(u => actor.planned != null && actor.planned.target == u.entity.gameObject && u.entity.currentHp > 0) ?? Pick(actor, units.Where(u => u.party != actor.party && u.entity.currentHp > 0).ToList());
                        message += target == null ? " 대상 없음" : " " + Hit(actor, target, null);
                    }
                }
                if (!CheckEnd() && actor.entity.currentHp > 0)
                {
                    int damage = actor.entity.StatusEffects.CompleteAction(snapshot, actor.entity.maxHp, () => Random.value);
                    if (damage > 0) { actor.entity.currentHp -= damage; message += $" / 지속 피해 {damage}"; }
                    CheckEnd();
                }
                Capture(message);
            }
            finally { random = Random.state; Random.state = previous; }
        }
        Unit Plan(Unit u)
        {
            u.planned = null;
            if (config.useDatabaseAI && !u.party && u.entity is MonsterController monster && monster.sourceData.aiProfile != null)
            {
                var context = new BattleContext(units.Where(t => t.party).Select(t => t.entity).ToList(), units.Where(t => !t.party).Select(t => t.entity).ToList());
                u.planned = monster.sourceData.aiProfile.DecideAction(monster, context);
            }
            return u;
        }
        SkillData ChooseSkill(Unit u)
        {
            if (u.slot.policy == SimPolicy.BasicAttack || !u.entity.CanUseSkills) return null;
            var usable = u.skills.Where(s => s.useType != UseType.Passive && s.useType != UseType.Exploration &&
                (!u.party && !config.chargeEnemySkillCost || (s.useHpCost ? u.entity.currentHp > s.costValue : u.entity.currentMp >= s.costValue))).ToList();
            if (u.slot.policy == SimPolicy.SupportFirst)
            {
                var heal = usable.FirstOrDefault(s => s.effectType == EffectType.Recover_HP &&
                    units.Any(t => t.party == u.party && t.entity.currentHp > 0 && (float)t.entity.currentHp / t.entity.maxHp < config.healThreshold));
                if (heal != null) return heal;
                var cure = usable.FirstOrDefault(s => IsCure(s.effectType) && units.Any(t => t.party == u.party && NeedsCure(t.entity, s.effectType)));
                if (cure != null) return cure;
                var revive = usable.FirstOrDefault(s => IsRevive(s) && units.Any(t => t.party == u.party && t.entity.currentHp == 0));
                if (revive != null) return revive;
            }
            var attacks = usable.Where(s => IsAttack(s)).ToList();
            if (u.slot.policy == SimPolicy.RandomSkills) attacks = usable;
            return attacks.Count == 0 || Random.value >= config.skillChance ? null : attacks[Random.Range(0, attacks.Count)];
        }
        static bool IsAttack(SkillData s) => s.effectType == EffectType.Magic_Atk || s.effectType == EffectType.Special_Atk || s.statusEffectData != null;
        static bool IsRevive(SkillData s) => s.effectType == EffectType.Revive_Empty || s.effectType == EffectType.Revive_Fully;
        static bool IsCure(EffectType type) => type == EffectType.Recover_Bad_Status || type == EffectType.Recover_Poison || type == EffectType.Recover_Paralyze || type == EffectType.Recover_Curse;
        static bool NeedsCure(BattleEntity e, EffectType type) => e.currentHp > 0 && (type == EffectType.Recover_Bad_Status ? e.activeEffects.Count > 0 :
            e.StatusEffects.Has(type == EffectType.Recover_Poison ? StatusEffectID.Poison : type == EffectType.Recover_Curse ? StatusEffectID.Curse : StatusEffectID.Paralyze));
        Unit Pick(Unit actor, List<Unit> candidates)
        {
            if (candidates.Count == 0) return null;
            if (actor.slot.target == SimTarget.CommanderFirst) return candidates.FirstOrDefault(u => u.slot.commander) ?? candidates[Random.Range(0, candidates.Count)];
            if (actor.slot.target == SimTarget.LowestHpRatio) return candidates.OrderBy(u => (float)u.entity.currentHp / u.entity.maxHp).First();
            return candidates[Random.Range(0, candidates.Count)];
        }
        string UseSkill(Unit actor, SkillData skill)
        {
            bool ally = skill.targetScope == TargetScope.Self || skill.targetScope == TargetScope.One_Ally || skill.targetScope == TargetScope.All_Allies ||
                skill.targetScope == TargetScope.Dead_Ally || skill.targetScope == TargetScope.All_Dead_Allies;
            var targets = units.Where(u => (u.party == actor.party) == ally && (IsRevive(skill) ? u.entity.currentHp == 0 : u.entity.currentHp > 0)).ToList();
            if (skill.targetScope == TargetScope.Self) targets = new List<Unit> { actor };
            if (skill.targetScope == TargetScope.Front_Enemies || skill.targetScope == TargetScope.Front_Single_Enemy || skill.targetScope == TargetScope.Random_Front_Enemy)
                targets = targets.Where(u => u.entity.columnIndex < 3).ToList();
            if (IsCure(skill.effectType)) targets = targets.Where(u => NeedsCure(u.entity, skill.effectType)).ToList();
            bool all = skill.targetScope == TargetScope.All_Enemies || skill.targetScope == TargetScope.All_Allies ||
                skill.targetScope == TargetScope.All_Dead_Allies || skill.targetScope == TargetScope.Front_Enemies;
            if (!all && targets.Count > 0)
            {
                var planned = targets.FirstOrDefault(u => actor.planned != null && actor.planned.target == u.entity.gameObject);
                targets = new List<Unit> { planned ?? (ally ? targets.OrderBy(u => (float)u.entity.currentHp / u.entity.maxHp).First() : Pick(actor, targets)) };
            }
            if (targets.Count == 0) return skill.dataName + ": 대상 없음";
            if (actor.party || config.chargeEnemySkillCost)
            {
                if (skill.useHpCost ? actor.entity.currentHp <= skill.costValue : actor.entity.currentMp < skill.costValue) return skill.dataName + ": 비용 부족";
                if (skill.useHpCost) actor.entity.currentHp -= skill.costValue; else actor.entity.currentMp -= skill.costValue;
            }
            var messages = new List<string>();
            foreach (var target in targets)
            {
                if (actor.entity.currentHp <= 0) break;
                if (IsAttack(skill)) messages.Add(Hit(actor, target, skill));
                else messages.Add(target.slot.label + (effects.ApplyEffect((IBattleTarget)target.entity, skill) ? " 적용" : " 효과 없음"));
            }
            return skill.dataName + ": " + string.Join("; ", messages);
        }
        string Hit(Unit attacker, Unit target, SkillData skill)
        {
            var a = attacker.entity; var d = target.entity;
            var action = new BattleAction(a.gameObject, d.gameObject, skill == null ? ActionType.Attack : ActionType.Skill, a.GetTotalAgi()) { actionData = skill };
            if (BattleCalculator.CheckEvasion(a, d, action, 0)) return target.slot.label + " 회피";
            ElementType element = skill == null ? ElementType.Physical : skill.element;
            var tier = d.GetResistances().GetResistanceTier(element);
            bool physical = element == ElementType.Physical;
            if (tier == ResistTier.Null) return target.slot.label + " 무효";
            int damage = BattleCalculator.CalculateDamage(a, d, action, BattleCalculator.CheckCritical(a, d, action));
            if (tier == ResistTier.Repel || (physical ? d.isPhysicalReflect : d.isMagicReflect))
            {
                if (damage > 0) a.StatusEffects.OnDirectDamage(); a.currentHp -= damage;
                return "반사 " + damage;
            }
            if (tier == ResistTier.Drain || (physical ? d.isPhysicalAbsorb : d.isMagicAbsorb))
            {
                d.currentHp += damage; return target.slot.label + " 흡수 " + damage;
            }
            if (damage > 0) d.StatusEffects.OnDirectDamage(); d.currentHp -= damage;
            if (d.currentHp > 0 && skill != null) BattleCalculator.ProcessSkillStatusEffect(a, d, skill);
            return target.slot.label + " 피해 " + damage;
        }
        bool CheckEnd()
        {
            bool loss = units.Where(u => u.party).All(u => u.entity.currentHp <= 0 || u.entity.IsPetrified) ||
                units.Any(u => u.party && u.slot.commander && (u.entity.currentHp <= 0 || u.entity.IsPetrified));
            if (loss) { Finish(SimOutcome.Loss); return true; }
            if (units.Where(u => !u.party).All(u => u.entity.currentHp <= 0 || u.entity.IsPetrified)) { Finish(SimOutcome.Win); return true; }
            return false;
        }
        void Finish(SimOutcome outcome)
        {
            Result.outcome = outcome;
            Result.survivors = units.Count(u => u.party && u.entity.currentHp > 0 && !u.entity.IsPetrified);
            Result.remainingHpRatio = (float)units.Where(u => u.party).Sum(u => u.entity.currentHp) / units.Where(u => u.party).Sum(u => u.entity.maxHp);
        }
        void Capture(string message)
        {
            if (!capture) return;
            if (Frames.Count >= config.replayFrameLimit) { TraceTruncated = true; return; }
            Frames.Add(new SimFrame { round = Result.rounds, action = Result.actions, message = message,
                units = units.Select(u => new SimUnitFrame { name = u.slot.label, party = u.party, hp = u.entity.currentHp,
                    maxHp = u.entity.maxHp, mp = u.entity.currentMp, maxMp = u.entity.maxMp,
                    statuses = StatusEffectText.Summary(u.entity.StatusEffects, true) }).ToList() });
        }
        public void Dispose() { if (root != null) UnityEngine.Object.DestroyImmediate(root); root = null; }
    }
}
