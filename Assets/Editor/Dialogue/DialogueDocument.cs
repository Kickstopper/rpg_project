using System;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Feature.Dialogue;
using UnityEngine;

namespace DialogueEditing
{
    [Serializable]
    public class DialogueRow
    {
        public string key = Guid.NewGuid().ToString("N");
        public string EventID = "", Seq = "", Type = "TALK", BackgroundID = "", CharacterID = "";
        public string Name = "", Text = "", Condition = "", Action = "", NextID = "END";
        public DialogueRow Copy() { var copy = (DialogueRow)MemberwiseClone(); copy.key = Guid.NewGuid().ToString("N"); return copy; }
        public string[] Cells() => new[] { EventID, Seq, Type, BackgroundID, CharacterID, Name, Text, Condition, Action, NextID };
    }

    public sealed class DialogueDocument : ScriptableObject
    {
        public static readonly string[] Header = { "EventID", "Seq", "Type", "BackgroundID", "CharacterID", "Name", "Text", "Condition", "Action", "NextID" };
        public List<DialogueRow> rows = new List<DialogueRow>();
        public string sourceHash = "", savedCsv = "";
        public bool loaded;

        public static List<DialogueRow> Decode(string csv)
        {
            var records = DialogueCsv.Parse(csv);
            if (records.Count == 0 || records[0].Length != Header.Length || !new HashSet<string>(records[0]).SetEquals(Header))
                throw new FormatException("EventScripts.csv의 10개 열이 필요합니다. 다른 형식의 CSV는 열지 않습니다.");
            var result = new List<DialogueRow>();
            foreach (var d in DialogueCsv.Read(csv))
                result.Add(new DialogueRow { EventID = d["EventID"], Seq = d["Seq"], Type = d["Type"], BackgroundID = d["BackgroundID"],
                    CharacterID = d["CharacterID"], Name = d["Name"], Text = d["Text"], Condition = d["Condition"], Action = d["Action"], NextID = d["NextID"] });
            return result;
        }

        public string Encode() => DialogueCsv.Write(new[] { Header }.Concat(rows.Select(r => r.Cells())));
        public List<DialogueRow> Event(string id) => rows.Where(r => r.EventID == id).ToList();
        public static bool None(string value) => string.IsNullOrEmpty(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase);
        public static string Kind(DialogueRow row) => row.Type.ToUpperInvariant();
        public string NewSeq(string id)
        {
            var used = new HashSet<string>(Event(id).Select(r => r.Seq));
            foreach (var row in Event(id)) foreach (string target in row.NextID.Split(',')) used.Add(target.Trim());
            int n = 1;
            while (used.Contains(n.ToString())) n++;
            return n.ToString();
        }

        public List<DialogueRow> Block(DialogueRow root)
        {
            var result = new List<DialogueRow> { root };
            var lines = Event(root.EventID);
            if (Kind(root) == "CHOICE")
                for (int i = lines.IndexOf(root) + 1; i < lines.Count && Kind(lines[i]) == "BRANCH"; i++) result.Add(lines[i]);
            return result;
        }

        public DialogueRow AddTalk(string id)
        {
            var row = new DialogueRow { EventID = id, Seq = NewSeq(id), Text = "대사를 입력하세요." };
            var last = Event(id).LastOrDefault();
            int index = last == null ? rows.Count : rows.IndexOf(last) + 1;
            rows.Insert(index, row);
            return row;
        }

        public DialogueRow AddChoice(string id)
        {
            DialogueRow root = AddTalk(id);
            root.Type = "CHOICE"; root.Text = "어떻게 할까요?";
            AddBranch(root); AddBranch(root);
            return root;
        }

        public DialogueRow AddBranch(DialogueRow choice)
        {
            if (Kind(choice) != "CHOICE") throw new ArgumentException("선택 질문에만 선택지를 추가할 수 있습니다.");
            List<DialogueRow> block = Block(choice);
            var row = new DialogueRow { EventID = choice.EventID, Seq = NewSeq(choice.EventID), Type = "BRANCH", Text = "새 선택지" };
            rows.Insert(rows.IndexOf(block.Last()) + 1, row);
            RefreshChoiceLinks(choice.EventID);
            return row;
        }

        public DialogueRow Duplicate(DialogueRow row)
        {
            var original = Block(row);
            var mapping = new Dictionary<string, string>();
            var copies = new List<DialogueRow>();
            int index = rows.IndexOf(original.Last()) + 1;
            foreach (DialogueRow source in original)
            {
                var copy = source.Copy(); copy.Seq = NewSeq(row.EventID);
                mapping[source.Seq] = copy.Seq;
                rows.Insert(index++, copy); copies.Add(copy);
            }
            foreach (DialogueRow copy in copies)
                if (mapping.TryGetValue(copy.NextID, out string next)) copy.NextID = next;
            RefreshChoiceLinks(row.EventID);
            return copies[0];
        }

