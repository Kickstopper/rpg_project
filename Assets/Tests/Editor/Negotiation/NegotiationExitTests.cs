using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Feature.Negotiation;
using UnityEngine;

namespace RPGProject.Tests.Negotiation
{
    public sealed class NegotiationExitTests
    {
        private static FieldInfo Field(string name) => typeof(BattleManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Invoke(BattleManager manager, string method, params object[] args) =>
            typeof(BattleManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args);

        [TestCase(0f, true)]
        [TestCase(0.99f, false)]
        public void WithdrawalHasTwoOutcomesAndDrawsOnlyOnce(float roll, bool peaceful)
        {
            int draws = 0;
            var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 40, 40,
                _ => throw new Exception("No withdrawal cost"), () => false, () => false,
                roll: () => { draws++; return roll; });
            string result = session.Resolve("leave", "CHECK_MOOD:WITHDRAW");
            Assert.That(result, Is.EqualTo(peaceful ? "WITHDRAW_PEACE" : "WITHDRAW_HOSTILE"));
            Assert.That(session.ShouldEndBattle, Is.EqualTo(peaceful));
            Assert.That(session.HasPaidTrade, Is.False);
            Assert.That(session.Resolve("leave", "CHECK_MOOD:WITHDRAW"), Is.EqualTo(result));
            Assert.That(session.Resolve("other-leave", "CHECK_MOOD:WITHDRAW"), Is.EqualTo("END"));
            Assert.That(session.Resolve("trade-after-withdrawal", "CHECK_MOOD:TRADE:Gold"), Is.EqualTo("END"));
            Assert.That(draws, Is.EqualTo(1));
            session.Close(); Assert.That(session.ShouldEndBattle, Is.EqualTo(peaceful));
        }

        [Test]
        public void ProgressAndFriendlinessHelpButHighAngerPreventsPeace()
        {
            foreach (Personality p in Enum.GetValues(typeof(Personality)))
            {
                float early = NegotiationWithdrawalRules.PeaceChance(p, 0, 0, 0, 0);
                Assert.That(NegotiationWithdrawalRules.PeaceChance(p, 0, 50, 50, 3), Is.GreaterThan(early));
                Assert.That(NegotiationWithdrawalRules.PeaceChance(p, 74, 50, 50, 3),
                    Is.LessThan(NegotiationWithdrawalRules.PeaceChance(p, 0, 50, 50, 3)));
                Assert.That(NegotiationWithdrawalRules.IsPeaceful(p, 75, 100, 100, 4, 0f), Is.False);
                Assert.That(NegotiationWithdrawalRules.IsPeaceful(p, 100, 100, 100, 8, 0f), Is.False);
            }
            Assert.That(NegotiationWithdrawalRules.BasePeaceChance(Personality.Rational),
                Is.GreaterThan(NegotiationWithdrawalRules.BasePeaceChance(Personality.Aggressive)));
        }

        [Test]
        public void ExplicitLeaveAtAngerLimitShowsHostileWithdrawalRatherThanGenericFailure()
        {
            var session = new NegotiationSession(Personality.Aggressive, Race.Beast, default, 100, 100, 100,
                _ => false, () => false, () => false, roll: () => 0f);
            Assert.That(session.Resolve("leave", "CHECK_MOOD:WITHDRAW"), Is.EqualTo("WITHDRAW_HOSTILE"));
            Assert.That(session.ShouldEndBattle, Is.False);
        }

