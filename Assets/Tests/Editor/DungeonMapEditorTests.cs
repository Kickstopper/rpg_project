using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Data;
using NUnit.Framework;
using UI.DungeonMapScene;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class DungeonMapEditorTests
{
    private readonly List<Object> created = new List<Object>();
    private string temporaryDirectory;
    private T Track<T>(T value) where T : Object { created.Add(value); return value; }
    private static MapData Map(int width = 3, int height = 3) => DungeonMapEditing.Create(width, height, "TestMap", "Area", "TestTheme", true);

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "DungeonEditorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        Undo.IncrementCurrentGroup();
    }
    [TearDown]
    public void TearDown()
    {
        foreach (var value in created) if (value != null) { Undo.ClearUndo(value); Object.DestroyImmediate(value); }
        created.Clear();
        if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
    }
    [TestCase(0, 3)]
    [TestCase(3, 0)]
    [TestCase(-1, 3)]
    [TestCase(257, 3)]
    public void InvalidDimensionsAreRejectedBeforeAllocation(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Map(width, height));
    }
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase((int)CellType.Item_Shop)]
    public void WallOperationsPreserveFloorKindObjectsAndEvents(int kind)
    {
        var cell = new CellData { value = kind, floorTexIdx = 7, centerObjectID = 11 };
        cell.events.Add(new CellEventData { eventID = "KeepEvent" });
        DungeonMapEditing.SetWalls(cell, 2);
        DungeonMapEditing.SetWalls(cell, -1);
        Assert.That(cell.wallTextureIDs, Is.All.EqualTo(-1));
        Assert.That(cell.value, Is.EqualTo(kind));
        Assert.That(cell.floorTexIdx, Is.EqualTo(7));
        Assert.That(cell.centerObjectID, Is.EqualTo(11));
        Assert.That(cell.events[0].eventID, Is.EqualTo("KeepEvent"));
    }
    [Test]
    public void BatchFloorAndCeilingChangeOnlyTheSelectedTilesAndUndoTogether()
    {
        var document = Track(ScriptableObject.CreateInstance<DungeonMapEditorDocument>());
        document.map = Map();
        document.Edit("선택 타일의 바닥과 천장", map =>
        {
            foreach (var p in new[] { new Vector2Int(0, 0), new Vector2Int(2, 1) })
            {
                DungeonMapEditing.Paint(map.GetCell(p.x, p.y), DungeonPaintLayer.Floor, 0, 5);
                DungeonMapEditing.Paint(map.GetCell(p.x, p.y), DungeonPaintLayer.Ceiling, 0, 6);
            }
        });
        Assert.That(document.map.GetCell(0, 0).floorTexIdx, Is.EqualTo(5));
        Assert.That(document.map.GetCell(2, 1).ceilTexIdx, Is.EqualTo(6));
        Assert.That(document.map.GetCell(1, 1).floorTexIdx, Is.EqualTo(-1));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(document.map.cells.Select(c => c.floorTexIdx), Is.All.EqualTo(-1));
        Assert.That(document.map.cells.Select(c => c.ceilTexIdx), Is.All.EqualTo(-1));
        Undo.PerformRedo();
        Assert.That(document.map.GetCell(2, 1).floorTexIdx, Is.EqualTo(5));
        Assert.That(document.map.GetCell(0, 0).ceilTexIdx, Is.EqualTo(6));
    }
    [Test]
    public void FloodFillStopsAtRoomWallsAndAtDifferentMaterials()
    {
        var map = Map(3, 1);
        map.GetCell(0, 0).wallTextureIDs[1] = 0;
        Assert.That(DungeonMapEditing.FloodRegion(map, Vector2Int.zero, DungeonPaintLayer.Floor, 0).Count, Is.EqualTo(1));
        map.GetCell(0, 0).wallTextureIDs[1] = -1;
        map.GetCell(2, 0).floorTexIdx = 8;
        Assert.That(DungeonMapEditing.FloodRegion(map, Vector2Int.zero, DungeonPaintLayer.Floor, 0).Count, Is.EqualTo(2));
    }
    [Test]
    public void ResizePreservesSurvivingContentWithoutMutatingSourceAndCanUndo()
    {
        var document = Track(ScriptableObject.CreateInstance<DungeonMapEditorDocument>());
        var source = Map(); document.map = source;
        source.startX = 2; source.startY = 2;
        source.GetCell(1, 1).interactSetFlag = "chest_open";
        source.GetCell(1, 1).events.Add(new CellEventData { eventID = "event", requiredFlag = "flag", triggerOnAttempt = true });
        source.entrances.Add(new EntranceData { sourceX = 0, sourceY = 0, destinationID = "A" });
        source.entrances.Add(new EntranceData { sourceX = 1, sourceY = 1, destinationID = "B" });
        document.Edit("맵 축소", _ => document.map = DungeonMapEditing.Resize(source, 2, 2, -1, -1));
        Assert.That(source.GetCell(1, 1).x, Is.EqualTo(1));
        Assert.That(source.entrances.Count, Is.EqualTo(2));
        Assert.That(document.map.GetCell(0, 0).interactSetFlag, Is.EqualTo("chest_open"));
        Assert.That(document.map.GetCell(0, 0).events[0].triggerOnAttempt, Is.True);
        Assert.That(document.map.entrances.Count, Is.EqualTo(1));
        Assert.That(document.map.startX, Is.EqualTo(1));
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Assert.That(document.map.width, Is.EqualTo(3));
        Assert.That(document.map.entrances.Count, Is.EqualTo(2));
    }
    [Test]
    public void CroppedSelfPortalTargetDoesNotBecomeTheDefaultStartSentinel()
    {
        var source = Map();
        source.entrances.Add(new EntranceData { type = EntranceType.Map, sourceX = 2, sourceY = 2,
            destinationID = source.mapID, targetX = 0, targetY = 0 });
        var resized = DungeonMapEditing.Resize(source, 2, 2, -1, -1);
        Assert.That(resized.entrances[0].targetX, Is.LessThan(-1));
        Assert.That(resized.entrances[0].targetY, Is.LessThan(-1));
    }
    [Test]
    public void DefaultPortalStartSurvivesResize()
    {
        var source = Map();
        source.entrances.Add(new EntranceData { type = EntranceType.Map, sourceX = 1, sourceY = 1, destinationID = source.mapID });
        var resized = DungeonMapEditing.Resize(source, 4, 4, 1, 1);
        Assert.That(resized.entrances[0].targetX, Is.EqualTo(-1));
        Assert.That(resized.entrances[0].targetY, Is.EqualTo(-1));
    }
    [Test]
    public void InvalidJsonStructureIsRejected()
    {
        var source = Map();
        source.cells = new[] { new CellData() };
        Assert.That(DungeonMapEditing.TryRead(JsonUtility.ToJson(source), out var loaded, out var error), Is.False);
        Assert.That(loaded, Is.Null);
        Assert.That(error, Is.Not.Empty);
        Assert.That(DungeonMapEditing.TryRead("not json", out _, out _), Is.False);
    }
    [Test]
    public void RoundTripPreservesEventsInteractionsAndRandomMazeSettings()
    {
        var source = Map(); source.hasCeil = false;
        source.GetCell(0, 0).events.Add(new CellEventData { eventID = "Script_1", requiredFlag = "door_unlocked", requiredFlagState = false,
            isEventRepeatable = true, triggerOnAttempt = true, useForceDir = true, evForceDir = Direction.West });
        source.GetCell(0, 0).canInteract = true;
        source.GetCell(0, 0).interactChangeObjectID = 9;
        source.GetCell(0, 0).faceObjectIDs[3] = 7;
        source.entrances.Add(new EntranceData { type = EntranceType.RandomMaze, randomMapMaxCount = 8, randomMapRepeatCount = 3,
            finalDestinationID = "Finish", randomMapThemeID = "Cave", isWallEntrance = true });
        string json = JsonUtility.ToJson(source);
        Assert.That(DungeonMapEditing.TryRead(json, out var loaded, out var error), Is.True, error);
        Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(json));
    }
    [TestCase("../Other")]
    [TestCase("Map:1")]
    [TestCase("CON")]
    [TestCase("nul.json")]
    [TestCase("LPT9")]
    [TestCase("Trailing.")]
    public void IDsMustBePortableFileNames(string id) => Assert.That(DungeonMapFileIO.ValidID(id), Is.False);

    [Test]
    public void AtomicSaveKeepsPreviousVersionAndRemovesTemporaryFile()
    {
        string path = Path.Combine(temporaryDirectory, "map.json");
        DungeonMapFileIO.WriteAtomic(path, "first");
        DungeonMapFileIO.WriteAtomic(path, "second");
        Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("first"));
        Assert.That(Directory.GetFiles(temporaryDirectory, "*.tmp"), Is.Empty);
    }
    [Test]
    public void FailedWriteKeepsExistingFile()
    {
        string path = Path.Combine(temporaryDirectory, "map.json");
        File.WriteAllText(path, "keep");
        Assert.Throws<ArgumentNullException>(() => DungeonMapFileIO.WriteAtomic(path, null));
        Assert.That(File.ReadAllText(path), Is.EqualTo("keep"));
        Assert.That(Directory.GetFiles(temporaryDirectory, "*.tmp"), Is.Empty);
    }
    [Test]
    public void DuplicateIdentityCheckIncludesFileNamesAndIgnoresOnlyTheSamePath()
    {
        var index = new DungeonMapEditorIndex();
        var asset = Track(new TextAsset("{}")); asset.name = "Cave";
        index.maps.Add(new DungeonMapEditorIndex.Entry { asset = asset, path = "Assets/Cave.json", map = Map() });
        Assert.That(index.HasIdentityConflict("cave", "Assets/Other.json"), Is.True);
        Assert.That(index.HasIdentityConflict("TestMap", "Assets/Other.json"), Is.True);
        Assert.That(index.HasIdentityConflict("Cave", "Assets/Cave.json"), Is.False);
    }
    [Test]
    public void InteractionIDsAcceptBothWallTexturesAndObjectIDs()
    {
        var texture = Track(new Texture2D(64, 64));
        var theme = Track(ScriptableObject.CreateInstance<DungeonTheme>());
        theme.texture = new[] { texture };
        theme.objectSprites = new[] { new ObjectSpriteData { objectID = 42, texture = texture } };
        Assert.That(DungeonMapValidation.InteractionIDValid(theme, 0), Is.True);
        Assert.That(DungeonMapValidation.InteractionIDValid(theme, 42), Is.True);
        Assert.That(DungeonMapValidation.InteractionIDValid(theme, -1), Is.True);
        Assert.That(DungeonMapValidation.InteractionIDValid(theme, 900), Is.False);
    }
    [Test]
    public void PreviewRenderDoesNotRequireManagerRoot()
    {
        var texture = Track(new Texture2D(64, 64, TextureFormat.RGBA32, false));
        texture.SetPixels32(Enumerable.Repeat(new Color32(80, 100, 120, 255), 64 * 64).ToArray()); texture.Apply();
        var theme = Track(ScriptableObject.CreateInstance<DungeonTheme>());
        theme.texture = new[] { texture }; theme.floorTexIdx = 0; theme.ceilingTexIdx = 0;
        var map = Map();
        foreach (var cell in map.cells) DungeonMapEditing.SetWalls(cell, 0);
        var player = new DungeonPlayer(null, 0.66f, 0, new List<int>());
        player.SetMapData(map, 1, 1, Direction.North);
        var renderer = new RaycastRenderEngine(); renderer.Initialize(64, 32); Track(renderer.ScreenTexture);
        renderer.LoadAssets(theme, Array.Empty<Sprite>(), 64, 64, Array.Empty<SpriteInfo>());
        renderer.SetMapData(map, theme, new TileAnimState[map.width, map.height]);
        Assert.DoesNotThrow(() => renderer.RenderFrame(player, new UI.DungeonMapScene.RenderSettings(), false, 0));
        Assert.That(renderer.ScreenTexture.GetPixels32().Length, Is.EqualTo(64 * 32));
    }
}
