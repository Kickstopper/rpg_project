using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGProject.Infrastructure.Persistence;
using RPGProject.Core;

namespace RPGProject.Feature.Quests
{
    public enum QuestState { Locked, Available, Active, ReadyToReport, Claimed }
    public sealed class QuestReceipt
    {
        public string questID, questName, runID;
        public int gold;
    }

    public class QuestManager : MonoBehaviour
    {
        private readonly Dictionary<string, QuestData> definitions = new Dictionary<string, QuestData>();
        private readonly HashSet<string> completedQuests = new HashSet<string>();
        private readonly Dictionary<string, QuestProgress> activeQuests = new Dictionary<string, QuestProgress>();
        private bool claiming;
        public event Action Changed;
        public int MaxActiveQuests
        {
            get
            {
                var commander = ManagerRoot.Party?.partyData?.Find(p => p.isCommander);
                return 1 + Mathf.Max(1, commander == null ? 1 : commander.stats.level) / 15;
            }
        }
        public void InitializeQuests(List<QuestData> quests)
        {
            definitions.Clear();
            
            foreach (var q in quests ?? new List<QuestData>())
            {
                string error = QuestDefinitionValidation.Error(q);
                if (error != null) { Debug.LogError("[Quest] " + error); continue; }
                if (definitions.ContainsKey(q.QuestID)) { Debug.LogError("중복 QuestID: " + q.QuestID); continue; }
                definitions.Add(q.QuestID, q);
            }
        }
        public List<QuestData> GetAllQuests() => definitions.Values.ToList();
        public QuestData GetQuestData(string id) => id != null && definitions.TryGetValue(id, out var q) ? q : null;
        public List<QuestData> GetActiveQuests() => GetAllQuests().Where(q => IsQuestActive(q.QuestID)).ToList();
        public List<QuestData> GetAvailableQuests() => GetAllQuests().Where(q => GetState(q.QuestID) == QuestState.Available).ToList();
        public List<QuestData> GetReadyToReportQuests() => GetAllQuests().Where(q => GetState(q.QuestID) == QuestState.ReadyToReport).ToList();
        public bool IsQuestActive(string id) => id != null && activeQuests.ContainsKey(id);
        public bool IsQuestCompleted(string id) => GetState(id) == QuestState.Claimed;
        public bool HasCompleted(string id) => id != null && completedQuests.Contains(id);
        public QuestState GetState(string id)
        {
            var q = GetQuestData(id);
            if (q == null) return QuestState.Locked;
            if (activeQuests.TryGetValue(id, out var p)) return p.isReadyToReport ? QuestState.ReadyToReport : QuestState.Active;
            if (HasCompleted(id) && !q.IsRepeatable) return QuestState.Claimed;
            if (q.prerequisiteQuestIDs != null && q.prerequisiteQuestIDs.Any(required => !HasCompleted(required))) return QuestState.Locked;
            return QuestState.Available;
        }
        
        public int GetKillCount(string questID, string monsterID) => activeQuests.TryGetValue(questID, out var p) &&
            p.killCounts.TryGetValue(monsterID, out var count) ? count : 0;
        
        public string GetRunID(string id) => activeQuests.TryGetValue(id, out var p) ? p.runID : null;
        
        public bool TryAcceptQuest(string id, out string reason)
        {
            reason = null;
            if (claiming || GetState(id) != QuestState.Available) 
            {
                reason = "현재 접수할 수 없는 의뢰입니다.";
                return false;
            }

            if (activeQuests.Count >= MaxActiveQuests)
            {
                reason = $"동시 접수 한도는 {MaxActiveQuests}개입니다.";
                return false; 
            }
            
            var p = new QuestProgress { questID = id, runID = Guid.NewGuid().ToString("N") };
            foreach (var t in definitions[id].Targets) p.killCounts.Add(t.monsterID, 0);
            
            activeQuests.Add(id, p);
            RefreshExternalGoals();
            SyncFlags(id); Notify(); return true;
        }
        public void AcceptQuest(string id) { if (!TryAcceptQuest(id, out var reason)) Debug.LogWarning(reason); }

