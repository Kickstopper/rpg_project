using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGProject.Feature.Exploration;
using RPGProject.Feature.Shop;

using UnityEditor;
using UnityEngine;

// 프로젝트 에셋 조회는 다시 검색할 때만 실행하고 OnGUI에서는 캐시를 사용합니다.
public sealed class DungeonMapEditorIndex
{
    public const string CatalogPath = "Assets/Database/Dungeons/DungeonMapCatalog.asset";
    public sealed class Entry
    {
        public TextAsset asset;
        public string path;
        public MapData map;
        public string Key => asset.name; // 현재 런타임의 조회 키
        public string Label => $"{map.mapID} ({map.width}×{map.height}) — {path}";
    }
    public readonly List<Entry> maps = new List<Entry>();
    public readonly List<DungeonTheme> themes = new List<DungeonTheme>();
    public readonly List<ShopData> shops = new List<ShopData>();
    public readonly List<RegionTheme> regions = new List<RegionTheme>();
    public readonly List<string> events = new List<string>();
    public readonly List<string> flags = new List<string>();
    public readonly List<string> problems = new List<string>();
    public GameObject managersPrefab;

    public void Refresh()
    {
        maps.Clear(); themes.Clear(); shops.Clear(); regions.Clear(); events.Clear(); flags.Clear(); problems.Clear();
        LoadAssets(themes); LoadAssets(shops); LoadAssets(regions);
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null || !asset.text.Contains("\"cells\"") || !asset.text.Contains("\"mapID\"")) continue;
            if (!DungeonMapEditing.TryRead(asset.text, out var map, out var error))
            { problems.Add(path + ": " + error); continue; }
            maps.Add(new Entry { asset = asset, path = path, map = map });
            foreach (var cell in map.cells)
            {
                foreach (var ev in cell.events) { AddUnique(events, ev.eventID); AddUnique(flags, ev.requiredFlag); }
                AddUnique(events, cell.interactEventID);
                AddUnique(flags, cell.interactReqFlag); AddUnique(flags, cell.interactSetFlag);
            }
        }
        maps.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));
        events.Sort(StringComparer.Ordinal); flags.Sort(StringComparer.Ordinal);
        if (managersPrefab == null)
        {
            var candidates = AssetDatabase.FindAssets("Managers t:Prefab")
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null && g.GetComponentInChildren<DungeonManager>(true) != null).ToArray();
            if (candidates.Length == 1) managersPrefab = candidates[0];
        }
    }

    private static void LoadAssets<T>(List<T> target) where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) target.Add(asset);
        }
        target.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    private static void AddUnique(List<string> list, string value)
    { if (!string.IsNullOrWhiteSpace(value) && !list.Contains(value)) list.Add(value); }

    public DungeonTheme Theme(string id)
    {
        var matches = themes.Where(t => t.themeID == id).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    public Entry Map(string key)
    {
        var matches = maps.Where(m => m.Key == key).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    public IEnumerable<string> ElevatorIDs()
    {
        var manager = managersPrefab != null ? managersPrefab.GetComponentInChildren<DungeonManager>(true) : null;
        return manager != null && manager.elevatorList != null
            ? manager.elevatorList.Where(e => e != null).Select(e => e.id) : Enumerable.Empty<string>();
    }
    public IEnumerable<string> FieldNodeIDs()
    {
        var manager = managersPrefab != null ? managersPrefab.GetComponentInChildren<FieldMapManager>(true) : null;
        return manager != null && manager.allMapNodes != null
            ? manager.allMapNodes.Where(n => n != null).Select(n => n.mapID) : Enumerable.Empty<string>();
    }
    public bool HasIdentityConflict(string id, string excludedAssetPath) =>
        maps.Any(m => !string.Equals(m.path, excludedAssetPath, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(m.map.mapID, id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(m.Key, id, StringComparison.OrdinalIgnoreCase)));

    public static DungeonMapCatalog Register(string assetPath, DungeonTheme theme)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (asset == null) throw new InvalidOperationException("저장한 JSON 에셋을 찾지 못했습니다.");
        var catalog = AssetDatabase.LoadAssetAtPath<DungeonMapCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null)
                throw new InvalidOperationException("카탈로그 경로를 다른 에셋이 사용하고 있습니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            AssetDatabase.Refresh();
            catalog = ScriptableObject.CreateInstance<DungeonMapCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.maps = catalog.maps ?? new List<TextAsset>();
        catalog.themes = catalog.themes ?? new List<DungeonTheme>();
        if (catalog.maps.Any(m => m != null && m != asset && m.name == asset.name))
            throw new InvalidOperationException("카탈로그에 같은 파일명의 다른 맵이 있습니다.");
        if (theme != null && catalog.themes.Any(t => t != null && t != theme && t.themeID == theme.themeID))
            throw new InvalidOperationException("카탈로그에 같은 ID의 다른 테마가 있습니다.");
        if (!catalog.maps.Contains(asset)) catalog.maps.Add(asset);
        if (theme != null && !catalog.themes.Contains(theme)) catalog.themes.Add(theme);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    public static bool CatalogConnected(GameObject root, DungeonMapCatalog catalog)
    {
        var manager = root != null ? root.GetComponentInChildren<DungeonManager>(true) : null;
        if (manager == null || catalog == null) return false;
        return new SerializedObject(manager).FindProperty("mapCatalog").objectReferenceValue == catalog;
    }

    public static void ConnectCatalog(GameObject root, DungeonMapCatalog catalog)
    {
        if (root == null || catalog == null || !PrefabUtility.IsPartOfPrefabAsset(root))
            throw new InvalidOperationException("프로젝트의 @Managers 프리팹과 카탈로그가 필요합니다.");
        // 프리팹 에셋에 명시적으로 연결합니다. 씬 인스턴스만 수정하는 실수를 방지합니다.
        string path = AssetDatabase.GetAssetPath(root);
        var instance = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var manager = instance.GetComponentInChildren<DungeonManager>(true);
            if (manager == null) throw new InvalidOperationException("DungeonManager가 없는 프리팹입니다.");
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("mapCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                throw new InvalidOperationException("매니저 프리팹 저장에 실패했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }
    }
}
