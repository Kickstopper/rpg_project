using System;
using System.IO;
using NUnit.Framework;
using RPGProject.Editor.VFX;
using global::UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RPGProject.Tests.VFX
{
    public sealed class BattleVFXTimelineTests
    {
        private static BattleVFXTimeline Start(params float[] times)
        {
            var clock = new BattleVFXTimeline();
            clock.Configure(times, 0, true);
            clock.SetPlaying(true, 0);
            return clock;
        }

        [Test] public void EachFrameUsesItsOwnDuration()
        {
            var clock = Start(0.25f, 0.5f, 1f);
            clock.Tick(0.249); Assert.That(clock.Frame, Is.EqualTo(0));
            clock.Tick(0.25); Assert.That(clock.Frame, Is.EqualTo(1));
            clock.Tick(0.749); Assert.That(clock.Frame, Is.EqualTo(1));
            clock.Tick(0.75); Assert.That(clock.Frame, Is.EqualTo(2));
            clock.Tick(1.75); Assert.That(clock.Frame, Is.EqualTo(0));
        }

        [Test] public void PauseAndResumePreservePartialFrame()
        {
            var clock = Start(0.5f, 0.5f);
            clock.SetPlaying(false, 0.25);
            clock.Tick(100);
            Assert.That(clock.Position, Is.EqualTo(0.25));
            clock.SetPlaying(true, 100);
            clock.Tick(100.25);
            Assert.That(clock.Frame, Is.EqualTo(1));
        }

        [Test] public void SingleShotFinishesAfterLastFrameDurationAndCanReplay()
        {
            var clock = Start(0.25f, 0.5f);
            clock.Loop = false;
            clock.Tick(0.749); Assert.That(clock.Finished, Is.False);
            clock.Tick(0.75); Assert.That(clock.Finished, Is.True);
            Assert.That(clock.Playing, Is.False);
            clock.SetPlaying(true, 20);
            Assert.That(clock.Frame, Is.Zero);
            Assert.That(clock.Finished, Is.False);
        }

        [Test] public void OneFrameCanPlayAndFinish()
        {
            var clock = Start(0.25f); clock.Loop = false;
            clock.Tick(0.249); Assert.That(clock.Finished, Is.False);
            clock.Tick(0.25); Assert.That(clock.Finished, Is.True);
        }

        [Test] public void HugeStallUsesModuloAndSpeedWithoutCatchUpLoops()
        {
            var clock = Start(0.25f, 0.5f, 0.25f);
            clock.Speed = 2;
            clock.Tick(1000000000.25);
            Assert.That(clock.Position, Is.EqualTo(0.5));
            Assert.That(clock.Frame, Is.EqualTo(1));
        }

        [Test] public void ScrubbingPausesAndShowsFinalFrameAtEndpoint()
        {
            var clock = Start(0.25f, 0.5f);
            clock.SeekTime(clock.Duration, 1);
            Assert.That(clock.Frame, Is.EqualTo(1));
            Assert.That(clock.Playing, Is.False);
            Assert.That(clock.Finished, Is.False);
        }

        [Test] public void ChangingDurationsKeepsFrameAndShrinkingClampsIt()
        {
            var clock = Start(0.25f, 0.5f, 1f);
            clock.Tick(0.8);
            clock.Configure(new[] { 0.5f, 1f, 0.25f }, 1, false);
            Assert.That(clock.Frame, Is.EqualTo(2));
            Assert.That(clock.Position, Is.EqualTo(1.5));
            Assert.That(clock.Playing, Is.True);
            clock.Configure(new[] { 0.5f }, 2, false);
            Assert.That(clock.Frame, Is.Zero);
            clock.Configure(Array.Empty<float>(), 3, false);
            Assert.That(clock.Frame, Is.EqualTo(-1));
            Assert.That(clock.Playing, Is.False);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDurationsDisablePlayback(float duration)
        {
            var clock = Start(0.25f, duration);
            Assert.That(clock.Valid, Is.False);
            Assert.That(clock.Playing, Is.False);
            clock.Tick(1000000);
        }
    }

    public sealed class BattleVFXDocumentTests
    {
        private string directory, path;
        private GameObject prefab;
        private Sprite sprite;
        private BattleVFXDocument document;

        [SetUp] public void SetUp()
        {
            string name = "VFXEditorTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            directory = "Assets/" + name;
            var texture = new Texture2D(8, 8);
            AssetDatabase.CreateAsset(texture, directory + "/sprite.asset");
            sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
            sprite.name = "Frame";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();
            var root = new GameObject("TestVFX", typeof(RectTransform), typeof(Image), typeof(BattleVFXAnimator));
            try
            {
                var animator = root.GetComponent<BattleVFXAnimator>();
                animator.frames = new[] { new BattleVFXAnimator.FrameData { frameSprite = sprite, duration = 0.25f } };
                animator.images = new[] { sprite };
                animator.defaultDuration = 0.5f;
                root.GetComponent<Image>().color = Color.cyan;
                root.transform.localScale = Vector3.one * 5;
                path = directory + "/effect.prefab";
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            document = ScriptableObject.CreateInstance<BattleVFXDocument>();
            document.Load(prefab);
        }

        [TearDown] public void TearDown()
        {
            if (document != null) { Undo.ClearUndo(document); Object.DestroyImmediate(document); }
            if (!string.IsNullOrEmpty(directory)) AssetDatabase.DeleteAsset(directory);
        }

        [Test] public void DraftAndPlaybackDoNotChangePrefabBytes()
        {
            string before = File.ReadAllText(path);
            document.frames[0].duration = 0.75f;
            var clock = new BattleVFXTimeline(); clock.Configure(document.Durations(), 0, true); clock.SetPlaying(true, 0);
            for (int i = 0; i < 100; i++) clock.Tick(i * 0.1);
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(prefab.GetComponent<BattleVFXAnimator>().frames[0].duration, Is.EqualTo(0.25f));
            Assert.That(document.HasChanges, Is.True);
        }

        [Test] public void DefaultEditDoesNotOverwriteIndividualTimesUntilApplied()
        {
            document.defaultDuration = 0.125f;
            Assert.That(document.frames[0].duration, Is.EqualTo(0.25f));
            document.ApplyDefaultToAll();
            Assert.That(document.frames[0].duration, Is.EqualTo(0.125f));
        }

        [Test] public void AppendUsesDefaultAndKeepsExistingFrames()
        {
            document.Append(new[] { sprite, sprite });
            Assert.That(document.frames.Length, Is.EqualTo(3));
            Assert.That(document.frames[0].duration, Is.EqualTo(0.25f));
            Assert.That(document.frames[2].duration, Is.EqualTo(0.5f));
        }

        [Test] public void SaveRoundTripPreservesImageTransformImagesAndGuid()
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            document.frames[0].duration = 0.75f;
            document.defaultDuration = 0.125f;
            document.useNativeSize = false;
            document.Save();
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = contents.GetComponent<BattleVFXAnimator>();
                Assert.That(animator.frames[0].duration, Is.EqualTo(0.75f));
                Assert.That(animator.frames[0].frameSprite, Is.EqualTo(sprite));
                Assert.That(animator.defaultDuration, Is.EqualTo(0.125f));
                Assert.That(animator.useNativeSize, Is.False);
                Assert.That(animator.images, Is.EqualTo(new[] { sprite }));
                Assert.That(contents.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
                Assert.That(contents.transform.localScale, Is.EqualTo(Vector3.one * 5));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(document.HasChanges, Is.False);
        }

        [Test] public void ExternalPrefabChangeIsNotOverwritten()
        {
            File.AppendAllText(path, "\n");
            string changed = File.ReadAllText(path);
            document.frames[0].duration = 1;
            Assert.Throws<InvalidOperationException>(() => document.Save());
            Assert.That(File.ReadAllText(path), Is.EqualTo(changed));
            Assert.That(document.HasChanges, Is.True);
        }

        [Test] public void InvalidFrameCannotBeSaved()
        {
            string before = File.ReadAllText(path);
            document.frames[0].frameSprite = null;
            Assert.That(document.Validate(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => document.Save());
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test] public void UndoAndRedoRestoreDraftWithoutTouchingPrefab()
        {
            string before = File.ReadAllText(path);
            Undo.RegisterCompleteObjectUndo(document, "Change VFX duration");
            document.frames[0].duration = 2;
            Undo.PerformUndo();
            Assert.That(document.frames[0].duration, Is.EqualTo(0.25f));
            Undo.PerformRedo();
            Assert.That(document.frames[0].duration, Is.EqualTo(2));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }
    }
}
