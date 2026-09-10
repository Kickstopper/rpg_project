using System;
using System.Collections.Generic;

namespace RPGProject.Feature.Exploration
{
    /// <summary>Read-only event selection. An event becomes seen only after its dialogue completes.</summary>
    public static class DungeonCellEventRules
    {
        // Keep the existing save-key format so old one-shot history remains valid.
        public static string Key(string mapID, int x, int y, string eventID) => $"{mapID}_{x}_{y}_{eventID}";

        public static CellEventData Find(CellData cell, string mapID, int x, int y,
            ISet<string> seen, Func<string, bool> checkFlag, bool replay, bool checkOnAttempt,
            Func<string, bool> hasDialogue = null)
        {
            if (cell == null || cell.events == null) return null;
            foreach (var ev in cell.events)
            {
                if (ev == null || string.IsNullOrWhiteSpace(ev.eventID)) continue;
                if (!string.IsNullOrEmpty(ev.requiredFlag) &&
                    (checkFlag == null || checkFlag(ev.requiredFlag) != ev.requiredFlagState)) continue;
                if (hasDialogue != null && !hasDialogue(ev.eventID)) continue;
                bool wasSeen = seen != null && seen.Contains(Key(mapID, x, y, ev.eventID));
                if (replay)
                {
                    if (ev.isEventRepeatable && wasSeen) return ev;
                }
                else if (!wasSeen && ev.triggerOnAttempt == checkOnAttempt) return ev;
            }
            return null;
        }
    }
}
