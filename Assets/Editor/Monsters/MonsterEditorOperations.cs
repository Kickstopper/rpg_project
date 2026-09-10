using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Skills;
using UnityEngine;

namespace MonsterEditing
{
    public static class MonsterEditorOperations
    {
        public static string NewId(IEnumerable<MonsterDatabase.MonsterEntry> entries)
        {
            var used = new HashSet<string>((entries ?? Enumerable.Empty<MonsterDatabase.MonsterEntry>()).Where(e => e != null).Select(e => e.id), StringComparer.Ordinal);
            int number = 0;
            while (used.Contains("enemy_" + number.ToString("D3"))) number++;
            return "enemy_" + number.ToString("D3");
        }

        public static MonsterDatabase.MonsterEntry NewEntry(IEnumerable<MonsterDatabase.MonsterEntry> entries)
        {
            var entry = new MonsterDatabase.MonsterEntry
            {
                id = NewId(entries), name = "새 몬스터", image = Array.Empty<Sprite>(),
                fallDownImgs = Array.Empty<Sprite>(), downImgs = Array.Empty<Sprite>(),
                leftImgs = Array.Empty<Sprite>(), rightImgs = Array.Empty<Sprite>(), upImgs = Array.Empty<Sprite>(),
                skills = new List<SkillData>(), dropItemIds = new List<string>()
            };
            entry.stats.level = 1;
            return entry;
        }

        public static MonsterDatabase.MonsterEntry Duplicate(MonsterDatabase.MonsterEntry source,
            IEnumerable<MonsterDatabase.MonsterEntry> entries)
        {
            // Unity's serializer copies nested arrays/lists while retaining referenced asset identities.
            var copy = JsonUtility.FromJson<MonsterDatabase.MonsterEntry>(JsonUtility.ToJson(source));
            copy.id = NewId(entries); copy.name = source.name + " (복사)";
            return copy;
        }

        public static int FillMissingIds(List<MonsterDatabase.MonsterEntry> entries)
        {
            int changed = 0;
            if (entries == null) return 0;
            foreach (var entry in entries)
                if (entry != null && string.IsNullOrWhiteSpace(entry.id)) { entry.id = NewId(entries); changed++; }
            return changed;
        }
    }
}
