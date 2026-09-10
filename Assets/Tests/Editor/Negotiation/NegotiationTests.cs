using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Feature.Negotiation;
using RPGProject.Infrastructure.Persistence;
using UnityEngine;

namespace RPGProject.Tests.Negotiation
{
    public sealed class NegotiationTests
    {
        private static NegotiationSession Session(Func<NegotiationDemand, bool> pay = null,
            Func<bool> recruit = null, Func<bool> item = null, int joy = 0, int interest = 0,
            Personality personality = Personality.Foolish, int anger = 0)
        {
            return new NegotiationSession(personality, Race.Demon, default, anger, joy, interest, pay, recruit, item);
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("-100")]
        [TestCase("999999999999999999999999")]
        [TestCase("HP_-1")]
        [TestCase("MP_bad")]
        [TestCase("Gold_0")]
        public void MalformedDemandIsRejected(string value)
        {
            Assert.That(NegotiationDemand.TryParse(value, out _), Is.False);
        }

        [TestCase("100", DemandKind.Gold, 100)]
        [TestCase("Gold_100", DemandKind.Gold, 100)]
        [TestCase("HP_50", DemandKind.HP, 50)]
        [TestCase("MP_20", DemandKind.MP, 20)]
        [TestCase("Potion", DemandKind.Item, 1)]
        public void DemandFormatsAreParsed(string value, DemandKind kind, int amount)
        {
            Assert.That(NegotiationDemand.TryParse(value, out var demand), Is.True);
            Assert.That(demand.Kind, Is.EqualTo(kind));
            Assert.That(demand.Amount, Is.EqualTo(amount));
        }

