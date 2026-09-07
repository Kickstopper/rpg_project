using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Controller;
using Helper;
using NUnit.Framework;
using UI;
using UI.Battle;
using UI.DungeonMapScene;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace RPGProject.Tests.Battle
{
    public sealed class BattleOptimizationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private Random.State randomState;
        private float timeScale;

        [SetUp]
        public void SetUp()
        {
            randomState = Random.state;
            timeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
            Random.state = randomState;
            Time.timeScale = timeScale;
        }

        private GameObject MakeObject(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            return go;
        }

        private BattleTestEntity Entity(string name, int hp = 10)
        {
            var entity = MakeObject(name).AddComponent<BattleTestEntity>();
            entity.maxHp = entity.maxMp = 100;
            entity.currentHp = hp;
            return entity;
        }

        private BattleFieldController Field()
        {
            var field = MakeObject("Field").AddComponent<BattleFieldController>();
            field.enemyFrontRowContainer = MakeObject("EnemyFront").transform;
            field.enemyBackRowContainer = MakeObject("EnemyBack").transform;
            field.playerFrontRowContainer = MakeObject("PlayerFront").transform;
            field.playerBackRowContainer = MakeObject("PlayerBack").transform;
            return field;
        }

        private void Place(BattleEntity entity, Transform row, int column)
        {
            Transform slot = MakeObject("Slot").transform;
            slot.SetParent(row, false);
            entity.transform.SetParent(slot, false);
            entity.columnIndex = column;
        }

        [Test]
        public void StableSortMatchesLinqIncludingTiesAndExtremeSpeeds()
        {
            var random = new System.Random(4281);
            for (int round = 0; round < 500; round++)
            {
                var actions = new List<BattleAction>();
                int count = random.Next(0, 25);
                for (int i = 0; i < count; i++)
                    actions.Add(new BattleAction(null, null, ActionType.Attack, random.Next(-3, 4)));
                if (round % 3 == 0)
                {
                    actions.Add(new BattleAction(null, null, ActionType.Guard, int.MinValue));
                    actions.Add(new BattleAction(null, null, ActionType.Next, int.MaxValue));
                }
                BattleAction[] expected = actions.OrderByDescending(action => action.speed).ToArray();
                BattleCollectionUtility.SortActionsBySpeed(actions);
                CollectionAssert.AreEqual(expected, actions);
            }
        }

        [Test]
        public void LivingSnapshotsAreIndependentAndBuffersRejectSourceAliasing()
        {
            BattleFieldController field = Field();
            BattleTestEntity alive = Entity("Alive");
            field.activeMonsters.AddRange(new BattleEntity[] { null, alive, Entity("Dead", 0) });
            var first = field.GetLivingMonsters();
            var second = field.GetLivingMonsters();
            first.Clear();
            Assert.That(field.LivingMonsterCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { alive }, second);
            var destination = new List<BattleEntity> { null, null };
            field.FillLivingMonsters(destination);
            CollectionAssert.AreEqual(second, destination);
            Assert.Throws<ArgumentException>(() => field.FillLivingMonsters(field.activeMonsters));
            Assert.That(field.activeMonsters.Count, Is.EqualTo(3));
        }

        [Test]
        public void LivingAveragesMatchOriginalIntegerAverageRounding()
        {
            BattleTestEntity a = Entity("A");
            BattleTestEntity b = Entity("B");
            a.agility = int.MaxValue;
            b.agility = int.MaxValue - 1;
            a.luck = 17;
            b.luck = 38;
            a.level = 3;
            b.level = 8;
            var source = new List<BattleEntity> { a, Entity("Dead", 0), null, b };
            int count = BattleCollectionUtility.GetLivingAverages(source, true, out float agility, out float luck, out float level);
            var expected = new[] { a, b };
            Assert.That(count, Is.EqualTo(2));
            Assert.That(agility, Is.EqualTo((float)expected.Average(e => e.agility)));
            Assert.That(luck, Is.EqualTo((float)expected.Average(e => e.luck)));
            Assert.That(level, Is.EqualTo((float)expected.Average(e => e.level)));
            Assert.That(BattleCollectionUtility.GetLivingAverages(new List<BattleEntity>(), false,
                out agility, out luck, out level), Is.Zero);
            Assert.That(agility + luck + level, Is.Zero);
        }

        [Test]
        public void HpAndMpRefreshOnlyWhenClampedValueChanges()
        {
            BattleTestEntity entity = Entity("Entity", 0);
            entity.uiUpdates = 0;
            entity.currentHp = -1;
            entity.currentMp = -100;
            Assert.That(entity.uiUpdates, Is.Zero);
            entity.currentHp = 500;
            entity.currentMp = 500;
            Assert.That(entity.uiUpdates, Is.EqualTo(2));
            entity.currentHp = 101;
            entity.currentMp = 999;
            Assert.That(entity.uiUpdates, Is.EqualTo(2));
            entity.maxHp = 50;
            entity.currentHp = 100;
            Assert.That(entity.currentHp, Is.EqualTo(50));
            Assert.That(entity.uiUpdates, Is.EqualTo(3));
        }

        [TestCase(TargetScope.Single_Enemy)]
        [TestCase(TargetScope.Front_Single_Enemy)]
        [TestCase(TargetScope.One_Ally)]
        [TestCase(TargetScope.Dead_Ally)]
        public void SingleScopesKeepSpecifiedTargetAndClearOldBuffer(TargetScope scope)
        {
            BattleFieldController field = Field();
            GameObject target = Entity("Specified", 0).gameObject;
            var buffer = new List<GameObject> { null, null };
            field.FillTargetsByScope(scope, null, target, buffer);
            CollectionAssert.AreEqual(new[] { target }, buffer);
            field.FillTargetsByScope(scope, null, null, buffer);
            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void GroupScopesPreserveOrderAndFrontFallback()
        {
            BattleFieldController field = Field();
            BattleTestEntity back = Entity("Back");
            BattleTestEntity front = Entity("Front");
            Place(back, field.enemyBackRowContainer, 0);
            Place(front, field.enemyFrontRowContainer, 1);
            field.activeMonsters.AddRange(new[] { back, front, Entity("Dead", 0) });
            var buffer = new List<GameObject>();
            field.FillTargetsByScope(TargetScope.All_Enemies, null, null, buffer);
            CollectionAssert.AreEqual(new[] { back.gameObject, front.gameObject }, buffer);
            field.FillTargetsByScope(TargetScope.Front_Enemies, null, null, buffer);
            CollectionAssert.AreEqual(new[] { front.gameObject }, buffer);
            front.currentHp = 0;
            field.FillTargetsByScope(TargetScope.Front_Enemies, null, null, buffer);
            CollectionAssert.AreEqual(new[] { back.gameObject }, buffer);
            field.FillTargetsByScope(TargetScope.Random_Front_Enemy, null, null, buffer);
            Assert.That(buffer, Is.Empty); // Existing execution rule; no fallback for random front.
            field.SetValidTargetsByTargetScope(TargetScope.Front_Enemies);
            CollectionAssert.AreEqual(new[] { back }, field.validTargets);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RandomSelectionMatchesOriginalCandidateOrderAndRandomState(bool frontOnly)
        {
            BattleFieldController field = Field();
            var candidates = new List<BattleEntity>();
            for (int i = 0; i < 6; i++)
            {
                BattleTestEntity entity = Entity("Enemy" + i, i == 2 ? 0 : 10);
                Place(entity, i % 2 == 0 ? field.enemyFrontRowContainer : field.enemyBackRowContainer, i % 3);
                field.activeMonsters.Add(entity);
                if (entity.currentHp > 0 && (!frontOnly || i % 2 == 0)) candidates.Add(entity);
            }
            for (int seed = 0; seed < 50; seed++)
            {
                Random.InitState(seed);
                BattleEntity expected = candidates[Random.Range(0, candidates.Count)];
                float expectedNextRandom = Random.value;
                Random.InitState(seed);
                Assert.That(field.GetRandomLivingMonster(frontOnly), Is.SameAs(expected));
                Assert.That(Random.value, Is.EqualTo(expectedNextRandom));
            }
        }

        [Test]
        public void AllDeadAlliesIncludesOnlyOccupiedDeadPlayers()
        {
            BattleFieldController field = Field();
            var dead = MakeObject("DeadPlayer").AddComponent<PlayerController>();
            var alive = MakeObject("AlivePlayer").AddComponent<PlayerController>();
            alive.maxHp = 10;
            alive.currentHp = 10;
            var empty = MakeObject("EmptyPlayer").AddComponent<PlayerController>();
            empty.InitializeEmpty(0);
            field.activePlayers.AddRange(new BattleEntity[] { dead, alive, empty, null });
            Assert.That(field.GetFirstDeadPartyMember(), Is.SameAs(dead));
            CollectionAssert.AreEqual(new[] { dead.gameObject },
                field.GetTargetsByScope(TargetScope.All_Dead_Allies, alive.gameObject, dead.gameObject));
            CollectionAssert.AreEqual(new[] { alive.gameObject },
                field.GetTargetsByScope(TargetScope.All_Allies, alive.gameObject, null));
            CollectionAssert.AreEqual(new[] { alive.gameObject },
                field.GetTargetsByScope(TargetScope.Self, alive.gameObject, null));
        }

        [Test]
        public void EmptyRandomSelectionDoesNotAdvanceRandomState()
        {
            BattleFieldController field = Field();
            Random.InitState(1234);
            float expected = Random.value;
            Random.InitState(1234);
            Assert.That(field.GetRandomLivingMonster(false), Is.Null);
            Assert.That(Random.value, Is.EqualTo(expected));
            Assert.That(field.GetFirstDeadPartyMember(), Is.Null);
            Assert.That(field.GetCurrentValidTarget(), Is.Null);
            Assert.That(field.GetCurrentCharacter(), Is.Null);
        }

        [Test]
        public void TargetSortingIsStableAndDoesNotMutateCallerList()
        {
            BattleFieldController field = Field();
            BattleTestEntity back = Entity("Back");
            BattleTestEntity frontA = Entity("FrontA");
            BattleTestEntity frontB = Entity("FrontB");
            Place(back, field.enemyBackRowContainer, 0);
            Place(frontA, field.enemyFrontRowContainer, 2);
            Place(frontB, field.enemyFrontRowContainer, 2);
            var input = new List<BattleEntity> { back, frontA, frontB };
            field.SetValidTargets(input);
            CollectionAssert.AreEqual(new[] { back, frontA, frontB }, input);
            CollectionAssert.AreEqual(new[] { frontA, frontB, back }, field.validTargets);
            field.SetValidTargets(field.validTargets);
            Assert.That(field.validTargets.Count, Is.EqualTo(3));
        }

        [Test]
        public void NearestTargetSkipsDeadAndInactiveAndPreservesTies()
        {
            BattleFieldController field = Field();
            var attacker = MakeObject("Player").AddComponent<PlayerController>();
            BattleTestEntity left = Entity("Left");
            BattleTestEntity right = Entity("Right");
            BattleTestEntity inactive = Entity("Inactive");
            inactive.gameObject.SetActive(false);
            left.transform.position = Vector3.left;
            right.transform.position = Vector3.right;
            field.activeMonsters.AddRange(new[] { Entity("Dead", 0), inactive, left, right });
            Assert.That(field.FindNearestLivingTarget(attacker.gameObject), Is.SameAs(left.gameObject));
            Assert.That(field.FindNearestLivingTarget(null), Is.Null);
        }

        [UnityTest]
        public IEnumerator SlotReinitializationEmptiesChildrenInTheSameFrame()
        {
            BattleFieldController field = Field();
            field.InitializeSlots();
            Transform originalSlot = field.enemyFrontRowContainer.GetChild(0);
            GameObject oldMonster = MakeObject("OldMonster");
            oldMonster.transform.SetParent(originalSlot, false);
            field.InitializeSlots();
            Assert.That(originalSlot.childCount, Is.Zero);
            Assert.That(oldMonster.activeSelf, Is.False);
            Assert.That(oldMonster.transform.parent, Is.Null);
            Assert.That(field.enemyFrontRowContainer.childCount, Is.EqualTo(3));
            yield return null;
            Assert.That(oldMonster == null, Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedHitShakeRestoresOriginalPositionAndStopsOnDisable()
        {
            BattleTestEntity entity = Entity("Shake");
            Vector3 origin = new Vector3(10, 20, 0);
            entity.transform.localPosition = origin;
            for (int i = 0; i < 5; i++)
            {
                entity.TriggerHitShake(false);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Vector3.Distance(entity.transform.localPosition, origin), Is.LessThan(0.001f));
            entity.TriggerHitShake(true);
            entity.enabled = false;
            Assert.That(Vector3.Distance(entity.transform.localPosition, origin), Is.LessThan(0.001f));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(entity.transform.localPosition, origin), Is.LessThan(0.001f));
        }
    }
}
