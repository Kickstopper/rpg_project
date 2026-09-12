using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Negotiation;
using RPGProject.Feature.Skills;
using RPGProject.Feature.StatusEffects;
using RPGProject.Infrastructure.Persistence;
using RPGProject.Shared.Gameplay;
using UnityEngine;

namespace RPGProject.Tests.Negotiation
{
    public sealed class NegotiationKinshipTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private GameObject Inactive(string name)
        {
            var go = new GameObject(name); go.SetActive(false); objects.Add(go); return go;
        }

        private RuntimeCharacterData Member(string id, bool regular = true, bool monster = true)
        {
            return new RuntimeCharacterData(new CharacterSaveData
            {
                characterId = id, currentHp = 50, maxHp = 100, currentMp = 5, maxMp = 20,
                learnedSkillIds = new List<string>()
            }) { isRegular = regular, isMonster = monster, name = id };
        }

        private PlayerController Player(string name, int hp, int maxHp, int mp = 5, int maxMp = 20, bool regular = true)
        {
            var pc = Inactive(name).AddComponent<PlayerController>();
            pc.sourceData = Member(name, regular); pc.maxHp = maxHp; pc.maxMp = maxMp;
            pc.currentHp = hp; pc.currentMp = mp;
            pc.sourceData.currentHp = hp; pc.sourceData.maxHp = maxHp;
            pc.sourceData.currentMp = mp; pc.sourceData.maxMp = maxMp;
            return pc;
        }

