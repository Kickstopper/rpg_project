using System;
using System.Collections.Generic;


namespace RPGProject.Feature.Negotiation
{
    public static class NegotiationScriptValidator
    {
        public static string Value(Dictionary<string, string> row, string key) =>
            row.TryGetValue(key, out var value) ? (value ?? "").Trim() : "";

        public static List<string> Validate(List<Dictionary<string, string>> rows)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (rows == null || rows.Count == 0) { errors.Add("교섭 행이 없습니다."); return errors; }
            foreach (var row in rows)
            {
                string id = Value(row, "Seq");
                if (id.Length == 0 || !ids.Add(id)) errors.Add($"Seq가 비어 있거나 중복됩니다: {id}");
            }
            if (!ids.Contains("INTRO")) errors.Add("INTRO 행이 필요합니다.");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                string id = Value(row, "Seq"), type = Value(row, "Type").ToUpperInvariant();
                if (type != "TALK" && type != "CHOICE" && type != "BRANCH") errors.Add($"{id}: 지원하지 않는 Type '{type}'");
                string action = Value(row, "Action");
                if (action.Length > 0)
                {
                    // Rewards are committed by CHECK_MOOD, never by presentation of a TALK line.
                    string[] parts = action.Split(':');
                    if (type != "BRANCH" || parts.Length != 2 || parts[0] != "TONE" ||
                        !Enum.TryParse(parts[1], true, out ChoiceTone tone) || !Enum.IsDefined(typeof(ChoiceTone), tone))
                        errors.Add($"{id}: 교섭 Action은 BRANCH의 TONE:태도만 지원합니다.");
                }
                string condition = Value(row, "Condition");
                if (condition.Length > 0 && type != "BRANCH") errors.Add($"{id}: Condition은 BRANCH에만 지정하세요.");
                foreach (string clause in condition.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(clause)) continue;
                    string[] p = clause.Trim().Split(':');
                    bool valid = p.Length == 2 && p[0] == "FLAG" && !string.IsNullOrWhiteSpace(p[1]);
                    if ((p.Length == 2 || p.Length == 3) && p[0] == "HASITEM" && !string.IsNullOrWhiteSpace(p[1]))
                    {
                        valid = p.Length == 2 || (int.TryParse(p[2], out int count) && count > 0);
                        if (p[1].StartsWith("Gold_", StringComparison.Ordinal))
                            valid &= p.Length == 2 && int.TryParse(p[1].Substring(5), out int gold) && gold > 0;
                    }
                    if (!valid) errors.Add($"{id}: 잘못된 조건 '{clause}'");
                }
                string next = Value(row, "NextID");
                if (type == "CHOICE")
                {
                    var branches = new List<string>();
                    for (int j = i + 1; j < rows.Count && Value(rows[j], "Type").ToUpperInvariant() == "BRANCH"; j++)
                        branches.Add(Value(rows[j], "Seq"));
                    if (branches.Count == 0) errors.Add($"{id}: 바로 아래에 BRANCH가 필요합니다.");
                    // Empty NextID uses contiguous branches; an explicit list must match them.
                    if (next.Length > 0 && next != string.Join(",", branches)) errors.Add($"{id}: NextID 목록과 실제 BRANCH 순서가 다릅니다.");
                    continue;
                }
                if (next.Length == 0) { errors.Add($"{id}: NextID가 필요합니다."); continue; }
                if (!next.StartsWith("CHECK_MOOD:", StringComparison.Ordinal))
                { Require(next, id, ids, errors); continue; }
                string[] command = next.Split(':');
                if (command[1] == "TRADE")
                {
                    if (command.Length != 3 || !NegotiationTradeRules.TryGoal(command[2], out _))
                        errors.Add($"{id}: TRADE에는 Recruit/Item/Gold/HP/MP를 지정하세요.");
                    foreach (string target in new[] { "DEMAND_GOLD", "DEMAND_ITEM", "DEMAND_HP", "DEMAND_MP", "TRADE_DECLINED" })
                        Require(target, id, ids, errors);
                }
                else if (command[1] == "PAY")
                {
                    if (command.Length != 3 || !NegotiationTradeRules.TryDemandKind(command[2], out _))
                        errors.Add($"{id}: PAY에는 Gold/Item/HP/MP를 지정하세요.");
                    foreach (string target in new[] { "SUCCESS_RECRUIT", "SUCCESS_ITEM", "SUCCESS_GOLD", "SUCCESS_HP", "SUCCESS_MP", "FLED", "FAIL_REWARD", "INSUFFICIENT_ITEM", "TRADE_DECLINED" })
                        Require(target, id, ids, errors);
                }
                else if (command[1] == "SETTLE")
                {
                    if (command.Length != 2) errors.Add($"{id}: SETTLE에는 인수를 지정하지 않습니다.");
                    foreach (string target in new[] { "SUCCESS_RECRUIT", "SUCCESS_ITEM", "SUCCESS_GOLD", "SUCCESS_HP", "SUCCESS_MP", "FLED", "FAIL_REWARD" })
                        Require(target, id, ids, errors);
                }
                else if (command[1] == "GIVE")
                {
                    if (command.Length == 3 && command[2] == "REFUSE") continue;
                    if (command.Length != 4 || command[2] != "ACCEPT" || !NegotiationDemand.TryParse(command[3], out _))
                        errors.Add($"{id}: GIVE:ACCEPT에는 양수 금액, HP_n, MP_n 또는 아이템 ID가 필요합니다.");
                    Require("NEGO_START", id, ids, errors); Require("INSUFFICIENT_ITEM", id, ids, errors);
                }
                else if (command.Length != 2) errors.Add($"{id}: 잘못된 CHECK_MOOD 명령");
                else if (command[1] == "RECRUIT")
                { Require("SUCCESS_RECRUIT", id, ids, errors); Require("FAIL_RECRUIT", id, ids, errors); }
                else if (command[1] == "ITEM")
                { Require("SUCCESS_ITEM", id, ids, errors); Require("FAIL_ITEM", id, ids, errors); }
                else if (command[1] != "ANGRY" && command[1] != "DISAPPOINT") Require(command[1], id, ids, errors);
            }
            Require("FAIL", "공통 실패", ids, errors);
            return errors;
        }

        private static void Require(string target, string from, HashSet<string> ids, List<string> errors)
        {
            if (target != "END" && !ids.Contains(target)) errors.Add($"{from}: 목적지 '{target}'가 없습니다.");
        }
    }
}