        public void MoveBlock(DialogueRow row, int direction)
        {
            var lines = Event(row.EventID);
            if (Kind(row) == "BRANCH")
            {
                int position = lines.IndexOf(row), other = position + direction;
                if (other < 0 || other >= lines.Count || Kind(lines[other]) != "BRANCH") return;
                int a = rows.IndexOf(row), b = rows.IndexOf(lines[other]);
                rows[a] = lines[other]; rows[b] = row;
            }
            else
            {
                var roots = lines.Where(r => Kind(r) != "BRANCH").ToList();
                int other = roots.IndexOf(row) + direction;
                if (other < 0 || other >= roots.Count) return;
                var block = Block(row); var neighbor = Block(roots[other]);
                foreach (var member in block) rows.Remove(member);
                int insert = direction < 0 ? rows.IndexOf(neighbor[0]) : rows.IndexOf(neighbor.Last()) + 1;
                rows.InsertRange(insert, block);
            }
            RefreshChoiceLinks(row.EventID);
        }

        public void Delete(DialogueRow row)
        {
            foreach (var member in Block(row)) rows.Remove(member);
            RefreshChoiceLinks(row.EventID);
        }

        public void RefreshChoiceLinks(string id)
        {
            foreach (DialogueRow choice in Event(id).Where(r => Kind(r) == "CHOICE"))
                choice.NextID = string.Join(",", Block(choice).Skip(1).Select(r => r.Seq));
        }
    }

    public sealed class DialogueIssue
    {
        public DialogueRow row;
        public string message;
        public bool error;
    }