        private SkillData Skill(EffectType type, int amount)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>(); objects.Add(skill);
            skill.effectType = type; skill.effectValue = amount; return skill;
        }

        [Test]
        public void CompanionUsesCurrentPartyExactIDIncludingReservesNotRaceOrHistory()
        {
            var monster = new MonsterDatabase.MonsterEntry { id = "enemy_042", race = Race.Fairy };
            var reserve = Member(monster.id, false); reserve.race = Race.Fairy;
            var other = Member("enemy_043"); other.race = Race.Fairy;
            var party = new List<RuntimeCharacterData> { null, other, reserve };
            Assert.That(NegotiationKinshipRules.HasCompanion(monster, party), Is.True);
            party.Remove(reserve); // Retaining reserve elsewhere in a roster does not count.
            Assert.That(NegotiationKinshipRules.HasCompanion(monster, party), Is.False);
            other.characterId = monster.id; other.isMonster = false;
            Assert.That(NegotiationKinshipRules.HasCompanion(monster, party), Is.False);
            Assert.That(NegotiationKinshipRules.HasCompanion(monster, null), Is.False);
            monster.id = "";
            Assert.That(NegotiationKinshipRules.HasCompanion(monster, party), Is.False);
        }

        [TestCase("KIN_RESULT")]
        [TestCase("END")]
        public void KinshipCommitsOnlyAfterOfferOnceEvenWhenAngryAndReentered(string result)
        {
            int commits = 0;
            NegotiationSession session = null;
            session = new NegotiationSession(Personality.Aggressive, Race.Beast, default, 100, 0, 0,
                _ => throw new Exception("Kinship must not request payment"), () => false, () => false,
                kinship: () =>
                {
                    commits++;
                    Assert.That(session.Resolve("nested", "CHECK_MOOD:KINSHIP"), Is.EqualTo("FAIL"));
                    return result;
                });
            Assert.That(session.IsKinship, Is.True);
            Assert.That(session.ShouldEndBattle, Is.False);
            Assert.That(session.Resolve("INTRO", "KIN_OFFER"), Is.EqualTo("KIN_OFFER"));
            Assert.That(commits, Is.Zero);
            Assert.That(session.Resolve("KIN_OFFER", "CHECK_MOOD:KINSHIP"), Is.EqualTo(result));
            Assert.That(session.ShouldEndBattle, Is.True);
            session.Resolve("KIN_OFFER", "CHECK_MOOD:KINSHIP");
            session.Resolve("another", "CHECK_MOOD:KINSHIP");
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(session.HasPaidTrade, Is.False);
            session.Close();
            Assert.That(session.ShouldEndBattle, Is.True); // DialogueUI closes before BattleManager's callback.
            Assert.That(session.Resolve("late", "CHECK_MOOD:KINSHIP"), Is.EqualTo("END"));
        }

        [TestCase(NegotiationRewardKind.Recruit)]
        [TestCase(NegotiationRewardKind.Gold)]
        [TestCase(NegotiationRewardKind.Item)]
        [TestCase(NegotiationRewardKind.HP)]
        [TestCase(NegotiationRewardKind.MP)]
        public void EverySuccessfulTradeRequestsImmediateBattleEndAfterDialogue(NegotiationRewardKind kind)
        {
            var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                _ => true, () => true, () => true, _ => true, _ => true, () => false, () => 0f);
            Assert.That(session.ShouldEndBattle, Is.False);
            session.Resolve("plan", "CHECK_MOOD:TRADE:" + kind);
            Assert.That(session.ShouldEndBattle, Is.False);
            session.Resolve("pay", "CHECK_MOOD:PAY:Gold");
            session.Close();
            Assert.That(session.ShouldEndBattle, Is.True);
        }

        [Test]
        public void RejectionPaymentFailureAndBetrayalDoNotBecomePeacefulVictories()
        {
            foreach (bool pays in new[] { false, true })
            {
                var session = new NegotiationSession(Personality.Sly, Race.Human, default, 0, 100, 100,
                    _ => pays, () => false, () => false, _ => true, _ => false, () => true, () => 0f);
                session.Resolve("plan", "CHECK_MOOD:TRADE:Gold");
                Assert.That(session.Resolve("pay", "CHECK_MOOD:PAY:Gold"), Is.EqualTo(pays ? "FLED" : "INSUFFICIENT_ITEM"));
                Assert.That(session.ShouldEndBattle, Is.False);
            }
        }

        [Test]
        public void LegacyRecruitAndItemAlsoRequestBattleEnd()
        {
            foreach (string command in new[] { "RECRUIT", "ITEM" })
            {
                var session = new NegotiationSession(Personality.Polite, Race.Human, default, 0, 100, 100,
                    _ => false, () => true, () => true);
                session.Resolve("legacy", "CHECK_MOOD:" + command);
                Assert.That(session.ShouldEndBattle, Is.True);
            }
        }

        [TestCase(1, 5, 15)]
        [TestCase(20, 100, 300)]
        [TestCase(0, 5, 15)]
        [TestCase(-1, 5, 15)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue)]
        public void GoldIsLevelScaledBoundedAndOverflowSafe(int level, int min, int max)
        {
            Assert.That(NegotiationKinshipRules.GoldAmount(level, 0f), Is.EqualTo(min));
            Assert.That(NegotiationKinshipRules.GoldAmount(level, 1f), Is.EqualTo(max));
            Assert.That(NegotiationKinshipRules.GoldAmount(level, float.NaN), Is.EqualTo(max));
            for (int i = 0; i <= 100; i++)
                Assert.That(NegotiationKinshipRules.GoldAmount(level, i / 100f), Is.InRange(min, max));
        }

        [Test]
        public void RecoverySelectsRatioNotAbsoluteValueAndExcludesDeadReserveFullAndZeroCapacity()
        {
            var small = Player("small", 10, 20);
            var lowRatio = Player("low ratio", 20, 100);
            var dead = Player("dead", 0, 100);
            var reserve = Player("reserve", 1, 100, regular: false);
            var full = Player("full", 100, 100);
            var zero = Player("zero", 1, 0, 0, 0);
            var players = new[] { small, dead, reserve, full, zero, lowRatio };
            var hp = Skill(EffectType.Recover_HP, 30);
            Assert.That(NegotiationKinshipRules.FindRecoveryTarget(players, hp), Is.SameAs(lowRatio));
            var mp = Skill(EffectType.Recover_MP, 5);
            small.currentMp = 1; lowRatio.currentMp = 10;
            Assert.That(NegotiationKinshipRules.FindRecoveryTarget(players, mp), Is.SameAs(small));
            Assert.That(NegotiationKinshipRules.FindRecoveryTarget(players, Skill(EffectType.Revive_Fully, 100)), Is.Null);
            Assert.That(NegotiationKinshipRules.FindRecoveryTarget(players, Skill(EffectType.Recover_HP, 0)), Is.Null);
        }

        [Test]
        public void CurseCanExcludeIneffectiveHealingAndHiddenCardsStillRecoverWithDataSync()
        {
            var blocked = Player("blocked", 1, 100);
            var available = Player("available", 95, 100);
            var status = ScriptableObject.CreateInstance<StatusEffectData>(); objects.Add(status);
            status.id = StatusEffectID.Curse; status.healingReceivedMultiplier = 0f;
            blocked.StatusEffects.Apply(status);
            var skill = Skill(EffectType.Recover_HP, 30);
            var selected = NegotiationKinshipRules.FindRecoveryTarget(new[] { blocked, available }, skill);
            Assert.That(selected, Is.SameAs(available));
            Assert.That(selected.TryRestoreNegotiationResource(true, skill.effectValue), Is.True);
            Assert.That(selected.sourceData.currentHp, Is.EqualTo(100));
            Assert.That(blocked.currentHp, Is.EqualTo(1));
        }

        [Test]
        public void BothRecoveryKindsChooseLowestRatioAndStrongestOwnedSpellWithStableTie()
        {
            var manager = Inactive("manager").AddComponent<BattleManager>();
            var field = manager.gameObject.AddComponent<BattleFieldController>(); manager.fieldController = field;
            var monster = Inactive("monster").AddComponent<MonsterController>();
            var weak = Skill(EffectType.Recover_HP, 5);
            var strong = Skill(EffectType.Recover_HP, 50);
            var mp = Skill(EffectType.Recover_MP, 10);
            monster.sourceData = new MonsterDatabase.MonsterEntry { skills = new List<SkillData> { weak, null, mp, strong } };
            var a = Player("a", 20, 100, 15, 20);
            var b = Player("b", 90, 100, 1, 20);
            field.activePlayers.Add(a); field.activePlayers.Add(b);
            var method = typeof(BattleManager).GetMethod("FindKinshipRecovery", BindingFlags.NonPublic | BindingFlags.Instance);
            var args = new object[] { monster, null, null }; method.Invoke(manager, args);
            Assert.That(args[1], Is.SameAs(b)); Assert.That(args[2], Is.SameAs(mp));
            b.currentMp = 4; method.Invoke(manager, args); // HP 20/100 == MP 4/20: HP first.
            Assert.That(args[1], Is.SameAs(a)); Assert.That(args[2], Is.SameAs(strong));
            monster.sourceData.skills.Clear(); method.Invoke(manager, args);
            Assert.That(args[1], Is.Null); Assert.That(args[2], Is.Null);
        }

        [Test]
        public void EveryPersonalityHasAValidIndependentKinshipScript()
        {
            foreach (Personality personality in Enum.GetValues(typeof(Personality)))
            {
                var monster = new MonsterDatabase.MonsterEntry { id = "enemy_001", name = "테스트", personality = personality };
                var rows = NegotiationKinshipRules.CreateDialogues(monster);
                Assert.That(NegotiationScriptValidator.Validate(rows), Is.Empty);
                Assert.That(rows.Single(r => r["Seq"] == "KIN_OFFER")["NextID"], Is.EqualTo("CHECK_MOOD:KINSHIP"));
                rows[0]["Text"] = "changed";
                Assert.That(NegotiationKinshipRules.CreateDialogues(monster)[0]["Text"], Is.Not.EqualTo("changed"));
            }
        }
    }
}
