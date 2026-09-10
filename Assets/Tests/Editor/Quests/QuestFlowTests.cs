using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGProject.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RPGProject.Feature.Quests;
using RPGProject.Feature.Economy;
using RPGProject.Feature.Dialogue;
using RPGProject.Infrastructure.Persistence;

namespace RPGProject.Tests.Quests
{
    public sealed class QuestFlowTests
    {
        private GameObject host;
        private QuestManager quests;
        private FinanceManager finance;
        private QuestData definition;
        private object previousRoot;
        private FieldInfo rootField;
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Quest test managers"); host.SetActive(false);
            var root = host.AddComponent<ManagerRoot>();
            rootField = typeof(ManagerRoot).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            previousRoot = rootField.GetValue(null); rootField.SetValue(null, root);
            finance = host.AddComponent<FinanceManager>();
            quests = host.AddComponent<QuestManager>();
            Bind(root, "financeManager", finance); Bind(root, "questManager", quests);
            Bind(root, "flagManager", host.AddComponent<FlagManager>());
            definition = ScriptableObject.CreateInstance<QuestData>();
            definition.QuestID = "Q"; definition.QuestName = "Test"; definition.QuestType = "Sub";
            definition.locationID = "TEST"; definition.Reward = 100;
            definition.Targets.Add(new QuestTarget { monsterID = "A", requiredCount = 2 });
            quests.InitializeQuests(new List<QuestData> { definition });
        }
        private static void Bind(ManagerRoot root, string name, object value) =>
            typeof(ManagerRoot).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, value);
        [TearDown]
        public void TearDown()
        {
            rootField.SetValue(null, previousRoot);
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(definition);
        }
        private string Ready()
        {
            Assert.That(quests.TryAcceptQuest("Q", out _), Is.True);
            quests.ProcessBattleResult("TEST", new List<string> { "A", "A" });
            return quests.GetRunID("Q");
        }
        [Test]
        public void DestroyedCombatantsStayInRecordAndResultsApplyOnce()
        {
            var record = new QuestBattleRecord();
            record.Record(10, "A"); record.Record(10, "A"); record.Record(11, "A");
            Assert.That(record.Consume(), Is.EqualTo(new[] { "A", "A" }));
            Assert.That(record.Consume(), Is.Empty);
        }
        [Test]
        public void ClaimPaysOnceAndListenerObservesCommittedState()
        {
            string run = Ready();
            bool sawClaimed = false;
            finance.OnMoneyChanged += () => sawClaimed = quests.GetState("Q") == QuestState.Claimed;
            Assert.That(quests.TryClaimReward("Q", run, out var receipt, out _), Is.True);
            Assert.That(receipt.gold, Is.EqualTo(100)); Assert.That(sawClaimed, Is.True);
            Assert.That(quests.TryClaimReward("Q", run, out _, out _), Is.False);
            Assert.That(finance.CurrentMoney, Is.EqualTo(100));
        }
        [Test]
        public void ReentrantClaimCannotPayTwice()
        {
            string run = Ready(); bool inner = true;
            finance.OnMoneyChanged += () => inner = quests.TryClaimReward("Q", run, out _, out _);
            Assert.That(quests.TryClaimReward("Q", run, out _, out _), Is.True);
            Assert.That(inner, Is.False); Assert.That(finance.CurrentMoney, Is.EqualTo(100));
        }
        [Test]
        public void RepeatUsesNewRunAndRejectsStaleReceipt()
        {
            definition.QuestType = "Repeat";
            string first = Ready(); quests.TryClaimReward("Q", first, out _, out _);
            string second = Ready(); Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(quests.TryClaimReward("Q", first, out _, out _), Is.False);
            Assert.That(quests.TryClaimReward("Q", second, out _, out _), Is.True);
            Assert.That(finance.CurrentMoney, Is.EqualTo(200));
        }
        [Test]
        public void UnacceptedOrIncompleteQuestCannotClaim()
        {
            Assert.That(quests.TryClaimReward("Q", "none", out _, out _), Is.False);
            quests.TryAcceptQuest("Q", out _);
            Assert.That(quests.TryClaimReward("Q", quests.GetRunID("Q"), out _, out _), Is.False);
        }
        [Test]
        public void WrongLocationDoesNotAdvanceAndExtraKillsAreClamped()
        {
            quests.TryAcceptQuest("Q", out _);
            quests.ProcessBattleResult("OTHER", new List<string> { "A", "A" });
            Assert.That(quests.GetKillCount("Q", "A"), Is.Zero);
            Assert.That(quests.ProcessBattleResult("TEST", new List<string> { "A", "A", "A" }).Count, Is.EqualTo(1));
            Assert.That(quests.GetKillCount("Q", "A"), Is.EqualTo(2));
            Assert.That(quests.ProcessBattleResult("TEST", new List<string> { "A" }), Is.Empty);
        }
        [Test]
        public void AcceptValidatesCapacityAndPrerequisitesInManager()
        {
            var other = ScriptableObject.CreateInstance<QuestData>();
            try
            {
                other.QuestID = "B"; other.locationID = "TEST"; other.Targets.Add(new QuestTarget { monsterID = "A", requiredCount = 1 });
                other.prerequisiteQuestIDs.Add("Q");
                quests.InitializeQuests(new List<QuestData> { definition, other });
                Assert.That(quests.TryAcceptQuest("B", out _), Is.False);
                string run = Ready();
                other.prerequisiteQuestIDs.Clear();
                Assert.That(quests.TryAcceptQuest("B", out _), Is.False); // Capacity rechecked at commit.
                quests.TryClaimReward("Q", run, out _, out _);
                Assert.That(quests.TryAcceptQuest("B", out _), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
        }
        [Test]
        public void SaveIsDeepCopyAndReadyRoundTrips()
        {
            string run = Ready(); var data = new SaveData(); quests.Save(data);
            quests.NewGame(); quests.Load(data);
            Assert.That(quests.GetState("Q"), Is.EqualTo(QuestState.ReadyToReport));
            Assert.That(quests.GetRunID("Q"), Is.EqualTo(run));
            data.activeQuests[0].killCounts["A"] = 0;
            Assert.That(quests.GetKillCount("Q", "A"), Is.EqualTo(2));
        }
        [Test]
        public void LegacySaveRepairsKeysCountsAndRunId()
        {
            var data = new SaveData();
            data.activeQuests.Add(new QuestProgress { questID = "deleted" });
            data.activeQuests.Add(new QuestProgress { questID = "Q", isReadyToReport = true,
                killCounts = new Dictionary<string, int> { { "removedTarget", 99 }, { "A", -4 } } });
            quests.Load(data);
            Assert.That(quests.GetActiveQuests().Count, Is.EqualTo(1));
            Assert.That(quests.GetKillCount("Q", "A"), Is.Zero);
            Assert.That(quests.GetState("Q"), Is.EqualTo(QuestState.Active));
            Assert.That(quests.GetRunID("Q"), Is.Not.Empty);
        }
        [Test]
        public void OverflowKeepsQuestReadyWithoutPayment()
        {
            string run = Ready(); finance.SetMoney(int.MaxValue - 50);
            Assert.That(quests.TryClaimReward("Q", run, out _, out _), Is.False);
            Assert.That(quests.GetState("Q"), Is.EqualTo(QuestState.ReadyToReport));
            Assert.That(finance.CurrentMoney, Is.EqualTo(int.MaxValue - 50));
        }
        [Test]
        public void NegotiationAlternativeCompletesWithoutInventingKills()
        {
            definition.completionFlag = "QuestTalk_TEST_A";
            quests.TryAcceptQuest("Q", out _);
            quests.RecordNegotiation("OTHER", "A");
            Assert.That(quests.RefreshExternalGoals(), Is.Empty);
            quests.RecordNegotiation("TEST", "A");
            Assert.That(quests.RefreshExternalGoals().Count, Is.EqualTo(1));
            Assert.That(quests.GetKillCount("Q", "A"), Is.Zero);
            Assert.That(quests.GetState("Q"), Is.EqualTo(QuestState.ReadyToReport));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidTargetCountIsRejected(int count)
        {
            definition.Targets[0].requiredCount = count;
            Assert.That(QuestDefinitionValidation.Error(definition), Is.Not.Null);
        }
        [Test]
        public void BackupSurvivesCorruptPrimarySave()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            try
            {
                SaveManager.WriteSaveAtomically(path, "{\"sceneName\":\"First\"}");
                SaveManager.WriteSaveAtomically(path, "{\"sceneName\":\"Second\"}");
                File.WriteAllText(path, "broken");
                LogAssert.Expect(LogType.Warning, "기본 세이브를 읽지 못해 이전 백업을 불러옵니다.");
                Assert.That(SaveManager.ReadSaveWithBackup(path).sceneName, Is.EqualTo("First"));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
