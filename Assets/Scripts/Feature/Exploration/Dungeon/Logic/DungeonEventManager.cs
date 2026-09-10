using UnityEngine;
using System.Collections.Generic;
using RPGProject.Core;

namespace RPGProject.Feature.Exploration
{
    public class DungeonEventManager : MonoBehaviour
    {
        // Includes completed repeatable dialogues. SaveManager uses the same completedDialogues list.
        private readonly HashSet<string> completedEvents = new HashSet<string>();
        private string currentMapID = string.Empty;

        public void SetCurrentMapID(string mapID) => currentMapID = mapID;

        public string GetMapID(MapData map) => map == null || string.IsNullOrEmpty(map.mapID) ? currentMapID : map.mapID;
        private static bool CheckFlag(string flag) => ManagerRoot.Flag != null && ManagerRoot.Flag.CheckFlag(flag);
        private static bool HasDialogue(string id) => ManagerRoot.Dialogue != null && ManagerRoot.Dialogue.HasEvent(id);

        public CellEventData FindAutomaticEvent(MapData map, int x, int y, bool checkOnAttempt)
        {
            if (map == null) return null;
            return DungeonCellEventRules.Find(map.GetCell(x, y), GetMapID(map), x, y, completedEvents,
                CheckFlag, false, checkOnAttempt, HasDialogue);
        }

        public CellEventData FindRepeatableEvent(MapData map, int x, int y)
        {
            if (map == null) return null;
            return DungeonCellEventRules.Find(map.GetCell(x, y), GetMapID(map), x, y, completedEvents,
                CheckFlag, true, false, HasDialogue);
        }

        // This is a query only: do not mark an event as seen before it actually finishes.
        public (string eventID, int forceDir) CheckEvent(int x, int y, bool checkOnAttempt)
        {
            var map = ManagerRoot.Dungeon != null ? ManagerRoot.Dungeon.CurrentDungeonData : null;
            var ev = FindAutomaticEvent(map, x, y, checkOnAttempt);
            return ev == null ? (null, -1) : (ev.eventID, ev.useForceDir ? (int)ev.evForceDir : -1);
        }

        public void MarkCompleted(string mapID, int x, int y, string eventID)
        {
            if (string.IsNullOrWhiteSpace(eventID)) return;
            completedEvents.Add(DungeonCellEventRules.Key(mapID, x, y, eventID));
        }

        public void ResetAllEvents() => completedEvents.Clear();
        public List<string> GetCompletedTriggers() => new List<string>(completedEvents);

        public void ApplyCompletedTriggers(List<string> savedCompletedList)
        {
            completedEvents.Clear();
            if (savedCompletedList == null) return;
            foreach (string key in savedCompletedList)
                if (!string.IsNullOrEmpty(key)) completedEvents.Add(key);
        }
    }
}
