using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Data;
using UnityEditor;
using UnityEngine;

// 파일 경로와 마지막 저장본은 EditorWindow가 소유합니다.
// Undo는 맵 내용만 되돌리므로 Save As 이후에도 저장 대상이 바뀌지 않습니다.
public sealed class DungeonMapEditorDocument : ScriptableObject
{
    public MapData map;

    public void Edit(string label, Action<MapData> edit)
    {
        Undo.RegisterCompleteObjectUndo(this, label);
        edit(map);
        EditorUtility.SetDirty(this);
    }
}

public enum DungeonPaintLayer { Floor, Ceiling, Wall, CenterObject, FaceObject, CellType }

public static class DungeonMapEditing
{
    public const int MaxSize = 256;
    public static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public static bool ValidSize(int width, int height) =>
        width >= 1 && height >= 1 && width <= MaxSize && height <= MaxSize;

    public static MapData Create(int width, int height, string id, string location, string theme, bool ceiling)
    {
        if (!ValidSize(width, height)) throw new ArgumentOutOfRangeException(nameof(width), "맵 크기는 1~256입니다.");
        var map = new MapData { width = width, height = height, mapID = id, locationID = location,
            themeID = theme, hasCeil = ceiling, cells = new CellData[width * height] };
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) map.cells[y * width + x] = new CellData { x = x, y = y };
        return map;
    }

    public static MapData Clone(MapData map) => JsonUtility.FromJson<MapData>(JsonUtility.ToJson(map));

    // 현재 문서를 교체하기 전에 구조부터 검증합니다. 실패한 JSON은 현재 작업에 영향을 주지 않습니다.
    public static bool TryRead(string json, out MapData map, out string error)
    {
        map = null;
        error = null;
        try
        {
            var candidate = JsonUtility.FromJson<MapData>(json);
            if (!CheckStructure(candidate, out error)) return false;
            candidate.entrances = candidate.entrances ?? new List<EntranceData>();
            foreach (var cell in candidate.cells)
            {
                cell.events = cell.events ?? new List<CellEventData>();
                cell.faceObjectIDs = cell.faceObjectIDs ?? new[] { -1, -1, -1, -1 };
            }
            map = candidate;
            return true;
        }
        catch (Exception ex) { error = "맵 JSON을 읽을 수 없습니다: " + ex.Message; return false; }
    }

    public static bool CheckStructure(MapData map, out string error)
    {
        error = null;
        if (map == null || !ValidSize(map.width, map.height)) error = "맵 크기는 1~256이어야 합니다.";
        else if (map.cells == null || map.cells.Length != map.width * map.height)
            error = "맵 크기와 타일 개수가 일치하지 않습니다.";
        else
        {
            for (int i = 0; i < map.cells.Length; i++)
            {
                var c = map.cells[i];
                if (c == null || c.x != i % map.width || c.y != i / map.width)
                { error = $"타일 {i}의 좌표 또는 데이터가 잘못되었습니다."; break; }
                if (c.wallTextureIDs == null || c.wallTextureIDs.Length != 4 ||
                    (c.faceObjectIDs != null && c.faceObjectIDs.Length != 4))
                { error = $"({c.x}, {c.y})의 벽/오브젝트 방향 배열은 4개여야 합니다."; break; }
                if (c.events != null && c.events.Any(e => e == null))
                { error = $"({c.x}, {c.y})에 비어 있는 이벤트 데이터가 있습니다."; break; }
            }
            if (map.entrances != null && map.entrances.Any(e => e == null)) error = "비어 있는 입구 데이터가 있습니다.";
        }
        return error == null;
    }

    public static int ReadPaint(CellData cell, DungeonPaintLayer layer, int direction)
    {
        switch (layer)
        {
            case DungeonPaintLayer.Floor: return cell.floorTexIdx;
            case DungeonPaintLayer.Ceiling: return cell.ceilTexIdx;
            case DungeonPaintLayer.Wall: return cell.wallTextureIDs[direction];
            case DungeonPaintLayer.CenterObject: return cell.centerObjectID;
            case DungeonPaintLayer.FaceObject: return cell.faceObjectIDs[direction];
            default: return cell.value;
        }
    }

    public static void Paint(CellData cell, DungeonPaintLayer layer, int direction, int value)
    {
        switch (layer)
        {
            case DungeonPaintLayer.Floor: cell.floorTexIdx = value; break;
            case DungeonPaintLayer.Ceiling: cell.ceilTexIdx = value; break;
            case DungeonPaintLayer.Wall: cell.wallTextureIDs[direction] = value; break;
            case DungeonPaintLayer.CenterObject: cell.centerObjectID = value; break;
            case DungeonPaintLayer.FaceObject: cell.faceObjectIDs[direction] = value; break;
            default: cell.value = value; break;
        }
    }

    public static void SetWalls(CellData cell, int texture)
    {
        for (int i = 0; i < 4; i++) cell.wallTextureIDs[i] = texture;
        // 벽만 변경합니다. 바닥 구멍, 상점 등의 셀 종류는 유지합니다.
    }

    public static List<Vector2Int> FloodRegion(MapData map, Vector2Int start, DungeonPaintLayer layer, int direction)
    {
        var result = new List<Vector2Int>();
        if (map.GetCell(start.x, start.y) == null) return result;
        int original = ReadPaint(map.GetCell(start.x, start.y), layer, direction);
        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            result.Add(p);
            for (int d = 0; d < Directions.Length; d++)
            {
                var next = p + Directions[d];
                var cell = map.GetCell(next.x, next.y);
                if (cell == null) continue;
                if ((layer == DungeonPaintLayer.Floor || layer == DungeonPaintLayer.Ceiling) &&
                    (map.GetCell(p.x, p.y).wallTextureIDs[d] >= 0 || cell.wallTextureIDs[(d + 2) % 4] >= 0)) continue;
                if (ReadPaint(cell, layer, direction) == original && visited.Add(next)) queue.Enqueue(next);
            }
        }
        return result;
    }

    public static int CountRemovedEntrances(MapData map, int width, int height, int offsetX, int offsetY) =>
        map.entrances.Count(e => e.sourceX + offsetX < 0 || e.sourceX + offsetX >= width ||
                                e.sourceY + offsetY < 0 || e.sourceY + offsetY >= height);

    public static MapData Resize(MapData source, int width, int height, int offsetX, int offsetY)
    {
        if (!ValidSize(width, height)) throw new ArgumentOutOfRangeException(nameof(width));
        // 원본 셀을 재사용하지 않아 미리보기와 Undo의 참조를 보호합니다.
        var old = Clone(source);
        var result = Clone(source);
        result.width = width;
        result.height = height;
        result.cells = new CellData[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = old.GetCell(x - offsetX, y - offsetY) ?? new CellData();
                cell.x = x; cell.y = y;
                result.cells[y * width + x] = cell;
            }
        result.startX = Mathf.Clamp(source.startX + offsetX, 0, width - 1);
        result.startY = Mathf.Clamp(source.startY + offsetY, 0, height - 1);
        foreach (var entrance in result.entrances)
        {
            entrance.sourceX += offsetX;
            entrance.sourceY += offsetY;
            // 같은 맵을 향하는 명시적 도착 좌표도 같이 이동합니다. -1(기본 시작점)은 유지합니다.
            if (entrance.type == EntranceType.Map && !entrance.isWorldMap && entrance.destinationID == source.mapID)
            {
                if (entrance.targetX >= 0) { entrance.targetX += offsetX; if (entrance.targetX < 0) entrance.targetX = -2; }
                if (entrance.targetY >= 0) { entrance.targetY += offsetY; if (entrance.targetY < 0) entrance.targetY = -2; }
            }
        }
        result.entrances.RemoveAll(e => e.sourceX < 0 || e.sourceX >= width || e.sourceY < 0 || e.sourceY >= height);
        return result;
    }
}

public static class DungeonMapFileIO
{
    public static bool ValidID(string id) => !string.IsNullOrWhiteSpace(id) &&
        id == id.Trim() && id != "." && id != ".." &&
        id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && id.IndexOfAny("<>:\"/\\|?*".ToCharArray()) < 0 &&
        !id.EndsWith(".", StringComparison.Ordinal) && !id.Any(char.IsControl) && !IsReservedName(id);

    private static bool IsReservedName(string id)
    {
        string stem = id.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem)) return true;
        return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9';
    }

    public static string AssetPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string absolute = Path.GetFullPath(path).Replace('\\', '/');
        string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/') + "/";
        return absolute.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ? "Assets/" + absolute.Substring(assets.Length) : null;
    }

    // 기존 파일을 지운 뒤 이동하는 fallback은 사용하지 않습니다.
    // 교체 실패 시 이전 파일과 미저장 상태를 보존합니다.
    public static void WriteAtomic(string path, string json)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        string temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(fullPath)) File.Replace(temp, fullPath, fullPath + ".bak");
            else File.Move(temp, fullPath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
