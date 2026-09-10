using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Exploration;

using UnityEditor;
using UnityEngine;

public sealed class DungeonMapIssue
{
    public MessageType severity;
    public string message;
    public Vector2Int position;
    public DungeonMapIssue(MessageType severity, string message, int x = -1, int y = -1)
    { this.severity = severity; this.message = message; position = new Vector2Int(x, y); }
}

public static class DungeonMapValidation
{
    public static List<DungeonMapIssue> Check(MapData map, DungeonMapEditorIndex index, string path)
    {
        var result = new List<DungeonMapIssue>();
        if (!DungeonMapEditing.CheckStructure(map, out string error))
        { result.Add(new DungeonMapIssue(MessageType.Error, error)); return result; }
        Action<string, int, int> fail = (text, x, y) => result.Add(new DungeonMapIssue(MessageType.Error, text, x, y));
        if (!DungeonMapFileIO.ValidID(map.mapID)) fail("맵 ID에 파일명으로 사용할 수 없는 문자가 있습니다.", -1, -1);
        if (index.HasIdentityConflict(map.mapID, DungeonMapFileIO.AssetPath(path))) fail("다른 맵과 ID 또는 파일명이 중복됩니다.", -1, -1);
        if (!string.IsNullOrEmpty(path) && System.IO.Path.GetFileNameWithoutExtension(path) != map.mapID)
            fail("파일명과 맵 ID가 다릅니다. 다른 이름으로 저장하세요.", -1, -1);
        var theme = index.Theme(map.themeID);
        if (theme == null) fail("테마가 없거나 같은 테마 ID가 중복되어 있습니다.", -1, -1);
        else
        {
            if (!TextureValid(theme, theme.floorTexIdx)) fail("테마의 기본 바닥 텍스처가 잘못되었습니다.", -1, -1);
            if (map.hasCeil && !TextureValid(theme, theme.ceilingTexIdx)) fail("테마의 기본 천장 텍스처가 잘못되었습니다.", -1, -1);
            if (theme.texture == null || theme.texture.Length == 0 || theme.texture.Any(t => t == null || !t.isReadable || t.width != 64 || t.height != 64))
                fail("현재 게임 렌더러는 Read/Write가 켜진 64×64 텍스처를 사용합니다. 테마의 모든 벽/바닥 이미지를 확인하세요.", -1, -1);
            if (theme.objectSprites != null)
            {
                if (theme.objectSprites.Any(o => o.texture != null && !o.texture.isReadable))
                    fail("테마의 오브젝트 이미지에 Read/Write가 꺼져 있습니다.", -1, -1);
                if (theme.objectSprites.Where(o => o.texture != null).GroupBy(o => o.objectID).Any(g => g.Count() > 1))
                    fail("테마의 오브젝트 ID가 중복됩니다.", -1, -1);
            }
        }
        var start = map.GetCell(map.startX, map.startY);
        if (start == null) fail("시작점이 맵 밖에 있습니다.", -1, -1);
        else if (start.value == (int)CellType.Void_Floor || IsObstacle(theme, start.centerObjectID))
            fail("시작점이 바닥 구멍 또는 장애물 위에 있습니다.", start.x, start.y);
        if (!Enum.IsDefined(typeof(Direction), map.startDirection)) fail("시작 방향이 잘못되었습니다.", -1, -1);
        foreach (var cell in map.cells)
        {
            if (!Enum.IsDefined(typeof(CellType), cell.value)) fail("알 수 없는 타일 종류입니다.", cell.x, cell.y);
            if (theme != null)
            {
                if (cell.wallTextureIDs.Any(v => v < -1 || (v >= 0 && !TextureValid(theme, v))) ||
                    cell.floorTexIdx < -1 || (cell.floorTexIdx >= 0 && !TextureValid(theme, cell.floorTexIdx)) ||
                    cell.ceilTexIdx < -1 || (cell.ceilTexIdx >= 0 && !TextureValid(theme, cell.ceilTexIdx)))
                    fail("테마에 없는 텍스처 번호가 있습니다.", cell.x, cell.y);
                if (!ObjectValid(theme, cell.centerObjectID) || cell.faceObjectIDs.Any(v => !ObjectValid(theme, v)))
                    fail("테마에 없는 오브젝트 번호가 있습니다.", cell.x, cell.y);
                if (cell.canInteract && (!InteractionIDValid(theme, cell.interactChangeObjectID) || !InteractionIDValid(theme, cell.interactTargetTexID)))
                    fail("상호작용의 대상 또는 변경 재료가 테마에 없습니다.", cell.x, cell.y);
            }
            foreach (var ev in cell.events)
                if (string.IsNullOrWhiteSpace(ev.eventID))
                    result.Add(new DungeonMapIssue(MessageType.Warning, "이벤트가 비어 있어 실행되지 않습니다.", cell.x, cell.y));
        }
        var entranceCells = new HashSet<Vector2Int>();
        foreach (var e in map.entrances)
        {
            if (map.GetCell(e.sourceX, e.sourceY) == null) fail("입구가 맵 밖에 있습니다.", -1, -1);
            if (!entranceCells.Add(new Vector2Int(e.sourceX, e.sourceY))) fail("같은 타일에 입구가 중복됩니다.", e.sourceX, e.sourceY);
            if (!Enum.IsDefined(typeof(EntranceType), e.type) || !Enum.IsDefined(typeof(Direction), e.targetDirection))
                fail("입구 종류 또는 방향이 잘못되었습니다.", e.sourceX, e.sourceY);
            if (e.type == EntranceType.Map && !e.isWorldMap)
            {
                var target = e.destinationID == map.mapID ? map : index.Map(e.destinationID)?.map;
                if (target == null) fail("도착 맵을 찾을 수 없거나 파일명이 중복됩니다.", e.sourceX, e.sourceY);
                else
                {
                    bool useDefault = e.targetX == -1 && e.targetY == -1;
                    var targetCell = target.GetCell(useDefault ? target.startX : e.targetX, useDefault ? target.startY : e.targetY);
                    if (targetCell == null) fail("도착 좌표가 잘못되었습니다. 기본 시작점은 X/Y를 모두 -1로 설정합니다.", e.sourceX, e.sourceY);
                    else if (targetCell.value == -1 || IsObstacle(index.Theme(target.themeID), targetCell.centerObjectID))
                        fail("도착 지점이 바닥 구멍 또는 장애물 위에 있습니다.", e.sourceX, e.sourceY);
                }
            }
            else if (e.type == EntranceType.Map && e.isWorldMap && !index.regions.Any(r => r.regionID == e.destinationID))
                fail("월드 지역을 찾을 수 없습니다.", e.sourceX, e.sourceY);
            else if (e.type == EntranceType.Shop && !index.shops.Any(s => s.shopID == e.destinationID))
                fail("상점을 찾을 수 없습니다.", e.sourceX, e.sourceY);
            else if (e.type == EntranceType.RandomMaze)
            {
                if (e.randomMapWidth < 3 || e.randomMapWidth > 255 || e.randomMapHeight < 3 || e.randomMapHeight > 255 ||
                    e.randomMapMaxCount < 1 || e.randomMapRepeatCount < 1 || e.randomMapRepeatCount > e.randomMapMaxCount)
                    fail("랜덤 미로 크기는 3~255, 남은 층수는 1~전체 층수여야 합니다.", e.sourceX, e.sourceY);
                if (!string.IsNullOrEmpty(e.randomMapThemeID) && index.Theme(e.randomMapThemeID) == null)
                    fail("랜덤 미로 테마를 찾을 수 없습니다.", e.sourceX, e.sourceY);
                if (e.finalDestinationID != map.mapID && index.Map(e.finalDestinationID) == null)
                    fail("랜덤 미로의 최종 도착 맵을 찾을 수 없습니다.", e.sourceX, e.sourceY);
            }
            else if (e.type == EntranceType.Elevator || e.type == EntranceType.FieldMap)
            {
                var ids = e.type == EntranceType.Elevator ? index.ElevatorIDs() : index.FieldNodeIDs();
                if (index.managersPrefab == null)
                    result.Add(new DungeonMapIssue(MessageType.Warning, "매니저 프리팹을 지정하면 엘리베이터/필드 경로를 검사할 수 있습니다.", e.sourceX, e.sourceY));
                else if (!ids.Contains(e.destinationID)) fail("매니저에 등록되지 않은 목적지입니다.", e.sourceX, e.sourceY);
            }
        }
        return result;
    }
    public static bool TextureValid(DungeonTheme theme, int id) => theme != null && theme.texture != null &&
        id >= 0 && id < theme.texture.Length && theme.texture[id] != null;
    public static bool ObjectValid(DungeonTheme theme, int id) => id == -1 ||
        (id >= 0 && theme != null && theme.objectSprites != null && theme.objectSprites.Any(o => o.objectID == id && o.texture != null));
    public static bool InteractionIDValid(DungeonTheme theme, int id) => id == -1 || TextureValid(theme, id) || ObjectValid(theme, id);
    public static bool IsObstacle(DungeonTheme theme, int id) => id >= 0 && theme != null && theme.objectSprites != null &&
        theme.objectSprites.Any(o => o.objectID == id && o.isObstacle);
}
