using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Feature.Negotiation;
using UnityEditor;

namespace RPGProject.Tests.Negotiation
{
    public sealed class NegotiationCatalogTests
    {
        private static List<Dictionary<string, string>> Csv() =>
            DialogueCsv.Read(File.ReadAllText("Assets/CSV/Dialogues/Negotiation.csv"));

        [Test]
        public void EveryDatabaseMonsterHasRaceAndGenderText()
        {
            var catalog = NegotiationDialogueCatalog.Create(Csv());
            var db = AssetDatabase.LoadAssetAtPath<MonsterDatabase>("Assets/Database/MonsterDatabase.asset");
            Assert.That(db, Is.Not.Null);
            foreach (var monster in db.entries)
            {
                Assert.That(Enum.IsDefined(typeof(Race), monster.race), Is.True, monster.id);
                Assert.That(Enum.IsDefined(typeof(Gender), monster.gender), Is.True, monster.id);
                Assert.That(Enum.IsDefined(typeof(Personality), monster.personality), Is.True, monster.id);
                var script = catalog.Resolve(monster.personality, monster.race, monster.gender, out var key);
                Assert.That(key, Is.Not.EqualTo("DEFAULT"), monster.id);
                Assert.That(script.Single(r => r["Seq"] == "RACE_LINE")["Text"],
                    Is.Not.EqualTo("서로 다른 곳에서 왔더라도 말은 통할 수 있겠지."), monster.id);
                Assert.That(script.Single(r => r["Seq"] == "GENDER_LINE")["Text"], Does.Contain("{MonsterName}"));
            }
        }

        [Test]
        public void ResolvedRowsAreIsolatedAndSelectorsChangeOnlyText()
        {
            var catalog = NegotiationDialogueCatalog.Create(Csv());
            var a = catalog.Resolve(Personality.Polite, Race.Human, Gender.Male, out _);
            var b = catalog.Resolve(Personality.Polite, Race.Machine, Gender.None, out _);
            Assert.That(a.Single(r => r["Seq"] == "RACE_LINE")["Text"],
                Is.Not.EqualTo(b.Single(r => r["Seq"] == "RACE_LINE")["Text"]));
            Assert.That(a.Single(r => r["Seq"] == "GENDER_LINE")["Text"],
                Is.Not.EqualTo(b.Single(r => r["Seq"] == "GENDER_LINE")["Text"]));
            for (int i = 0; i < a.Count; i++)
                foreach (string field in new[] { "Seq", "Type", "Action", "Condition", "NextID" })
                    Assert.That(a[i][field], Is.EqualTo(b[i][field]));
            a[0]["Text"] = "mutated";
            Assert.That(catalog.Resolve(Personality.Polite, Race.Human, Gender.Male, out _)[0]["Text"], Is.Not.EqualTo("mutated"));
        }

        [Test]
        public void OldEightColumnCsvAndGenderSpecificEventRemainSupported()
        {
            var rows = Csv().Where(r => r["Race"] == "" && r["Gender"] == "" && r["EventID"] == "DEFAULT")
                .Select(r => r.Where(p => p.Key != "Race" && p.Key != "Gender").ToDictionary(p => p.Key, p => p.Value)).ToList();
            var old = rows.Select(r => new Dictionary<string, string>(r) { ["EventID"] = "SLY_FEMALE" }).ToList();
            rows.AddRange(old);
            var catalog = NegotiationDialogueCatalog.Create(rows);
            catalog.Resolve(Personality.Sly, Race.Demon, Gender.Female, out var key);
            Assert.That(key, Is.EqualTo("SLY_FEMALE"));
            catalog.Resolve(Personality.Sly, Race.Demon, Gender.Male, out key);
            Assert.That(key, Is.EqualTo("DEFAULT"));
        }

        [Test]
        public void CombinedSelectorOverridesRaceWhichOverridesGender()
        {
            var rows = Csv();
            Dictionary<string, string> Variant(string race, string gender, string text) =>
                new Dictionary<string, string> { ["EventID"] = "POLITE", ["Seq"] = "INTRO", ["Race"] = race,
                    ["Gender"] = gender, ["Text"] = text };
            rows.Add(Variant("", "Female", "gender"));
            rows.Add(Variant("Human", "", "race"));
            rows.Add(Variant("Human", "Female", "both"));
            var catalog = NegotiationDialogueCatalog.Create(rows);
            Assert.That(catalog.Resolve(Personality.Polite, Race.Human, Gender.Female, out _)[0]["Text"], Is.EqualTo("both"));
            Assert.That(catalog.Resolve(Personality.Polite, Race.Human, Gender.Male, out _)[0]["Text"], Is.EqualTo("race"));
            Assert.That(catalog.Resolve(Personality.Polite, Race.Beast, Gender.Female, out _)[0]["Text"], Is.EqualTo("gender"));
        }

