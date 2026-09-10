using System;
using System.Collections.Generic;
using System.Linq;
using MonsterEditing;
using NUnit.Framework;
using RPGProject.Feature.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MonsterEditorTests
{
    [Test] public void FirstFrameRemainsUntilIntervalAndThenLoops()
    {
        var clock = new MonsterPreviewClock(); clock.Configure(3, 0.5, 10, true); clock.SetPlaying(true, 10);
        Assert.That(clock.Frame, Is.Zero);
        clock.Tick(10.499); Assert.That(clock.Frame, Is.Zero);
        clock.Tick(10.5); Assert.That(clock.Frame, Is.EqualTo(1));
        clock.Tick(11); Assert.That(clock.Frame, Is.EqualTo(2));
        clock.Tick(11.5); Assert.That(clock.Frame, Is.Zero);
    }

    [Test] public void PausingPreservesPartialFrameAndDoesNotCountPausedTime()
    {
        var clock = new MonsterPreviewClock(); clock.Configure(4, 0.5, 0, true); clock.SetPlaying(true, 0);
        clock.SetPlaying(false, 0.25); clock.Tick(100);
        Assert.That(clock.Frame, Is.Zero);
        clock.SetPlaying(true, 100); clock.Tick(100.249);
        Assert.That(clock.Frame, Is.Zero);
        clock.Tick(100.25); Assert.That(clock.Frame, Is.EqualTo(1));
    }

    [Test] public void IntervalEditKeepsCurrentFrameAndRestartsItsDuration()
    {
        var clock = new MonsterPreviewClock(); clock.Configure(4, 0.5, 0, true); clock.SetPlaying(true, 0);
        clock.Tick(0.75); clock.Configure(4, 0.25, 0.75, false);
        Assert.That(clock.Frame, Is.EqualTo(1));
        Assert.That(clock.Playing, Is.True);
        clock.Tick(0.999); Assert.That(clock.Frame, Is.EqualTo(1));
        clock.Tick(1); Assert.That(clock.Frame, Is.EqualTo(2));
    }

    [TestCase(0, 0.5)]
    [TestCase(1, 0.5)]
    [TestCase(4, 0)]
    [TestCase(4, -1)]
    [TestCase(4, double.NaN)]
    [TestCase(4, double.PositiveInfinity)]
    public void StaticOrInvalidConfigurationsNeverAutoPlay(int count, double interval)
    {
        var clock = new MonsterPreviewClock(); clock.Configure(count, interval, 0, true); clock.SetPlaying(true, 0);
        Assert.That(clock.Playing, Is.False); Assert.That(clock.Tick(1000), Is.False);
        Assert.That(clock.Frame, Is.Zero);
    }

    [Test] public void ScrubbingPausesAndClampsWhenArrayShrinks()
    {
        var clock = new MonsterPreviewClock(); clock.Configure(5, 0.5, 0, true); clock.SetPlaying(true, 0);
        clock.Seek(4, 1); Assert.That(clock.Playing, Is.False);
        clock.Tick(2); Assert.That(clock.Frame, Is.EqualTo(4));
        clock.Configure(2, 0.5, 2, false); Assert.That(clock.Frame, Is.EqualTo(1));
        clock.Configure(0, 0.5, 3, false); Assert.That(clock.Frame, Is.Zero);
    }

    [Test] public void LargeTimeJumpDoesNotRequireCatchUpLoop()
    {
        var clock = new MonsterPreviewClock(); clock.Configure(7, 0.125, 0, true); clock.SetPlaying(true, 0);
        double now = 1000000000.375;
        clock.Tick(now);
        Assert.That(clock.Frame, Is.EqualTo((long)Math.Floor(now / 0.125) % 7));
    }

    [TestCase(0.5f)]
    [TestCase(0.1f)]
    [TestCase(0.03f)]
    public void FrequentUpdatesMatchAbsoluteTimeWithoutAccumulatingDrift(float interval)
    {
        var clock = new MonsterPreviewClock(); clock.Configure(5, interval, 100, true); clock.SetPlaying(true, 100);
        for (int i = 1; i <= 6000; i++)
        {
            double now = 100 + i / 60.0;
            clock.Tick(now);
            int expected = (int)Math.Floor(((now - 100) % ((double)interval * 5)) / interval);
            Assert.That(clock.Frame, Is.EqualTo(expected), "Editor tick " + i);
        }
    }

    [Test] public void ClockIgnoresSceneTimeScale()
    {
        float previous = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            var clock = new MonsterPreviewClock(); clock.Configure(3, 0.5, 0, true); clock.SetPlaying(true, 0); clock.Tick(1);
            Assert.That(clock.Frame, Is.EqualTo(2));
        }
        finally { Time.timeScale = previous; }
    }

    [Test] public void FillMissingIdsDoesNotRenameExistingIdsOrReorderEntries()
    {
        var a = new MonsterDatabase.MonsterEntry { id = "boss_guard" };
        var b = new MonsterDatabase.MonsterEntry { id = "enemy_000" };
        var c = new MonsterDatabase.MonsterEntry { id = "" };
        var d = new MonsterDatabase.MonsterEntry { id = " " };
        var entries = new List<MonsterDatabase.MonsterEntry> { a, b, c, d };
        Assert.That(MonsterEditorOperations.FillMissingIds(entries), Is.EqualTo(2));
        Assert.That(a.id, Is.EqualTo("boss_guard")); Assert.That(b.id, Is.EqualTo("enemy_000"));
        CollectionAssert.AreEqual(new[] { a, b, c, d }, entries);
        Assert.That(entries.Select(e => e.id).Distinct().Count(), Is.EqualTo(4));
    }

    [Test] public void NewEntryDoesNotCopyPreviousArraysOrSettings()
    {
        var previous = new MonsterDatabase.MonsterEntry { id = "enemy_000", isBoss = true, animInterval = 7, dropItemIds = new List<string> { "rare" } };
        var created = MonsterEditorOperations.NewEntry(new[] { previous });
        Assert.That(created.id, Is.Not.EqualTo(previous.id));
        Assert.That(created.isBoss, Is.False); Assert.That(created.animInterval, Is.EqualTo(0.5f));
        Assert.That(created.image, Is.Empty); Assert.That(created.dropItemIds, Is.Empty);
    }

    [Test] public void DuplicateCopiesListsButRetainsSpriteAssetReferences()
    {
        Texture2D texture = new Texture2D(8, 8);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        try
        {
            var original = new MonsterDatabase.MonsterEntry { id = "enemy_000", name = "Slime", image = new[] { sprite }, animInterval = 0.125f, dropItemIds = new List<string> { "potion" } };
            var copy = MonsterEditorOperations.Duplicate(original, new[] { original });
            Assert.That(copy.image, Is.Not.SameAs(original.image)); Assert.That(copy.image[0], Is.SameAs(sprite));
            Assert.That(copy.animInterval, Is.EqualTo(original.animInterval));
            copy.dropItemIds.Clear(); Assert.That(original.dropItemIds.Count, Is.EqualTo(1));
            copy.image[0] = null; Assert.That(original.image[0], Is.SameAs(sprite));
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }

    [Test] public void PreviewClockDoesNotModifyDatabaseSerializedDataOrDirtyState()
    {
        var database = ScriptableObject.CreateInstance<MonsterDatabase>();
        try
        {
            database.entries.Add(new MonsterDatabase.MonsterEntry { id = "test", animInterval = 0.25f, image = new Sprite[3] });
            string before = JsonUtility.ToJson(database); bool dirty = EditorUtility.IsDirty(database);
            var clock = new MonsterPreviewClock(); clock.Configure(database.entries[0].image.Length, database.entries[0].animInterval, 0, true); clock.SetPlaying(true, 0);
            for (int i = 0; i < 100; i++) clock.Tick(i * 0.1);
            Assert.That(JsonUtility.ToJson(database), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(database), Is.EqualTo(dirty));
        }
        finally { Object.DestroyImmediate(database); }
    }
}