    public static class DialogueValidation
    {
        public static List<DialogueIssue> Check(DialogueDocument doc)
        {
            var issues = new List<DialogueIssue>();
            void Add(DialogueRow row, string text, bool error = true) => issues.Add(new DialogueIssue { row = row, message = text, error = error });
            foreach (var group in doc.rows.GroupBy(r => r.EventID))
            {
                var rows = group.ToList();
                var ids = new HashSet<string>(rows.Select(r => r.Seq));
                var duplicateIds = new HashSet<string>(rows.GroupBy(r => r.Seq).Where(g => g.Count() > 1).Select(g => g.Key));
                for (int i = 0; i < rows.Count; i++)
                {
                    DialogueRow row = rows[i]; string kind = DialogueDocument.Kind(row);
                    if (string.IsNullOrWhiteSpace(row.EventID) || row.EventID != row.EventID.Trim()) Add(row, "이벤트 ID가 비었거나 앞뒤에 공백이 있습니다.");
                    if (string.IsNullOrWhiteSpace(row.Seq) || row.Seq != row.Seq.Trim() || row.Seq.IndexOfAny(new[] { ',', ':', ';' }) >= 0 || row.Seq.Equals("END", StringComparison.OrdinalIgnoreCase)) Add(row, "단계 ID가 비었거나 예약어/구분자를 포함합니다.");
                    if (duplicateIds.Contains(row.Seq)) Add(row, "같은 이벤트에 단계 ID가 중복됩니다.");
                    if (!new[] { "TALK", "CHOICE", "BRANCH", "JOIN", "LEAVE" }.Contains(kind)) Add(row, "지원하지 않는 단계 종류입니다.");
                    if ((kind == "JOIN" || kind == "LEAVE") && string.IsNullOrWhiteSpace(row.CharacterID)) Add(row, "합류/이탈할 캐릭터를 선택하세요.");
                    if ((kind == "TALK" || kind == "CHOICE" || kind == "BRANCH") && string.IsNullOrWhiteSpace(row.Text)) Add(row, "표시할 문장을 입력하세요.");
                    if (kind == "BRANCH" && (i == 0 || (DialogueDocument.Kind(rows[i - 1]) != "CHOICE" && DialogueDocument.Kind(rows[i - 1]) != "BRANCH"))) Add(row, "선택지는 선택 질문 바로 뒤에 묶여 있어야 합니다.");
                    if (kind == "CHOICE")
                    {
                        var branches = rows.Skip(i + 1).TakeWhile(r => DialogueDocument.Kind(r) == "BRANCH").ToList();
                        if (branches.Count == 0) Add(row, "선택지가 없습니다. '선택지 추가'를 누르세요.");
                        if (branches.Count > 0 && branches.All(r => !DialogueDocument.None(r.Condition))) Add(row, "모든 선택지에 조건이 있어 게임에서 버튼이 하나도 안 나올 수 있습니다. 무조건 선택할 수 있는 항목을 남기세요.", false);
                    }
                    else if (!string.IsNullOrEmpty(row.NextID) && !row.NextID.Equals("END", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!ids.Contains(row.NextID)) Add(row, $"연결된 단계 '{row.NextID}'가 없습니다. 다음 진행을 다시 선택하세요.");
                        else if (rows.Any(r => r.Seq == row.NextID && DialogueDocument.Kind(r) == "BRANCH")) Add(row, "선택지로 직접 이동하면 효과가 실행되지 않습니다. 대사 또는 선택 질문으로 연결하세요.");
                    }
                    if (kind != "BRANCH" && !DialogueDocument.None(row.Condition)) Add(row, "현재 게임은 선택지의 조건만 검사합니다. 이 단계의 조건은 적용되지 않습니다.", false);
                    if (kind != "BRANCH" && !DialogueDocument.None(row.Action)) Add(row, "현재 게임은 선택한 선택지의 효과만 실행합니다. 이 단계의 효과는 적용되지 않습니다.", false);
                    ValidateCommands(row, row.Condition, true, Add);
                    ValidateCommands(row, row.Action, false, Add);
                }
                var reachable = new HashSet<DialogueRow>();
                var pending = new Queue<DialogueRow>();
                if (rows.Count > 0) pending.Enqueue(rows[0]);
                void Follow(DialogueRow from, bool branch)
                {
                    if (from.NextID.Equals("END", StringComparison.OrdinalIgnoreCase) || (branch && string.IsNullOrEmpty(from.NextID))) return;
                    int index = rows.IndexOf(from);
                    var next = string.IsNullOrEmpty(from.NextID) ? (index + 1 < rows.Count ? rows[index + 1] : null) : rows.FirstOrDefault(r => r.Seq == from.NextID);
                    if (next != null) pending.Enqueue(next);
                }
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    if (!reachable.Add(current)) continue;
                    if (DialogueDocument.Kind(current) == "CHOICE")
                    {
                        foreach (var branch in rows.Skip(rows.IndexOf(current) + 1).TakeWhile(r => DialogueDocument.Kind(r) == "BRANCH"))
                        { reachable.Add(branch); Follow(branch, true); }
                    }
                    else Follow(current, false);
                }
                foreach (var row in rows.Where(r => !reachable.Contains(r))) Add(row, "이벤트 시작에서 이 단계로 연결되지 않습니다. 앞 단계의 '다음 진행'을 확인하세요.", false);

                // Recursive runtime advancement without visible text must not form a cycle.
                foreach (DialogueRow start in rows)
                {
                    var seen = new HashSet<DialogueRow>(); DialogueRow cursor = start;
                    while (cursor != null && (DialogueDocument.Kind(cursor) == "BRANCH" ||
                        ((DialogueDocument.Kind(cursor) == "JOIN" || DialogueDocument.Kind(cursor) == "LEAVE") && string.IsNullOrEmpty(cursor.Text))))
                    {
                        if (!seen.Add(cursor)) { Add(start, "표시 없는 단계가 반복 연결되어 게임이 멈출 수 있습니다."); break; }
                        if (cursor.NextID.Equals("END", StringComparison.OrdinalIgnoreCase)) break;
                        int index = rows.IndexOf(cursor);
                        cursor = string.IsNullOrEmpty(cursor.NextID) ? (index + 1 < rows.Count ? rows[index + 1] : null) : rows.FirstOrDefault(r => r.Seq == cursor.NextID);
                    }
                }
            }
            return issues;
        }

        private static void ValidateCommands(DialogueRow row, string text, bool condition, Action<DialogueRow, string, bool> add)
        {
            if (DialogueDocument.None(text)) return;
            foreach (string token in text.Split(';'))
            {
                if (DialogueDocument.None(token.Trim())) continue;
                string[] p = token.Trim().Split(':'); string op = p[0].ToUpperInvariant();
                bool valid = false;
                if (condition && op == "FLAG") valid = p.Length == 2 && !string.IsNullOrWhiteSpace(p[1]);
                if ((condition && op == "HASITEM") || (!condition && op == "REMOVE"))
                {
                    valid = p.Length >= 2 && !string.IsNullOrWhiteSpace(p[1]);
                    if (valid && p[1].StartsWith("Gold_", StringComparison.Ordinal)) valid = p.Length == 2 && int.TryParse(p[1].Substring(5), out int amount) && amount > 0;
                    else if (valid) valid = condition ? p.Length == 2 : p.Length == 2 || (p.Length == 3 && int.TryParse(p[2], out int count) && count > 0);
                }
                if (!condition && op == "SET_FLAG") valid = p.Length == 3 && !string.IsNullOrWhiteSpace(p[1]) && bool.TryParse(p[2], out _);
                if (!valid) add(row, $"{(condition ? "조건" : "효과")} '{token}': 형식을 확인하세요. 일반 이벤트에서는 보유 확인·소모·플래그만 지원합니다. BATTLE/ADD_MOOD는 런타임 미구현, TONE은 교섭 전용입니다.", true);
            }
        }
    }
}
