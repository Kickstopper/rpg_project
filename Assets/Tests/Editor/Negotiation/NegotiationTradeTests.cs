using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Negotiation;
using RPGProject.Infrastructure.Persistence;
using RPGProject.Feature.StatusEffects;
using UnityEngine;

namespace RPGProject.Tests.Negotiation
{
    public sealed class NegotiationTradeTests
    {
        [TestCase(0f, DemandKind.Gold)]
        [TestCase(0.26f, DemandKind.Item)]
        [TestCase(0.51f, DemandKind.HP)]
        [TestCase(0.76f, DemandKind.MP)]
        public void AllFourDemandsCommitPaymentAndRewardExactlyOnce(float demandRoll, DemandKind kind)
        {
            int payments = 0, rewards = 0;
            var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                d => { Assert.That(d.Kind, Is.EqualTo(kind)); Assert.That(d.Amount, Is.GreaterThan(0)); payments++; return true; },
                () => false, () => false, _ => true, _ => { rewards++; return true; }, () => false, () => demandRoll);
            Assert.That(session.Resolve("plan", "CHECK_MOOD:TRADE:Gold"), Is.EqualTo("DEMAND_" + kind.ToString().ToUpperInvariant()));
            string cmd = "CHECK_MOOD:PAY:" + kind;
            Assert.That(session.Resolve("pay", cmd), Is.EqualTo("SUCCESS_GOLD"));
            session.Resolve("pay", cmd);
            Assert.That(session.Resolve("settle-again", "CHECK_MOOD:SETTLE"), Is.EqualTo("SUCCESS_GOLD"));
            session.Resolve("other-pay", cmd);
            Assert.That(payments, Is.EqualTo(1)); Assert.That(rewards, Is.EqualTo(1));
            session.Close(); Assert.That(session.Resolve("late", "CHECK_MOOD:SETTLE"), Is.EqualTo("END"));
        }

        [Test]
        public void BetrayalRequiresPaymentAndGrantsNoReward()
        {
            int paid = 0, escaped = 0, rewarded = 0;
            var session = new NegotiationSession(Personality.Sly, Race.Human, default, 0, 100, 100,
                d => { paid += d.Amount; return true; }, () => { rewarded++; return true; }, () => false,
                _ => true, _ => { rewarded++; return true; }, () => { escaped++; return true; }, () => 0f);
            Assert.That(session.Resolve("early", "CHECK_MOOD:SETTLE"), Is.EqualTo("FAIL"));
            Assert.That(escaped, Is.Zero);
            session.Resolve("plan", "CHECK_MOOD:TRADE:Recruit");
            Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:Gold"), Is.EqualTo("FLED"));
            session.Resolve("again", "CHECK_MOOD:SETTLE");
            Assert.That(paid, Is.EqualTo(100)); Assert.That(escaped, Is.EqualTo(1)); Assert.That(rewarded, Is.Zero);
            Assert.That(session.Fled, Is.True); Assert.That(session.Recruited, Is.False);
        }

        [Test]
        public void PaymentFailureDoesNotRollBetrayalOrGiveAnything()
        {
            int draws = 0;
            var session = new NegotiationSession(Personality.Sly, Race.Human, default, 0, 100, 100, _ => false,
                () => throw new Exception("recruit"), () => false, _ => true,
                _ => throw new Exception("reward"), () => throw new Exception("flee"), () => { draws++; return 0f; });
            session.Resolve("plan", "CHECK_MOOD:TRADE:HP");
            Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:Gold"), Is.EqualTo("INSUFFICIENT_ITEM"));
            Assert.That(draws, Is.EqualTo(1)); Assert.That(session.HasPaidTrade, Is.False);
            Assert.That(session.Resolve("settle", "CHECK_MOOD:SETTLE"), Is.EqualTo("FAIL"));
        }

