using System;
using Data;
using Helper;

namespace RPGProject.Negotiation
{
    public enum DemandKind { Gold, HP, MP, Item }

    public struct NegotiationDemand
    {
        public DemandKind Kind;
        public int Amount;
        public string ItemID;

        public static bool TryParse(string value, out NegotiationDemand demand)
        {
            demand = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            int amount;
            if (int.TryParse(value, out amount))
            {
                if (amount <= 0) return false;
                demand = new NegotiationDemand { Kind = DemandKind.Gold, Amount = amount };
                return true;
            }
            foreach (var prefix in new[] { "HP_", "MP_", "Gold_" })
            {
                if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(value.Substring(prefix.Length), out amount) || amount <= 0) return false;
                demand = new NegotiationDemand
                {
                    Kind = prefix == "HP_" ? DemandKind.HP : prefix == "MP_" ? DemandKind.MP : DemandKind.Gold,
                    Amount = amount
                };
                return true;
            }
            
            if (char.IsDigit(value[0]) || value[0] == '-' || value[0] == '+' || value.Contains(":")) return false;
            demand = new NegotiationDemand { Kind = DemandKind.Item, ItemID = value, Amount = 1 };
            return true;
        }
    }

    /// <summary>One negotiation attempt. No scene objects, UI or global managers.</summary>
    public sealed class NegotiationSession
    {
        private readonly Personality personality;
        private readonly Race race;
        private readonly EnvironmentState environment;
        private readonly Func<NegotiationDemand, bool> pay;
        private readonly Func<bool> recruit;
        private readonly Func<bool> giveItem;
        private readonly System.Collections.Generic.Dictionary<string, string> resolved =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        private int choices;
        public int Anger { get; private set; }
        public int Joy { get; private set; }
        public int Interest { get; private set; }
        public bool Recruited { get; private set; }
        public bool RewardGranted { get; private set; }
        public bool Closed { get; private set; }
        public bool MustStop => Anger >= 100 || choices >= 8;

        public NegotiationSession(Personality personality, Race race, EnvironmentState environment,
            int anger, int joy, int interest, Func<NegotiationDemand, bool> pay,
            Func<bool> recruit, Func<bool> giveItem)
        {
            this.personality = personality; this.race = race; this.environment = environment;
            Anger = Clamp(anger); Joy = Clamp(joy); Interest = Clamp(interest);
            this.pay = pay; this.recruit = recruit; this.giveItem = giveItem;
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
        public void Close() { Closed = true; }

        public void ApplyTone(ChoiceTone tone)
        {
            if (Closed || MustStop) return;
            choices++;
            var delta = NegotiationCalculator.CalculateMoodChange(tone, personality, race, environment);
            Anger = Clamp(Anger + delta.addedAnger);
            Joy = Clamp(Joy + delta.addedJoy);
            Interest = Clamp(Interest + delta.addedInterest);
        }

        // A side effect is committed at most once per node per attempt, including failed payments.
        public string Resolve(string nodeID, string instruction)
        {
            if (Closed) return "END";
            instruction = (instruction ?? "").Trim();
            if (!instruction.StartsWith("CHECK_MOOD:", StringComparison.Ordinal)) return instruction;
            if (resolved.TryGetValue(nodeID, out var cached)) return cached;
            string result = ResolveCore(instruction);
            resolved[nodeID] = result;
            return result;
        }

        private string ResolveCore(string instruction)
        {
            string[] parts = instruction.Split(':');
            if (MustStop) return "FAIL";
            switch (parts[1])
            {
                case "RECRUIT":
                    if (Recruited) return "SUCCESS_RECRUIT";
                    if ((Joy >= 100 || Interest >= 100) && recruit != null && recruit())
                    { Recruited = true; return "SUCCESS_RECRUIT"; }
                    return "FAIL_RECRUIT";
                case "ITEM":
                    if (RewardGranted) return "SUCCESS_ITEM";
                    if (Joy >= 50 && Interest >= 50 && giveItem != null && giveItem())
                    { RewardGranted = true; return "SUCCESS_ITEM"; }
                    return "FAIL_ITEM";
                case "GIVE":
                    if (parts.Length == 3 && parts[2] == "REFUSE") return "END";
                    if (parts.Length != 4 || parts[2] != "ACCEPT" ||
                        !NegotiationDemand.TryParse(parts[3], out var demand)) return "FAIL";
                    if (pay == null || !pay(demand)) return "INSUFFICIENT_ITEM";
                    // Offering a bribe gives no benefit; only a successful payment does.
                    Joy = Clamp(Joy + (personality == Personality.Foolish ? 45 : 30));
                    Interest = Clamp(Interest + 30);
                    return "NEGO_START";
                case "ANGRY":
                case "DISAPPOINT":
                    Anger = 100;
                    return "FAIL";
                default: return parts.Length == 2 ? parts[1] : "FAIL";
            }
        }
    }
}
