using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RPGProject.Feature.Skills;
using RPGProject.Feature.StatusEffects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGProject.Balance
{
    // Freezes referenced definitions once. A run and its replay remain independent of subsequent asset edits.
    public sealed class BalanceBatch : IDisposable
    {
        readonly List<Object> owned = new List<Object>();
        readonly Dictionary<Object, Object> copies = new Dictionary<Object, Object>();
        readonly List<string> manifest = new List<string>();
        public List<BalanceScenario> Configurations { get; } = new List<BalanceScenario>();
        public List<SimReport> Reports { get; } = new List<SimReport>();
        BalanceSimulation current;
        int index;
        public bool Running { get; private set; }
        public bool Paused;
        public string Error { get; private set; }
        public SimFrame LiveFrame { get; private set; }
        public double Seconds { get; private set; }
        public int Completed => Reports.Sum(r => r.trials.Count);
        public int Requested => Reports.Sum(r => r.requested);
        public BalanceBatch(BalanceScenario source, bool sweep)
        {
            try
            {
                foreach (float scale in sweep ? new[] { .8f, 1f, 1.2f } : new[] { 1f })
                {
                    var config = Object.Instantiate(source); config.hideFlags = HideFlags.HideAndDontSave; owned.Add(config);
                    config.enemyStatScale *= scale;
                    config.monsterDatabase = Copy(source.monsterDatabase);
                    if (config.monsterDatabase != null && config.monsterDatabase.entries != null)
                        foreach (var entry in config.monsterDatabase.entries)
                            if (entry != null)
                            {
                                if (entry.skills != null) entry.skills = entry.skills.Select(CopySkill).ToList();
                                entry.aiProfile = Copy(entry.aiProfile);
                            }
                    foreach (var slot in config.party.Concat(config.enemies))
                    {
                        slot.weapon = Copy(slot.weapon);
                        slot.skills = (slot.skills ?? new List<SkillData>()).Select(CopySkill).ToList();
                        slot.startingStatuses = (slot.startingStatuses ?? new List<StatusEffectData>()).Select(Copy).ToList();
                    }
                    Configurations.Add(config);
                    Reports.Add(new SimReport { name = config.scenarioName + (sweep ? $" / 적 능력치 ×{scale:0.0}" : ""),
                        requested = config.trials, baseSeed = config.seed, configuration = JsonUtility.ToJson(config, true),
                        unityVersion = Application.unityVersion });
                }
                foreach (var report in Reports) report.assetManifest = string.Join("\n", manifest);
                Running = true;
            }
            catch { Dispose(); throw; }
        }
        T Copy<T>(T source) where T : ScriptableObject
        {
            if (source == null) return null;
            if (owned.Contains(source)) return source;
            if (copies.TryGetValue(source, out var existing)) return (T)existing;
            var copy = Object.Instantiate(source); copy.hideFlags = HideFlags.HideAndDontSave;
            copies.Add(source, copy); owned.Add(copy);
            using (var sha = SHA256.Create())
            {
                string path = AssetDatabase.GetAssetPath(source);
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(source)))).Replace("-", "");
                manifest.Add(AssetDatabase.AssetPathToGUID(path) + " | " + path + " | " + hash);
            }
            return copy;
        }
        SkillData CopySkill(SkillData source)
        {
            var copy = Copy(source); if (copy != null) copy.statusEffectData = Copy(copy.statusEffectData); return copy;
        }
        public void Tick(double milliseconds = 6)
        {
            if (!Running || Paused) return;
            var timer = Stopwatch.StartNew();
            try
            {
                while (timer.Elapsed.TotalMilliseconds < milliseconds && Running)
                {
                    if (index >= Reports.Count) { Running = false; break; }
                    var report = Reports[index]; var config = Configurations[index];
                    if (current == null) current = new BalanceSimulation(config, unchecked(config.seed + report.trials.Count), report.trials.Count == 0);
                    current.Step();
                    if (current.Frames.Count > 0) LiveFrame = current.Frames[current.Frames.Count - 1];
                    if (!current.Done) continue;
                    report.trials.Add(current.Result); current.Dispose(); current = null;
                    if (report.trials.Count >= report.requested) index++;
                }
            }
            catch (Exception e) { Error = e.ToString(); Cancel(); }
            finally { Seconds += timer.Elapsed.TotalSeconds; }
        }
        public void Cancel()
        {
            Running = false; current?.Dispose(); current = null;
            foreach (var report in Reports) if (report.trials.Count < report.requested) report.cancelled = true;
        }
        public void Dispose()
        {
            Cancel();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
    }
}