        [Test]
        public void PreflightRejectsUnavailableRewardBeforeDemandOrSpending()
        {
            var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                _ => throw new Exception("pay"), () => false, () => false, _ => false,
                _ => false, () => false, () => throw new Exception("roll"));
            Assert.That(session.Resolve("plan", "CHECK_MOOD:TRADE:HP"), Is.EqualTo("TRADE_DECLINED"));
            Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:HP"), Is.EqualTo("FAIL"));
        }

        [Test]
        public void DifferentDemandCannotBeSubstitutedAndFailedRewardDoesNotShowSuccess()
        {
            int paid = 0;
            var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                _ => { paid++; return true; }, () => false, () => false, _ => true, _ => false, () => false, () => 0f);
            session.Resolve("plan", "CHECK_MOOD:TRADE:Item");
            Assert.That(session.Resolve("wrong", "CHECK_MOOD:PAY:HP"), Is.EqualTo("FAIL"));
            Assert.That(paid, Is.Zero);
            Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:Gold"), Is.EqualTo("FAIL_REWARD"));
            Assert.That(session.RewardGranted, Is.False);
        }

        [Test]
        public void FinanceCallbackCannotReenterPayment()
        {
            NegotiationSession session = null; int paid = 0;
            session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                _ => { paid++; Assert.That(session.Resolve("nested", "CHECK_MOOD:PAY:Gold"), Is.EqualTo("FAIL")); return true; },
                () => true, () => false, _ => true, _ => true, () => false, () => 0f);
            session.Resolve("plan", "CHECK_MOOD:TRADE:Recruit");
            Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:Gold"), Is.EqualTo("SUCCESS_RECRUIT"));
            Assert.That(paid, Is.EqualTo(1));
        }

        [TestCase(Personality.Polite)]
        [TestCase(Personality.Principled)]
        [TestCase(Personality.Rational)]
        public void HonestPersonalitiesNeverBetray(Personality p)
        {
            Assert.That(NegotiationTradeRules.FleeChance(p), Is.Zero);
        }

        [Test]
        public void HealingClampsAndSynchronizesInactiveActorAndHonorsCurse()
        {
            var root = new GameObject("inactive payer"); root.SetActive(false);
            var curse = ScriptableObject.CreateInstance<StatusEffectData>();
            try
            {
                var pc = root.AddComponent<PlayerController>();
                pc.sourceData = new RuntimeCharacterData(new CharacterSaveData { currentHp = 10, maxHp = 100, currentMp = 0, maxMp = 20, learnedSkillIds = new List<string>() });
                pc.maxHp = 100; pc.maxMp = 20; pc.currentHp = 10; pc.currentMp = 0;
                Assert.That(pc.TryPayNegotiationResource(true, 10), Is.False);
                Assert.That(pc.TryPayNegotiationResource(false, 3), Is.False);
                Assert.That(pc.TryPayNegotiationResource(true, 5), Is.True);
                curse.id = StatusEffectID.Curse; curse.healingReceivedMultiplier = 0.5f;
                pc.StatusEffects.Apply(curse);
                Assert.That(pc.TryRestoreNegotiationResource(true, 10), Is.True);
                Assert.That(pc.currentHp, Is.EqualTo(10)); Assert.That(pc.sourceData.currentHp, Is.EqualTo(10));
                pc.currentHp = 99;
                Assert.That(pc.TryRestoreNegotiationResource(true, 10), Is.True);
                Assert.That(pc.currentHp, Is.EqualTo(100)); Assert.That(pc.sourceData.currentHp, Is.EqualTo(100));
                Assert.That(pc.TryRestoreNegotiationResource(true, 10), Is.False);
                pc.currentMp = 19;
                Assert.That(pc.TryRestoreNegotiationResource(false, 5), Is.True);
                Assert.That(pc.sourceData.currentMp, Is.EqualTo(20));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(curse); }
        }

        [Test]
        public void EscapeRemovesOneEnemyOccurrenceWithoutKillingIt()
        {
            var root = new GameObject("escape test"); root.SetActive(false);
            try
            {
                var manager = root.AddComponent<BattleManager>();
                var field = root.AddComponent<BattleFieldController>(); manager.fieldController = field;
                var go = new GameObject("monster"); go.transform.SetParent(root.transform, false);
                var monster = go.AddComponent<MonsterController>();
                monster.sourceData = new MonsterDatabase.MonsterEntry { id = "same_species", name = "test" };
                monster.maxHp = 100; monster.currentHp = 100;
                field.activeMonsters.Add(monster);
                field.encounterLog.Add(monster.sourceData); field.encounterLog.Add(monster.sourceData);
                typeof(BattleManager).GetField("negotiationTarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, monster);
                var method = typeof(BattleManager).GetMethod("TryFleeNegotiationTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((bool)method.Invoke(manager, null), Is.True);
                Assert.That(field.activeMonsters, Is.Empty);
                Assert.That(field.encounterLog.Count, Is.EqualTo(1));
                Assert.That(monster.currentHp, Is.EqualTo(100));
                Assert.That(monster.gameObject.activeSelf, Is.False);
                Assert.That((bool)method.Invoke(manager, null), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
