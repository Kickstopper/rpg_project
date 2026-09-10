using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Dialogue;
using RPGProject.Shared.Gameplay;
using UnityEditor;
using UnityEngine;

namespace DialogueEditing
{
    public sealed class DialogueAssetCatalog
    {
        public sealed class Entry
        {
            public string id, label;
            public Sprite sprite;
        }
        public readonly List<Entry> characters = new List<Entry>();
        public readonly List<Entry> backgrounds = new List<Entry>();
        public readonly List<Entry> items = new List<Entry>();

        public void Reload()
        {
            characters.Clear(); backgrounds.Clear(); items.Clear();
            // Same name/image precedence as DialogueUI: NPC, character, monster.
            foreach (var db in Assets<NpcDatabase>())
                foreach (var e in db.entries) if (e != null) Add(characters, e.id, e.name, e.portraitImage);
            foreach (var db in Assets<CharacterDatabase>())
                foreach (var e in db.entries) if (e != null) Add(characters, e.id, e.name, e.portraitImage);
            foreach (var db in Assets<MonsterDatabase>())
                foreach (var e in db.entries) if (e != null) Add(characters, e.id, e.name, e.portrait);
            foreach (var db in Assets<BackgroundDatabase>())
                foreach (var e in db.entries) if (e != null) Add(backgrounds, e.id, e.bgImage != null ? e.bgImage.name : e.id, e.bgImage);
            foreach (var item in Assets<BaseItemData>()) Add(items, item.id, item.dataName, null);
        }

        private static IEnumerable<T> Assets<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name).OrderBy(g => g))
            {
                T value = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (value != null) yield return value;
            }
        }

        private static void Add(List<Entry> entries, string id, string label, Sprite sprite)
        {
            if (!string.IsNullOrWhiteSpace(id) && !entries.Any(e => e.id == id))
                entries.Add(new Entry { id = id, label = string.IsNullOrEmpty(label) ? id : label, sprite = sprite });
        }
    }
}