        public bool TryClaimReward(string id, string expectedRunID, out QuestReceipt receipt, out string reason)
        {
            receipt = null; reason = null;
            if (claiming || GetState(id) != QuestState.ReadyToReport ||
                string.IsNullOrEmpty(expectedRunID) || GetRunID(id) != expectedRunID)
            {
                reason = "이미 수령했거나 보고할 수 없는 의뢰입니다.";
                return false;
            }
            
            var finance = ManagerRoot.Finance;
            var q = definitions[id];
            
            if (finance == null || (long)finance.CurrentMoney + q.Reward > int.MaxValue)
            {
                reason = "보상을 지급할 수 없습니다. 소지금 상한을 확인하세요.";
                return false;
            }
            
            claiming = true;
            
            try
            {
                // Finance listeners observe the completed quest and the credited balance together.
                activeQuests.Remove(id); completedQuests.Add(id);
                if (q.IsRepeatable && !string.IsNullOrEmpty(q.completionFlag)) ManagerRoot.Flag?.SetFlag(q.completionFlag, false);
                SyncFlags(id);
                receipt = new QuestReceipt { questID = id, questName = q.QuestName, runID = expectedRunID, gold = q.Reward };
                finance.AddMoney(q.Reward);
            }
            finally { claiming = false; }
            
            Notify();
            return true;
        }
        [Obsolete("Use TryClaimReward with the accepted run ID; completion includes payment.")]
        public void CompleteQuest(string id) { TryClaimReward(id, GetRunID(id), out _, out _); }

        public List<QuestData> RefreshExternalGoals()
        {
            var ready = new List<QuestData>();
            foreach (var pair in activeQuests)
            {
                var q = GetQuestData(pair.Key);
                if (q == null || pair.Value.isReadyToReport || string.IsNullOrEmpty(q.completionFlag)) continue;
                if (ManagerRoot.Flag != null && ManagerRoot.Flag.CheckFlag(q.completionFlag))
                {
                    pair.Value.isReadyToReport = true; SyncFlags(q.QuestID); ready.Add(q);
                }
            }
            if (ready.Count > 0) Notify();
            return ready;
        }
        // Called only after a successful recruitment. Kept separate from the kill record.
        public void RecordNegotiation(string location, string monsterID)
        {
            if (ManagerRoot.Flag != null) ManagerRoot.Flag.SetFlag("QuestTalk_" + location + "_" + monsterID, true);
        }

        public List<QuestData> ProcessBattleResult(string location, List<string> killedIDs)
        {
            var newlyReady = RefreshExternalGoals();
            foreach (var pair in activeQuests)
            {
                var q = GetQuestData(pair.Key); var p = pair.Value;
                if (q == null || p.isReadyToReport || q.locationID != location) continue;
                foreach (var id in killedIDs ?? new List<string>())
                {
                    var t = q.Targets.Find(target => target.monsterID == id);
                    if (t != null) p.killCounts[id] = Mathf.Min(t.requiredCount, p.killCounts[id] + 1);
                }
                if (q.Targets.Count > 0 && q.Targets.All(t => p.killCounts[t.monsterID] >= t.requiredCount))
                { p.isReadyToReport = true; newlyReady.Add(q); SyncFlags(q.QuestID); }
            }
            Notify(); return newlyReady;
        }
        public void NewGame()
        {
            completedQuests.Clear(); activeQuests.Clear(); claiming = false;
            foreach (var id in definitions.Keys) SyncFlags(id);
            Notify();
        }
        public void Save(SaveData data)
        {
            data.completedQuestIDs = completedQuests.ToList();
            data.activeQuests = activeQuests.Values.Select(p => new QuestProgress
            { questID = p.questID, runID = p.runID, isReadyToReport = p.isReadyToReport,
                killCounts = new Dictionary<string, int>(p.killCounts) }).ToList();
        }
        public void Load(SaveData data)
        {
            NewGame();
            foreach (var id in data.completedQuestIDs ?? new List<string>())
                if (GetQuestData(id) != null) completedQuests.Add(id);
            foreach (var saved in data.activeQuests ?? new List<QuestProgress>())
            {
                if (saved == null) continue;
                var q = GetQuestData(saved.questID);
                if (q == null || (HasCompleted(q.QuestID) && !q.IsRepeatable) || activeQuests.ContainsKey(q.QuestID)) continue;
                var p = new QuestProgress { questID = q.QuestID,
                    runID = string.IsNullOrEmpty(saved.runID) ? Guid.NewGuid().ToString("N") : saved.runID };
                foreach (var t in q.Targets)
                {
                    int count = 0;
                    saved.killCounts?.TryGetValue(t.monsterID, out count);
                    p.killCounts[t.monsterID] = Mathf.Clamp(count, 0, t.requiredCount);
                }
                p.isReadyToReport = (q.Targets.Count > 0 && q.Targets.All(t => p.killCounts[t.monsterID] >= t.requiredCount)) ||
                    (!string.IsNullOrEmpty(q.completionFlag) && ManagerRoot.Flag != null && ManagerRoot.Flag.CheckFlag(q.completionFlag));
                activeQuests.Add(q.QuestID, p);
            }
            foreach (var id in definitions.Keys) SyncFlags(id);
            Notify();
        }
        private void SyncFlags(string id)
        {
            if (ManagerRoot.Flag == null) return;
            ManagerRoot.Flag.SetFlag("QuestReady_" + id, GetState(id) == QuestState.ReadyToReport);
            ManagerRoot.Flag.SetFlag("QuestComplete_" + id, HasCompleted(id));
        }
        private void Notify()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