        [Test]
        public void EveryResolvedCsvProfileAdaptsAllFiveLeaveChoicesAndKeepsSuccessNodes()
        {
            var catalog = NegotiationDialogueCatalog.Create(DialogueCsv.Read(File.ReadAllText("Assets/CSV/Dialogues/Negotiation.csv")));
            foreach (Personality p in Enum.GetValues(typeof(Personality)))
            foreach (Race race in Enum.GetValues(typeof(Race)))
            foreach (Gender gender in Enum.GetValues(typeof(Gender)))
            {
                var rows = catalog.Resolve(p, race, gender, out _);
                int countBefore = rows.Count;
                NegotiationWithdrawalRules.PrepareScript(rows, p, "테스트");
                Assert.That(NegotiationScriptValidator.Validate(rows), Is.Empty);
                Assert.That(rows.FindAll(r => NegotiationScriptValidator.Value(r, "NextID") == "CHECK_MOOD:WITHDRAW").Count, Is.EqualTo(5));
                Assert.That(rows.Find(r => r["Seq"] == "SUCCESS_RECRUIT")["NextID"], Is.EqualTo("END"));
                Assert.That(rows.Count, Is.EqualTo(countBefore + 2));
                NegotiationWithdrawalRules.PrepareScript(rows, p, "테스트");
                Assert.That(rows.Count, Is.EqualTo(countBefore + 2));
                Assert.That(catalog.Resolve(p, race, gender, out _).Count, Is.EqualTo(countBefore));
            }
        }

        [TestCase("ProcessTurn")]
        [TestCase("ProcessEnemyTurn")]
        [TestCase("PreparePlayerTurn")]
        [TestCase("NextPlayerInput")]
        public void RemainingEnemiesCannotStartATurnWhileNegotiatingOrExitPending(string entry)
        {
            var go = new GameObject("turn gate"); go.SetActive(false);
            try
            {
                var manager = go.AddComponent<BattleManager>();
                var field = go.AddComponent<BattleFieldController>(); manager.fieldController = field;
                for (int i = 0; i < 2; i++)
                {
                    var enemy = new GameObject("remaining enemy"); enemy.transform.SetParent(go.transform);
                    var monster = enemy.AddComponent<MonsterController>(); monster.maxHp = 100; monster.currentHp = 100;
                    field.activeMonsters.Add(monster);
                }
                var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 0, 0, _ => false, () => false, () => false);
                Field("negotiationSession").SetValue(manager, session);
                // No UI/managers are wired: entering any unguarded phase would fail this test.
                Assert.DoesNotThrow(() => Invoke(manager, entry));
                Field("negotiationSession").SetValue(manager, null);
                Field("negotiationExitPending").SetValue(manager, true);
                Assert.DoesNotThrow(() => Invoke(manager, entry));
                Assert.That(field.activeMonsters.Count, Is.EqualTo(2));
                Assert.That(((IEnumerator)Invoke(manager, "ExecuteActions")).MoveNext(), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DialogueCloseInsideRecruitCallbackIsDeferredUntilOutcomeIsWritten()
        {
            var go = new GameObject("reentrant completion"); go.SetActive(false);
            try
            {
                var manager = go.AddComponent<BattleManager>();
                NegotiationSession session = null;
                session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                    _ => true, () =>
                    {
                        Assert.That(session.Recruited, Is.False);
                        session.Close(); // DialogueUI closes its session before invoking BattleManager.
                        Assert.DoesNotThrow(() => Invoke(manager, "OnNegotiationEnded", 0));
                        Assert.That(Field("negotiationSession").GetValue(manager), Is.SameAs(session));
                        Assert.That(Field("deferredNegotiationEnd").GetValue(manager), Is.True);
                        return true;
                    }, () => false);
                Field("negotiationSession").SetValue(manager, session);
                Assert.That(session.Resolve("recruit", "CHECK_MOOD:RECRUIT"), Is.EqualTo("SUCCESS_RECRUIT"));
                Assert.That(session.ShouldEndBattle, Is.True);
                Assert.That(session.IsResolving, Is.False);
                // Suppress scene presentation only. Falling through to ProcessTurn would dereference missing UI.
                Field("isEndingBattle").SetValue(manager, true);
                Assert.DoesNotThrow(() => Invoke(manager, "OnNegotiationEnded", 0));
                Assert.That(Field("negotiationSession").GetValue(manager), Is.Null);
                Assert.That(Field("deferredNegotiationEnd").GetValue(manager), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
