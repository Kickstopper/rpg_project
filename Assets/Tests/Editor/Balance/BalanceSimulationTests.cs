using System.Collections.Generic;
using System.Linq;
using Data;
using NUnit.Framework;
using RPGProject.Balance;
using UnityEngine;

namespace RPGProject.Tests.Balance
{
    public sealed class BalanceSimulationTests
    {
        BalanceScenario scenario;
        readonly List<Object> owned = new List<Object>();
        [SetUp] public void Setup()
        {
            scenario = ScriptableObject.CreateInstance<BalanceScenario>(); owned.Add(scenario);
            scenario.trials = 3; scenario.useDatabaseAI = false;
            scenario.party = new List<BalanceSlot> { new BalanceSlot { label = "P", commander = true, policy = SimPolicy.BasicAttack } };
            scenario.enemies = new List<BalanceSlot> { new BalanceSlot { label = "E", policy = SimPolicy.BasicAttack } };
        }
        [TearDown] public void Cleanup() { foreach (var o in owned) Object.DestroyImmediate(o); owned.Clear(); }
        static void Finish(BalanceSimulation simulation)
        {
            int safety = 10000;
            while (!simulation.Done && safety-- > 0) simulation.Step();
            Assert.IsTrue(simulation.Done);
        }
        [Test] public void SameSeedProducesSameOutcomeAndTrace()
        {
            using (var a = new BalanceSimulation(scenario, 123, true))
            using (var b = new BalanceSimulation(scenario, 123, true))
            {
                Finish(a); Finish(b); Assert.AreEqual(JsonUtility.ToJson(a.Result), JsonUtility.ToJson(b.Result));
                Assert.AreEqual(a.Frames.Count, b.Frames.Count);
                for (int i = 0; i < a.Frames.Count; i++) Assert.AreEqual(JsonUtility.ToJson(a.Frames[i]), JsonUtility.ToJson(b.Frames[i]));
            }
        }
        [Test] public void SimulationDoesNotConsumeEditorRandomState()
        {
            var prior = Random.state;
            try
            {
                Random.InitState(776); var state = Random.state; float expected = Random.value; Random.state = state;
                using (var sim = new BalanceSimulation(scenario, 42, false)) Finish(sim);
                Assert.AreEqual(expected, Random.value);
            }
            finally { Random.state = prior; }
        }
        [Test] public void PhysicalImmunityEndsAsTimeoutNotLoss()
        {
            scenario.maxRounds = 2;
            scenario.party[0].resistance = new ResistanceData { phys = ResistTier.Null };
            scenario.enemies[0].resistance = new ResistanceData { phys = ResistTier.Null };
            using (var sim = new BalanceSimulation(scenario, 8, true))
            {
                Finish(sim); Assert.AreEqual(SimOutcome.Timeout, sim.Result.outcome); Assert.AreEqual(2, sim.Result.rounds);
                Assert.AreEqual(1, sim.Result.remainingHpRatio);
            }
        }
        [Test] public void DeadCommanderEndsAtEntry()
        {
            scenario.party[0].hpRatio = 0;
            using (var sim = new BalanceSimulation(scenario, 1, true))
            { Assert.IsTrue(sim.Done); Assert.AreEqual(SimOutcome.Loss, sim.Result.outcome); Assert.AreEqual(0, sim.Result.actions); }
        }
        [Test] public void SleepBlocksActionAndPersistsAgainstNullDamage()
        {
            var sleep = ScriptableObject.CreateInstance<StatusEffectData>(); owned.Add(sleep);
            sleep.id = StatusEffectID.Sleep; sleep.restrictionType = RestrictionType.SkipTurn; sleep.restrictionChance = 1;
            sleep.cureType = EffectCureType.ExplicitOnly;
            scenario.party[0].startingStatuses.Add(sleep); scenario.maxRounds = 1;
            scenario.party[0].resistance = new ResistanceData { phys = ResistTier.Null };
            using (var sim = new BalanceSimulation(scenario, 2, true))
            {
                sim.Step(); StringAssert.Contains("행동 불가", sim.Frames.Last().message);
                Finish(sim); Assert.AreEqual(SimOutcome.Timeout, sim.Result.outcome);
            }
        }
        [Test] public void TraceBoundDoesNotStopCalculation()
        {
            scenario.replayFrameLimit = 1;
            using (var sim = new BalanceSimulation(scenario, 3, true))
            { Finish(sim); Assert.AreEqual(1, sim.Frames.Count); Assert.IsTrue(sim.TraceTruncated); }
        }
        [Test] public void CancelDoesNotCountUnfinishedTrial()
        {
            using (var batch = new BalanceBatch(scenario, false))
            { batch.Cancel(); Assert.AreEqual(0, batch.Completed); Assert.IsFalse(batch.Running); Assert.IsTrue(batch.Reports[0].cancelled); }
        }
        [Test] public void SweepUsesSameSeedSequenceAndDistinctFrozenScales()
        {
            using (var batch = new BalanceBatch(scenario, true))
            {
                Assert.AreEqual(3, batch.Configurations.Count);
                Assert.AreEqual(.8f, batch.Configurations[0].enemyStatScale, .001f);
                Assert.AreEqual(1.2f, batch.Configurations[2].enemyStatScale, .001f);
                scenario.enemyStatScale = 2;
                Assert.AreEqual(1f, batch.Configurations[1].enemyStatScale);
                int safety = 5000; while (batch.Running && safety-- > 0) batch.Tick(10);
                Assert.IsFalse(batch.Running); Assert.IsNull(batch.Error);
                CollectionAssert.AreEqual(batch.Reports[0].trials.Select(t => t.seed), batch.Reports[2].trials.Select(t => t.seed));
            }
        }
        [Test] public void ReferencedStatusDefinitionIsFrozen()
        {
            var poison = ScriptableObject.CreateInstance<StatusEffectData>(); owned.Add(poison);
            poison.id = StatusEffectID.Poison; poison.dotDamage = 5; scenario.party[0].startingStatuses.Add(poison);
            using (var batch = new BalanceBatch(scenario, false))
            { poison.dotDamage = 999; Assert.AreEqual(5, batch.Configurations[0].party[0].startingStatuses[0].dotDamage); }
        }
        [Test] public void EmptyOrDuplicatePartyIsRejected()
        {
            scenario.party.Add(new BalanceSlot()); Assert.IsNotEmpty(BalanceSimulation.Validate(scenario));
            scenario.party.Clear(); Assert.IsNotEmpty(BalanceSimulation.Validate(scenario));
        }
        [Test] public void WilsonIntervalHandlesZeroAndAllWins()
        {
            Assert.AreEqual(Vector2.zero, SimReport.Wilson(0, 0));
            var ci = SimReport.Wilson(10, 10); Assert.Greater(ci.x, .7f); Assert.AreEqual(1f, ci.y, .0001f);
        }
        [Test] public void DisposeRemovesTemporaryObjects()
        {
            int before = Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "Balance simulation (temporary)");
            using (var sim = new BalanceSimulation(scenario, 8, false)) sim.Step();
            Assert.AreEqual(before, Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "Balance simulation (temporary)"));
        }
    }
}
