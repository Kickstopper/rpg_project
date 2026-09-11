using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGProject.Feature.Negotiation
{
    public sealed class NegotiationDialogueCatalog
    {
        private sealed class Variant
        {
            public string Seq;
            public Race? Race;
            public Gender? Gender;
            public string Name;
            public string Text;
            public int Specificity => (Race.HasValue ? 2 : 0) + (Gender.HasValue ? 1 : 0);
        }

        private sealed class Script
        {
            public readonly List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
            public readonly List<Variant> Variants = new List<Variant>();
        }

        private readonly Dictionary<string, Script> scripts = new Dictionary<string, Script>(StringComparer.Ordinal);

        public static NegotiationDialogueCatalog Create(List<Dictionary<string, string>> rows)
        {
            if (rows == null || rows.Count == 0) throw new FormatException("교섭 CSV가 비어 있습니다.");
            var result = new NegotiationDialogueCatalog();
            foreach (var source in rows)
            {
                string key = Value(source, "EventID").ToUpperInvariant();
                if (key.Length == 0) throw new FormatException("교섭 EventID가 없습니다.");
                if (!result.scripts.TryGetValue(key, out var script))
                    result.scripts.Add(key, script = new Script());
                string race = Value(source, "Race"), gender = Value(source, "Gender");
                if (race.Length == 0 && gender.Length == 0)
                {
                    var row = new Dictionary<string, string>(source);
                    foreach (var field in new[] { "EventID", "Seq", "Type", "Condition", "Action", "NextID" })
                        row[field] = Value(source, field);
                    row["EventID"] = key;
                    script.Rows.Add(row);
                    continue;
                }

                foreach (var field in new[] { "Type", "Condition", "Action", "NextID" })
                    if (Value(source, field).Length != 0)
                        throw new FormatException($"{key}/{Value(source, "Seq")}: 변형 행의 {field}는 비워 주세요.");
                var variant = new Variant { Seq = Value(source, "Seq"),
                    Name = Value(source, "Name"), Text = Value(source, "Text") };
                if (race.Length != 0) variant.Race = ParseName<Race>(race, key);
                if (gender.Length != 0) variant.Gender = ParseName<Gender>(gender, key);
                if (variant.Text.Length == 0) throw new FormatException($"{key}/{variant.Seq}: 변형 Text가 없습니다.");
                if (script.Variants.Any(v => v.Seq == variant.Seq && v.Race == variant.Race && v.Gender == variant.Gender))
                    throw new FormatException($"{key}/{variant.Seq}: 동일 Race/Gender 변형이 중복됩니다.");
                script.Variants.Add(variant);
            }
            foreach (var pair in result.scripts)
            {
                var errors = NegotiationScriptValidator.Validate(pair.Value.Rows);
                if (errors.Count != 0) throw new FormatException(pair.Key + ": " + string.Join("\n", errors));
                foreach (var variant in pair.Value.Variants)
                    if (!pair.Value.Rows.Any(r => Value(r, "Seq") == variant.Seq))
                        throw new FormatException($"{pair.Key}/{variant.Seq}: 변형 대상 기본 Seq가 없습니다.");
                pair.Value.Variants.Sort((a, b) => a.Specificity.CompareTo(b.Specificity));
            }
            if (!result.scripts.ContainsKey("DEFAULT")) throw new FormatException("DEFAULT 기본 교섭이 필요합니다.");
            return result;
        }

        private static T ParseName<T>(string text, string key) where T : struct
        {
            if (!Enum.GetNames(typeof(T)).Any(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase)))
                throw new FormatException($"{key}: 알 수 없는 {typeof(T).Name} '{text}'");
            return (T)Enum.Parse(typeof(T), text, true);
        }

        public List<Dictionary<string, string>> Resolve(Personality personality, Race race, Gender gender,
            out string selectedKey)
        {
            string p = personality.ToString().ToUpperInvariant(), r = race.ToString().ToUpperInvariant(),
                g = gender.ToString().ToUpperInvariant();
            foreach (string key in new[] { p + "_" + r + "_" + g, p + "_" + r, p + "_" + g, p, "DEFAULT" })
            {
                if (!scripts.TryGetValue(key, out var script)) continue;
                var rows = script.Rows.Select(row => new Dictionary<string, string>(row)).ToList();
                var byId = rows.ToDictionary(row => Value(row, "Seq"), StringComparer.Ordinal);
                foreach (var variant in script.Variants)
                {
                    if ((variant.Race.HasValue && variant.Race.Value != race) ||
                        (variant.Gender.HasValue && variant.Gender.Value != gender)) continue;
                    var row = byId[variant.Seq];
                    row["Text"] = variant.Text;
                    if (variant.Name.Length != 0) row["Name"] = variant.Name;
                }
                selectedKey = key;
                return rows;
            }
            selectedKey = "";
            return new List<Dictionary<string, string>>();
        }

        private static string Value(Dictionary<string, string> row, string key) =>
            NegotiationScriptValidator.Value(row, key);
    }
}
