using System.Collections.Generic;

using NUnit.Framework;
using RPGProject.Feature.Characters;
using RPGProject.Feature.StatusEffects;
using UnityEngine;

namespace RPGProject.Tests.StatusEffects
{
    public sealed class StatusEffectTests
    {
        readonly List<StatusEffectData> assets = new List<StatusEffectData>();
        StatusEffectData Effect(StatusEffectID id, RestrictionType restriction = RestrictionType.None)
        {
            var effect = ScriptableObject.CreateInstance<StatusEffectData>();
            assets.Add(effect); effect.id = id; effect.maxTurns = 3;
            effect.restrictionType = restriction; effect.restrictionChance = 1;
            return effect;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }
        [Test] public void SpecificCureKeepsOtherAilments()
        {
            var set = new StatusEffectSet(); set.Apply(Effect(StatusEffectID.Poison)); set.Apply(Effect(StatusEffectID.Petrify));
            Assert.IsTrue(set.Remove(StatusEffectID.Poison));
            Assert.IsTrue(set.Has(StatusEffectID.Petrify)); Assert.IsFalse(set.Remove(StatusEffectID.Poison));
        }
        [Test] public void RestrictionPriorityDoesNotDependOnInsertionOrder()
        {
            var silence = Effect(StatusEffectID.Silence, RestrictionType.Silence);
            var sleep = Effect(StatusEffectID.Sleep, RestrictionType.SkipTurn);
            foreach (bool reverse in new[] { false, true })
            {
                var set = new StatusEffectSet(); set.Apply(reverse ? sleep : silence); set.Apply(reverse ? silence : sleep);
                Assert.AreEqual(RestrictionType.SkipTurn, set.ResolveRestriction(() => 0f));
            }
        }
        [Test] public void ZeroChanceDoesNotBlockAndFractionalSilenceDoesNotDisableInput()
        {
            var silence = Effect(StatusEffectID.Silence, RestrictionType.Silence); silence.restrictionChance = 0;
            var set = new StatusEffectSet(); set.Apply(silence);
            Assert.AreEqual(RestrictionType.None, set.ResolveRestriction(() => 0f));
            silence.restrictionChance = .35f;
            Assert.IsFalse(set.HasRestriction(RestrictionType.Silence, true));
            Assert.AreEqual(RestrictionType.Silence, set.ResolveRestriction(() => .2f));
            Assert.AreEqual(RestrictionType.None, set.ResolveRestriction(() => .8f));
        }
        [Test] public void OneTurnStunExpiresAfterOneLostOpportunity()
        {
            var stun = Effect(StatusEffectID.Stun, RestrictionType.SkipTurn); stun.cureType = EffectCureType.TurnBased; stun.maxTurns = 1;
            var set = new StatusEffectSet(); set.Apply(stun);
            Assert.AreEqual(RestrictionType.SkipTurn, set.ResolveRestriction(() => 0f));
            set.CompleteAction(set.Snapshot(), 100, () => 0f);
            Assert.IsFalse(set.Has(StatusEffectID.Stun));
        }
        [Test] public void ReappliedOrNewEffectDoesNotExpireInCurrentOpportunity()
        {
            var stun = Effect(StatusEffectID.Stun); stun.cureType = EffectCureType.TurnBased; stun.maxTurns = 1;
            var set = new StatusEffectSet(); set.Apply(stun); var snapshot = set.Snapshot();
            set.Apply(stun); set.CompleteAction(snapshot, 100, () => 0f);
            Assert.AreEqual(1, set.Effects[0].turnsRemaining);
        }
        [Test] public void PoisonDoesNotWakeSleepButDirectDamageDoes()
        {
            var sleep = Effect(StatusEffectID.Sleep); sleep.cureOnDirectDamage = true;
            var poison = Effect(StatusEffectID.Poison); poison.battleDotMaxHpRatio = .05f;
            var set = new StatusEffectSet(); set.Apply(sleep); set.Apply(poison);
            Assert.AreEqual(5, set.CompleteAction(set.Snapshot(), 100, () => 1f));
            Assert.IsTrue(set.Has(StatusEffectID.Sleep)); set.OnDirectDamage();
            Assert.IsFalse(set.Has(StatusEffectID.Sleep)); Assert.IsTrue(set.Has(StatusEffectID.Poison));
        }
        [Test] public void MultiplePersistentEffectsRoundTripWithoutLosingStepProgress()
        {
            var poison = Effect(StatusEffectID.Poison); poison.durationType = EffectDurationType.Persistent;
            poison.explorationStepInterval = 3; poison.explorationDamage = 2;
            var paralyze = Effect(StatusEffectID.Paralyze); paralyze.durationType = EffectDurationType.Persistent;
            var set = new StatusEffectSet(); set.Apply(poison); set.Apply(paralyze); set.Apply(Effect(StatusEffectID.Burn));
            Assert.AreEqual(10, set.Step(10, 10)); Assert.AreEqual(10, set.Step(10, 10));
            var restored = new StatusEffectSet(); restored.Import(set.ExportPersistent(), id => id == poison.id ? poison : paralyze);
            Assert.AreEqual(2, restored.Effects.Count); Assert.AreEqual(8, restored.Step(10, 10));
        }
        [Test] public void ExplorationDamageLeavesOneHpAndNeverRevivesDeadCharacter()
        {
            var poison = Effect(StatusEffectID.Poison); poison.durationType = EffectDurationType.Persistent; poison.explorationDamage = 20;
            var set = new StatusEffectSet(); set.Apply(poison);
            Assert.AreEqual(1, set.Step(5, 100)); Assert.AreEqual(0, set.Step(0, 100));
        }
        [Test] public void EndingBattleRetainsPersistentEffectsAndNotifiesViews()
        {
            var poison = Effect(StatusEffectID.Poison); poison.durationType = EffectDurationType.Persistent;
            var set = new StatusEffectSet(); set.Apply(poison); set.Apply(Effect(StatusEffectID.Sleep));
            int notifications = 0; set.Changed += () => notifications++;
            set.ClearBattleOnly();
            Assert.AreEqual(1, notifications); Assert.AreEqual(1, set.Effects.Count); Assert.IsTrue(set.Has(poison.id));
        }
        [Test] public void ChanceCureIgnoresMaxTurnsAndNotifiesOnExpiry()
        {
            var burn = Effect(StatusEffectID.Burn); burn.cureType = EffectCureType.ChancePerTurn;
            burn.maxTurns = 1; burn.cureChancePerTurn = .25f;
            var set = new StatusEffectSet(); set.Apply(burn);
            for (int i = 0; i < 5; i++) set.CompleteAction(set.Snapshot(), 100, () => .9f);
            Assert.IsTrue(set.Has(burn.id));
            int changes = 0; set.Changed += () => changes++;
            set.CompleteAction(set.Snapshot(), 100, () => .1f);
            Assert.AreEqual(1, changes); Assert.IsFalse(set.Has(burn.id));
        }
        [Test] public void MissingResistanceIsNormalAndExplicitZeroIsImmune()
        {
            var resist = new ResistanceData(); Assert.AreEqual(1f, resist.GetStatusEffectMultiplier(StatusEffectID.Sleep));
            resist.statusEffects = new[] { new StatusEffectResistance { id = StatusEffectID.Sleep, inflictionMultiplier = 0 } };
            Assert.AreEqual(0f, resist.GetStatusEffectMultiplier(StatusEffectID.Sleep));
        }
        [Test] public void AttackAndDefenseMultipliersCombine()
        {
            var burn = Effect(StatusEffectID.Burn); burn.atkMultiplier = .8f;
            var curse = Effect(StatusEffectID.Curse); curse.atkMultiplier = .5f;
            var set = new StatusEffectSet(); set.Apply(burn); set.Apply(curse);
            Assert.AreEqual(.4f, set.Multiplier(d => d.atkMultiplier), .0001f);
        }
    }
}
