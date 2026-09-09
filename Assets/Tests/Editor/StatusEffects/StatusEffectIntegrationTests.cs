using System.Collections.Generic;
using System.Reflection;
using Data;
using Manager;
using NUnit.Framework;
using UI.Battle;
using UnityEngine;

namespace RPGProject.Tests.StatusEffects
{
    public sealed class StatusEffectIntegrationTests
    {
        GameObject host;
        object previousRoot;
        FieldInfo rootField;
        StatusEffectDatabase database;
        StatusEffectData poison, paralyze, silence;
        EffectManager effects;
        readonly List<Object> temporary = new List<Object>();
        [SetUp] public void Setup()
        {
            host = new GameObject("Status integration managers"); host.SetActive(false);
            var root = host.AddComponent<ManagerRoot>();
            rootField = typeof(ManagerRoot).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            previousRoot = rootField.GetValue(null); rootField.SetValue(null, root);
            var manager = host.AddComponent<DatabaseManager>();
            typeof(ManagerRoot).GetField("databaseManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, manager);
            database = ScriptableObject.CreateInstance<StatusEffectDatabase>(); temporary.Add(database);
            manager.statusEffectDB = database;
            poison = Definition(StatusEffectID.Poison); paralyze = Definition(StatusEffectID.Paralyze);
            silence = Definition(StatusEffectID.Silence); silence.durationType = EffectDurationType.BattleOnly;
            silence.restrictionType = RestrictionType.Silence; silence.restrictionChance = 1;
            database.db.AddRange(new[] { poison, paralyze, silence }); database.Initialize();
            effects = host.AddComponent<EffectManager>();
        }
        StatusEffectData Definition(StatusEffectID id)
        {
            var data = ScriptableObject.CreateInstance<StatusEffectData>(); temporary.Add(data);
            data.id = id; data.durationType = EffectDurationType.Persistent; data.cureType = EffectCureType.ExplicitOnly;
            return data;
        }
        RuntimeCharacterData Character(CharacterSaveData save = null) => new RuntimeCharacterData(save ?? new CharacterSaveData {
            learnedSkillIds = new List<string>(), currentHp = 50, maxHp = 100
        });
        PlayerController View(RuntimeCharacterData character)
        {
            var go = new GameObject("Status view"); go.SetActive(false); temporary.Add(go);
            var view = go.AddComponent<PlayerController>(); view.sourceData = character;
            view.maxHp = 100; view.currentHp = 50;
            typeof(BattleEntity).GetMethod("BindStatusEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { character.StatusEffects });
            return view;
        }
        [TearDown] public void Cleanup()
        {
            rootField.SetValue(null, previousRoot);
            for (int i = temporary.Count - 1; i >= 0; i--) Object.DestroyImmediate(temporary[i]);
            temporary.Clear(); Object.DestroyImmediate(host);
        }
        [Test] public void LegacySingleStatusMigratesAndNewEmptySaveDoesNotResurrectIt()
        {
            var save = new CharacterSaveData { persistentStatusId = "Poison", learnedSkillIds = new List<string>() };
            var character = Character(save); Assert.IsTrue(character.StatusEffects.Has(StatusEffectID.Poison));
            character.StatusEffects.Apply(paralyze);
            var exported = character.ToSaveData(); Assert.AreEqual(2, exported.statusEffects.Count);
            Assert.AreEqual(2, Character(exported).StatusEffects.Effects.Count);
            exported.statusEffects.Clear(); exported.persistentStatusId = "Poison";
            Assert.AreEqual(0, Character(exported).StatusEffects.Effects.Count);
        }
        [Test] public void AntidoteUpdatesBothViewsAndSaveButDoesNotCureParalysis()
        {
            var character = Character(); character.StatusEffects.Apply(poison); character.StatusEffects.Apply(paralyze);
            var field = View(character); var battle = View(character);
            var antidote = ScriptableObject.CreateInstance<ConsumableItemData>(); temporary.Add(antidote);
            antidote.effectType = EffectType.Recover_Poison;
            Assert.IsTrue(effects.ApplyEffect(field, antidote));
            Assert.IsFalse(battle.StatusEffects.Has(StatusEffectID.Poison));
            Assert.IsTrue(battle.StatusEffects.Has(StatusEffectID.Paralyze));
            Assert.AreEqual("Paralyze", character.ToSaveData().statusEffects[0].id);
            Assert.IsFalse(effects.ApplyEffect(field, antidote));
        }
        [Test] public void SkillAvailabilityTracksSharedCureImmediately()
        {
            var character = Character(); character.StatusEffects.Apply(silence);
            var field = View(character); var battle = View(character);
            Assert.IsFalse(field.CanUseSkills); Assert.IsFalse(battle.CanUseSkills);
            field.StatusEffects.Remove(StatusEffectID.Silence);
            Assert.IsTrue(battle.CanUseSkills);
        }
    }
}