        [Test]
        public void FailedPaymentAndOfferingMoneyGiveNoMoodBonus()
        {
            int calls = 0;
            var session = Session(pay: _ => { calls++; return false; });
            session.ApplyTone(ChoiceTone.Bribe);
            session.ApplyTone(ChoiceTone.Accept);
            Assert.That(session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100"), Is.EqualTo("INSUFFICIENT_ITEM"));
            Assert.That(session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100"), Is.EqualTo("INSUFFICIENT_ITEM"));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(session.Joy, Is.Zero);
            Assert.That(session.Interest, Is.Zero);
        }

        [Test]
        public void SuccessfulPaymentIsAppliedOncePerNode()
        {
            int gold = 100;
            var session = Session(pay: d => { if (gold < d.Amount) return false; gold -= d.Amount; return true; });
            Assert.That(session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100"), Is.EqualTo("NEGO_START"));
            session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100");
            Assert.That(gold, Is.Zero);
            Assert.That(session.Joy, Is.EqualTo(45));
            Assert.That(session.Interest, Is.EqualTo(30));
        }

        [Test]
        public void RewardsCommitOnceAndClosedSessionCannotMutate()
        {
            int recruits = 0, items = 0, payments = 0;
            var session = Session(_ => { payments++; return true; }, () => { recruits++; return true; },
                () => { items++; return true; }, 100, 100);
            Assert.That(session.Resolve("18", "CHECK_MOOD:RECRUIT"), Is.EqualTo("SUCCESS_RECRUIT"));
            session.Resolve("otherRecruit", "CHECK_MOOD:RECRUIT");
            Assert.That(session.Resolve("19", "CHECK_MOOD:ITEM"), Is.EqualTo("SUCCESS_ITEM"));
            session.Resolve("otherItem", "CHECK_MOOD:ITEM");
            Assert.That(recruits, Is.EqualTo(1));
            Assert.That(items, Is.EqualTo(1));
            session.Close();
            Assert.That(session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100"), Is.EqualTo("END"));
            Assert.That(payments, Is.Zero);
        }

        [Test]
        public void FailedRecruitmentOrUnavailableRewardCannotShowSuccess()
        {
            var session = Session(recruit: () => false, item: () => false, joy: 100, interest: 100);
            Assert.That(session.Resolve("18", "CHECK_MOOD:RECRUIT"), Is.EqualTo("FAIL_RECRUIT"));
            Assert.That(session.Resolve("19", "CHECK_MOOD:ITEM"), Is.EqualTo("FAIL_ITEM"));
        }

        [Test]
        public void AngerThresholdPreventsSpendingAndRecruitment()
        {
            var session = Session(_ => throw new Exception("must not spend"),
                () => throw new Exception("must not recruit"), joy: 100, interest: 100, anger: 100);
            Assert.That(session.MustStop, Is.True);
            Assert.That(session.Resolve("18", "CHECK_MOOD:RECRUIT"), Is.EqualTo("FAIL"));
            Assert.That(session.Resolve("22", "CHECK_MOOD:GIVE:ACCEPT:100"), Is.EqualTo("FAIL"));
        }

        [Test]
        public void ActualEnvironmentChangesReaction()
        {
            var calm = NegotiationCalculator.CalculateMoodChange(ChoiceTone.Gentle, Personality.Polite, Race.Beast, default);
            var fullRain = NegotiationCalculator.CalculateMoodChange(ChoiceTone.Gentle, Personality.Polite, Race.Beast,
                new EnvironmentState { moonPhase = MoonPhase.Full, weather = Weather.Rain });
            Assert.That(fullRain.addedAnger, Is.GreaterThan(calm.addedAnger));
            Assert.That(fullRain.addedJoy, Is.LessThan(calm.addedJoy));
        }

        [TestCase(Personality.Polite)]
        [TestCase(Personality.Aggressive)]
        [TestCase(Personality.Sly)]
        [TestCase(Personality.Foolish)]
        [TestCase(Personality.Childish)]
        public void EveryPersonalityHasRecruitAndItemPaths(Personality personality)
        {
            int anger = 0, joy = 0, interest = 0;
            bool recruited = false, item = false;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var session = new NegotiationSession(personality, Race.Demon, default, anger, joy, interest,
                    _ => true, () => true, () => true);
                session.ApplyTone(personality == Personality.Aggressive ? ChoiceTone.Threat : ChoiceTone.Gentle);
                session.ApplyTone(personality == Personality.Aggressive ? ChoiceTone.Flirt : ChoiceTone.Relieve);
                session.ApplyTone(ChoiceTone.Persuade);
                recruited |= session.Resolve("18", "CHECK_MOOD:RECRUIT") == "SUCCESS_RECRUIT";
                item |= session.Resolve("19", "CHECK_MOOD:ITEM") == "SUCCESS_ITEM";
                anger = session.Anger; joy = session.Joy; interest = session.Interest;
                session.Close();
            }
            Assert.That(recruited, Is.True);
            Assert.That(item, Is.True);
        }

        [Test]
        public void IncludedCsvPassesStrictParserAndGraphValidation()
        {
            var rows = DialogueCsv.Read(File.ReadAllText("Assets/CSV/Dialogues/Negotiation.csv"));
            Assert.That(rows.Any(r => r["EventID"] == "DEFAULT"), Is.True);
            foreach (var group in rows.GroupBy(r => r["EventID"]))
                Assert.That(NegotiationScriptValidator.Validate(group.ToList()), Is.Empty, group.Key);
        }

        [Test]
        public void ValidatorRejectsMissingAmountDuplicateSeqAndMissingTarget()
        {
            var rows = DialogueCsv.Read(File.ReadAllText("Assets/CSV/Dialogues/Negotiation.csv"))
                .Where(r => r["EventID"] == "DEFAULT").ToList();
            rows.First(r => r["Seq"] == "22")["NextID"] = "CHECK_MOOD:GIVE:ACCEPT";
            rows.Add(new Dictionary<string, string>(rows[0]));
            rows.First(r => r["Seq"] == "18")["NextID"] = "DOES_NOT_EXIST";
            Assert.That(NegotiationScriptValidator.Validate(rows).Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void InactivePartyStillPaysResourcesAndCannotPayLethalHp()
        {
            var root = new GameObject("Inactive negotiation payer");
            root.SetActive(false);
            try
            {
                var pc = root.AddComponent<PlayerController>();
                pc.sourceData = new RuntimeCharacterData(new CharacterSaveData
                { currentHp = 100, maxHp = 100, currentMp = 20, maxMp = 20, learnedSkillIds = new List<string>() });
                pc.currentHp = pc.maxHp = 100;
                pc.currentMp = pc.maxMp = 20;
                Assert.That(pc.TryPayNegotiationResource(true, 50), Is.True);
                Assert.That(pc.currentHp, Is.EqualTo(50));
                Assert.That(pc.sourceData.currentHp, Is.EqualTo(50));
                Assert.That(pc.TryPayNegotiationResource(true, 50), Is.False);
                Assert.That(pc.TryPayNegotiationResource(false, 21), Is.False);
                Assert.That(pc.TryPayNegotiationResource(false, -1), Is.False);
                Assert.That(pc.TryPayNegotiationResource(false, 20), Is.True);
                Assert.That(pc.currentMp, Is.Zero);
                Assert.That(pc.sourceData.currentMp, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