        [TestCase("Race", "Machines")]
        [TestCase("Gender", "2")]
        [TestCase("Seq", "MISSING_NODE")]
        [TestCase("NextID", "END")]
        public void InvalidOverridesAreRejected(string field, string value)
        {
            var rows = Csv();
            var variant = rows.First(r => r["Race"] == "Human");
            variant[field] = value;
            Assert.Throws<FormatException>(() => NegotiationDialogueCatalog.Create(rows));
        }

        [Test]
        public void DuplicateOverridesAreRejected()
        {
            var rows = Csv(); rows.Add(new Dictionary<string, string>(rows.First(r => r["Race"] == "Human")));
            Assert.Throws<FormatException>(() => NegotiationDialogueCatalog.Create(rows));
        }

        [TestCase(Personality.Proud)]
        [TestCase(Personality.Principled)]
        [TestCase(Personality.Rational)]
        public void NewPersonalitiesPreferReasonToInsults(Personality personality)
        {
            var reason = NegotiationCalculator.CalculateMoodChange(ChoiceTone.Persuade, personality, Race.Human, default);
            var insult = NegotiationCalculator.CalculateMoodChange(ChoiceTone.Insult, personality, Race.Human, default);
            Assert.That(reason.addedInterest, Is.GreaterThan(insult.addedInterest));
            Assert.That(reason.addedAnger, Is.LessThan(insult.addedAnger));
        }

        [Test]
        public void All312ProfilesHaveAllFivePaidOutcomeRoutes()
        {
            var catalog = NegotiationDialogueCatalog.Create(Csv());
            foreach (Personality p in Enum.GetValues(typeof(Personality)))
                foreach (Race race in Enum.GetValues(typeof(Race)))
                    foreach (Gender gender in Enum.GetValues(typeof(Gender)))
                        foreach (NegotiationRewardKind goal in Enum.GetValues(typeof(NegotiationRewardKind)))
                        {
                            var rows = catalog.Resolve(p, race, gender, out _);
                            var byId = rows.ToDictionary(r => r["Seq"]);
                            var choices = new Dictionary<string, string> {
                                ["OPEN"] = p == Personality.Aggressive ? "OPEN_THREAT" : "OPEN_REASON",
                                ["ROUND2"] = p == Personality.Aggressive || p == Personality.Sly ? "SECOND_FLIRT" : "SECOND_RELIEVE",
                                ["NEGO_START"] = "THIRD_REASON", ["GOAL"] = goal == NegotiationRewardKind.Recruit ? "ASK_JOIN" : "ASK_ITEM",
                                ["REWARD_CHOICE"] = goal == NegotiationRewardKind.Item ? "REQUEST_ITEM" : goal == NegotiationRewardKind.Gold ? "REQUEST_GOLD" : "REQUEST_RECOVERY",
                                ["RECOVERY_CHOICE"] = goal == NegotiationRewardKind.HP ? "REQUEST_HP" : "REQUEST_MP" };
                            int payments = 0, rewards = 0;
                            var session = new NegotiationSession(p, race, default, 0, 0, 0,
                                _ => { payments++; return true; }, () => { rewards++; return true; }, () => false,
                                _ => true, _ => { rewards++; return true; }, () => true, () => 0.99f);
                            string id = "INTRO"; int visits = 0;
                            var reached = new HashSet<string>();
                            while (id != "END")
                            {
                                Assert.That(++visits, Is.LessThan(128));
                                reached.Add(id);
                                var row = byId[id];
                                if (row["Type"] == "CHOICE")
                                {
                                    string branchID = id.StartsWith("COST_") ? "ACCEPT_" + id.Substring(5) : choices[id];
                                    var branch = byId[branchID];
                                    if (branch["Action"].Length > 0)
                                        session.ApplyTone((ChoiceTone)Enum.Parse(typeof(ChoiceTone), branch["Action"].Split(':')[1]));
                                    id = session.MustStop ? "FAIL" : session.Resolve(id, branch["NextID"]);
                                }
                                else id = session.Resolve(id, row["NextID"]);
                            }
                            Assert.That(reached, Does.Contain("SUCCESS_" + (goal == NegotiationRewardKind.Recruit ? "RECRUIT" : goal.ToString().ToUpperInvariant())), $"{p}/{race}/{gender}/{goal}");
                            Assert.That(payments, Is.EqualTo(1)); Assert.That(rewards, Is.EqualTo(1));
                        }
        }
    }
}
