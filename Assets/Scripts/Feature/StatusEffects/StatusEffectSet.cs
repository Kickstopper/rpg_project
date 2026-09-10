using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Battle;
using UnityEngine;

namespace RPGProject.Feature.StatusEffects
{
    [Serializable]
    public class StatusEffectSaveEntry
    {
        public string id;
        public int turnsRemaining;
        public int stepsElapsed;
    }

    /// <summary>One owner per character. Views share this instance; saves contain IDs, never SO references.</summary>
    public sealed class StatusEffectSet
    {
        readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        public IReadOnlyList<ActiveEffect> Effects => effects;
        public event Action Changed;

        public bool Has(StatusEffectID id) => effects.Exists(e => e.data.id == id);
        public bool HasRestriction(RestrictionType type, bool guaranteedOnly = false) =>
            effects.Exists(e => e.data.restrictionType == type &&
                e.data.restrictionChance >= (guaranteedOnly ? 1f : float.Epsilon));

        public bool Apply(StatusEffectData data)
        {
            if (data == null || data.id == StatusEffectID.None) return false;
            var existing = effects.Find(e => e.data.id == data.id);
            if (existing != null) effects.Remove(existing);
            // Replace the instance: a reapplication during an action must not consume its first turn.
            effects.Add(new ActiveEffect(data));
            Changed?.Invoke();
            return true;
        }

        public bool Remove(StatusEffectID id) => RemoveWhere(e => e.data.id == id);
        public bool RemoveWhere(Predicate<ActiveEffect> predicate)
        {
            if (effects.RemoveAll(predicate) == 0) return false;
            Changed?.Invoke();
            return true;
        }
        public void ClearBattleOnly() => RemoveWhere(e => e.data.durationType == EffectDurationType.BattleOnly);
        public void OnDirectDamage() => RemoveWhere(e => e.data.cureOnDirectDamage);
        public List<ActiveEffect> Snapshot() => new List<ActiveEffect>(effects);

        // Called once immediately before execution, never while drawing UI or deciding AI.
        public RestrictionType ResolveRestriction(Func<float> roll)
        {
            foreach (var type in new[] { RestrictionType.SkipTurn, RestrictionType.Charm,
                                        RestrictionType.Panic, RestrictionType.Silence })
                foreach (var effect in effects)
                    if (effect.data.restrictionType == type &&
                        roll() < Mathf.Clamp01(effect.data.restrictionChance)) return type;
            return RestrictionType.None;
        }

        // Only effects present when this action opportunity started can tick or expire.
        public int CompleteAction(IReadOnlyList<ActiveEffect> atStart, int maxHp, Func<float> roll)
        {
            int damage = 0;
            bool changed = false;
            foreach (var effect in atStart)
            {
                if (!effects.Contains(effect)) continue;
                damage += effect.data.BattleDamage(maxHp);
                bool cured = false;
                if (effect.data.cureType == EffectCureType.TurnBased)
                {
                    effect.turnsRemaining = Math.Max(0, effect.turnsRemaining - 1);
                    cured = effect.turnsRemaining == 0;
                    changed = true;
                }
                else if (effect.data.cureType == EffectCureType.ChancePerTurn)
                    cured = roll() < Mathf.Clamp01(effect.data.cureChancePerTurn);
                if (cured) { effects.Remove(effect); changed = true; }
            }
            if (changed) Changed?.Invoke();
            return damage;
        }

        public int Step(int hp, int maxHp)
        {
            if (hp <= 0) return hp;
            foreach (var effect in effects)
            {
                var data = effect.data;
                if (data.durationType != EffectDurationType.Persistent || data.FieldDamage(maxHp) <= 0) continue;
                effect.stepsElapsed++;
                if (effect.stepsElapsed < Mathf.Max(1, data.explorationStepInterval)) continue;
                effect.stepsElapsed = 0;
                hp = Mathf.Max(1, hp - data.FieldDamage(maxHp));
                if (hp == 0) break;
            }
            return hp;
        }

        public float Multiplier(Func<StatusEffectData, float> selector)
        {
            float result = 1f;
            foreach (var effect in effects) result *= Mathf.Max(0f, selector(effect.data));
            return result;
        }

        public List<StatusEffectSaveEntry> ExportPersistent() => effects
            .Where(e => e.data.durationType == EffectDurationType.Persistent)
            .Select(e => new StatusEffectSaveEntry { id = e.data.id.ToString(),
                turnsRemaining = e.turnsRemaining, stepsElapsed = e.stepsElapsed }).ToList();

        public void Import(IEnumerable<StatusEffectSaveEntry> saved, Func<StatusEffectID, StatusEffectData> resolve)
        {
            effects.Clear();
            if (saved != null)
                foreach (var entry in saved)
                {
                    if (entry == null || !Enum.TryParse(entry.id, out StatusEffectID id) || id == StatusEffectID.None) continue;
                    var data = resolve(id);
                    if (data == null || data.durationType != EffectDurationType.Persistent || Has(id)) continue;
                    if (data.cureType == EffectCureType.TurnBased && entry.turnsRemaining <= 0) continue;
                    effects.Add(new ActiveEffect(data) { turnsRemaining = Math.Max(0, entry.turnsRemaining),
                        stepsElapsed = Math.Max(0, entry.stepsElapsed) % Mathf.Max(1, data.explorationStepInterval) });
                }
            Changed?.Invoke();
        }
    }
}
