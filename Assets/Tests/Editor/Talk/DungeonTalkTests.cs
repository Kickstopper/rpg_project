using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;
using Manager;
using NUnit.Framework;
using UI.Common;
using UI.DungeonMapScene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGProject.Tests.Talk
{
    public sealed class DungeonTalkRulesTests
    {
        private readonly HashSet<string> seen = new HashSet<string>();
        private CellData cell;
        [SetUp] public void SetUp()
        {
            seen.Clear();
            cell = new CellData { events = new List<CellEventData> { new CellEventData { eventID = "talk", isEventRepeatable = true } } };
        }
        private CellEventData Find(bool replay, bool attempt = false, int x = 2, string map = "A", Func<string, bool> flag = null, Func<string, bool> exists = null) =>
            DungeonCellEventRules.Find(cell, map, x, 3, seen, flag, replay, attempt, exists);
        private void Complete() => seen.Add(DungeonCellEventRules.Key("A", 2, 3, "talk"));

        [Test] public void FirstVisitIsAutomaticAndQueriesDoNotMarkItSeen()
        {
            Assert.That(Find(false), Is.SameAs(cell.events[0]));
            Assert.That(Find(true), Is.Null);
            Assert.That(seen, Is.Empty);
            Assert.That(Find(false), Is.Not.Null);
        }
        [Test] public void CompletedRepeatableEventBecomesManualAndStaysRepeatable()
        {
            Complete();
            Assert.That(Find(false), Is.Null);
            Assert.That(Find(true), Is.SameAs(cell.events[0]));
            Assert.That(Find(true), Is.SameAs(cell.events[0]));
        }
        [Test] public void CompletionIsScopedToTheExactMapAndCell()
        {
            Complete();
            Assert.That(Find(true, x: 1), Is.Null);
            Assert.That(Find(true, map: "B"), Is.Null);
            Assert.That(Find(false, map: "B"), Is.Not.Null);
        }
        [Test] public void SeenOneShotDoesNotOfferTalk()
        {
            cell.events[0].isEventRepeatable = false; Complete();
            Assert.That(Find(false), Is.Null);
            Assert.That(Find(true), Is.Null);
        }
        [TestCase(true)] [TestCase(false)]
        public void RequiredFlagIsRecheckedForReplay(bool requiredState)
        {
            Complete(); cell.events[0].requiredFlag = "quest"; cell.events[0].requiredFlagState = requiredState;
            Assert.That(Find(true, flag: key => requiredState), Is.Not.Null);
            Assert.That(Find(true, flag: key => !requiredState), Is.Null);
            Assert.That(Find(true, flag: null), Is.Null);
        }
        [Test] public void AttemptTimingIsPreservedForFirstPlayAndReplayRequiresOwnCell()
        {
            cell.events[0].triggerOnAttempt = true;
            Assert.That(Find(false), Is.Null);
            Assert.That(Find(false, true), Is.Not.Null);
            Complete();
            Assert.That(Find(false, true), Is.Null);
            Assert.That(Find(true), Is.Not.Null);
            Assert.That(Find(true, x: 1), Is.Null);
        }
        [Test] public void FirstEligibleReplayWinsWhileUnseenEventsRemainAutomatic()
        {
            Complete();
            var next = new CellEventData { eventID = "new", isEventRepeatable = true };
            cell.events.Insert(0, next);
            Assert.That(Find(false), Is.SameAs(next));
            Assert.That(Find(true).eventID, Is.EqualTo("talk"));
            seen.Add(DungeonCellEventRules.Key("A", 2, 3, "new"));
            Assert.That(Find(true), Is.SameAs(next));
        }
        [Test] public void MissingDialogueAndNullRowsAreNotOffered()
        {
            Complete(); cell.events.Insert(0, null); cell.events.Add(new CellEventData());
            Assert.That(Find(true, exists: id => false), Is.Null);
            Assert.That(Find(true, exists: id => id == "talk"), Is.Not.Null);
            cell.events = null;
            Assert.That(Find(true), Is.Null);
        }
        [Test] public void ForceDirectionIsPreservedInSelectedEvent()
        {
            Complete(); cell.events[0].useForceDir = true; cell.events[0].evForceDir = Direction.West;
            var ev = Find(true);
            Assert.That(ev.useForceDir, Is.True);
            Assert.That(ev.evForceDir, Is.EqualTo(Direction.West));
        }
        [Test] public void SaveRoundTripAndNewGameResetIncludeRepeatableHistory()
        {
            var go = new GameObject("Event history test");
            try
            {
                var manager = go.AddComponent<DungeonEventManager>();
                manager.MarkCompleted("A", 2, 3, "talk");
                manager.MarkCompleted("A", 2, 3, "talk");
                List<string> saved = manager.GetCompletedTriggers();
                Assert.That(saved, Has.Count.EqualTo(1));
                manager.ResetAllEvents(); Assert.That(manager.GetCompletedTriggers(), Is.Empty);
                manager.ApplyCompletedTriggers(saved);
                var loaded = new HashSet<string>(manager.GetCompletedTriggers());
                Assert.That(DungeonCellEventRules.Find(cell, "A", 2, 3, loaded, null, true, false), Is.Not.Null);
                manager.ApplyCompletedTriggers(null); Assert.That(manager.GetCompletedTriggers(), Is.Empty);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void LegacyOneShotKeysStillMatch()
        {
            cell.events[0].isEventRepeatable = false;
            seen.Add("A_2_3_talk");
            Assert.That(Find(false), Is.Null);
        }
        [Test] public void ConsumedConfirmIsUnavailableToDialogueInTheSameFrame()
        {
            var field = typeof(GameInput).GetField("confirmConsumedFrame", BindingFlags.NonPublic | BindingFlags.Static);
            int previous = (int)field.GetValue(null);
            try
            {
                GameInput.ConsumeConfirmThisFrame();
                Assert.That(GameInput.IsConfirmConsumed, Is.True);
                Assert.That(GameInput.GetConfirmDown(), Is.False);
                Assert.That(GameInput.GetSelectDown(), Is.False);
            }
            finally { field.SetValue(null, previous); }
        }
    }

    public sealed class DungeonTalkRasterTests
    {
        private readonly List<Object> created = new List<Object>();
        private T Track<T>(T item) where T : Object { created.Add(item); return item; }
        [TearDown] public void TearDown()
        {
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }
        [Test] public void FallbackIsCenteredAndClipsInSmallViewports()
        {
            var icon = new DungeonTalkIcon(); icon.Configure(null, 33);
            var buffer = new Color32[65 * 49]; icon.DrawCentered(buffer, 65, 49);
            Assert.That(buffer.Count(c => c.a > 0), Is.GreaterThan(0));
            for (int y = 0; y < 49; y++)
            for (int x = 0; x < 65; x++)
                if (x < 16 || x >= 49 || y < 16 || y >= 33) Assert.That(buffer[y * 65 + x].a, Is.Zero);
            Assert.DoesNotThrow(() => icon.DrawCentered(new Color32[4], 2, 2));
        }
        [Test] public void SpriteSheetRegionAndOrientationArePreserved()
        {
            var texture = Track(new Texture2D(4, 2, TextureFormat.RGBA32, false));
            texture.SetPixels(new[] { Color.green, Color.green, Color.red, Color.red,
                Color.green, Color.green, Color.blue, Color.blue }); texture.Apply();
            var sprite = Track(Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect));
            var icon = new DungeonTalkIcon(); icon.Configure(sprite, 12);
            var buffer = new Color32[12 * 12]; icon.DrawCentered(buffer, 12, 12);
            Assert.That(buffer[3 * 12 + 6], Is.EqualTo((Color32)Color.red));
            Assert.That(buffer[9 * 12 + 6], Is.EqualTo((Color32)Color.blue));
        }
        [Test] public void StraightAlphaCompositePreservesTransparentAndOpaquePixels()
        {
            var blue = new Color32(0, 0, 255, 255);
            Assert.That(DungeonTalkIcon.Over(new Color32(255, 0, 0, 0), blue), Is.EqualTo(blue));
            Assert.That(DungeonTalkIcon.Over(new Color32(255, 0, 0, 255), blue), Is.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.That(DungeonTalkIcon.Over(new Color32(255, 0, 0, 128), blue), Is.EqualTo(new Color32(128, 0, 127, 255)));
        }
        [TestCase(false)] [TestCase(true)]
        public void RendererAddsTalkAfterDungeonAndClearsItOnNextFrame(bool stereo)
        {
            var texture = Track(new Texture2D(64, 64, TextureFormat.RGBA32, false));
            texture.SetPixels(Enumerable.Repeat(Color.black, 64 * 64).ToArray()); texture.Apply();
            var theme = Track(ScriptableObject.CreateInstance<DungeonTheme>());
            theme.texture = new[] { texture }; theme.floorTexIdx = theme.ceilingTexIdx = 0;
            var map = new MapData { mapID = "test", width = 3, height = 3, cells = new CellData[9] };
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                map.cells[y * 3 + x] = new CellData { x = x, y = y, wallTextureIDs = new[] { 0, 0, 0, 0 } };
            var player = new DungeonPlayer(null, 0.66f, 0, new List<int>());
            player.SetMapData(map, 1, 1, Direction.North);
            var renderer = new RaycastRenderEngine(); renderer.Initialize(64, 32); Track(renderer.ScreenTexture);
            renderer.LoadAssets(theme, Array.Empty<Sprite>(), 64, 64, Array.Empty<SpriteInfo>());
            renderer.SetMapData(map, theme, new TileAnimState[3, 3]);
            var settings = new UI.DungeonMapScene.RenderSettings();
            renderer.RenderFrame(player, settings, stereo, 0);
            var before = renderer.ScreenTexture.GetPixels32();
            renderer.SetTalkPrompt(true, null, 24); renderer.RenderFrame(player, settings, stereo, 0);
            Assert.That(renderer.ScreenTexture.GetPixels32().SequenceEqual(before), Is.False);
            renderer.SetTalkPrompt(false); renderer.RenderFrame(player, settings, stereo, 0);
            CollectionAssert.AreEqual(before, renderer.ScreenTexture.GetPixels32());
        }
    }
}
