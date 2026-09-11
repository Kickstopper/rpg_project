using System;



namespace RPGProject.Feature.Negotiation
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
        private readonly Func<NegotiationRewardKind, bool> canTrade;
        private readonly Func<NegotiationRewardKind, bool> giveReward;
        private readonly Func<bool> flee;
        private readonly Func<float> roll;
        private NegotiationRewardKind? tradeGoal;
        private DemandKind pendingDemand;
        private bool tradePaid;
        private string tradeResult;
        private bool resolving;
        private readonly System.Collections.Generic.Dictionary<string, string> resolved =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        private int choices;
        public int Anger { get; private set; }
        public int Joy { get; private set; }
        public int Interest { get; private set; }
        public bool Recruited { get; private set; }
        public bool RewardGranted { get; private set; }
        public bool Closed { get; private set; }
        public bool Fled { get; private set; }
        public bool HasPaidTrade => tradePaid;
        public bool MustStop => Anger >= 100 || choices >= 8;

        public NegotiationSession(Personality personality, Race race, EnvironmentState environment,
            int anger, int joy, int interest, Func<NegotiationDemand, bool> pay,
            Func<bool> recruit, Func<bool> giveItem,
            Func<NegotiationRewardKind, bool> canTrade = null,
            Func<NegotiationRewardKind, bool> giveReward = null, Func<bool> flee = null, Func<float> roll = null)
        {
            this.personality = personality; this.race = race; this.environment = environment;
            Anger = Clamp(anger); Joy = Clamp(joy); Interest = Clamp(interest);
            this.pay = pay; this.recruit = recruit; this.giveItem = giveItem;
            this.canTrade = canTrade; this.giveReward = giveReward; this.flee = flee;
            this.roll = roll ?? (() => 1f);
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
        public void Close() { Closed = true; }

        public void ApplyTone(ChoiceTone tone)
        {
            if (Closed || MustStop || tradeResult != null) return;
            choices++;
            var delta = NegotiationCalculator.CalculateMoodChange(tone, personality, race, environment);
            Anger = Clamp(Anger + delta.addedAnger);
            Joy = Clamp(Joy + delta.addedJoy);
            Interest = Clamp(Interest + delta.addedInterest);
        }

        public string Resolve(string nodeID, string instruction)
        {
            if (Closed) return "END";
            instruction = (instruction ?? "").Trim();
            if (!instruction.StartsWith("CHECK_MOOD:", StringComparison.Ordinal)) return instruction;
            if (resolved.TryGetValue(nodeID, out var cached)) return cached;
            if (resolving) return "FAIL";
            string result;
            resolving = true;
            try { result = ResolveCore(instruction); }
            finally { resolving = false; }
            resolved[nodeID] = result;
            return result;
        }

        private string ResolveCore(string instruction)
        {
            string[] parts = instruction.Split(':');
            if (parts[1] == "SETTLE" && parts.Length == 2) return SettleTrade();
            if (tradeResult != null) return "END";
            if (MustStop) return "FAIL";
            switch (parts[1])
            {
                case "TRADE":
                    if (parts.Length != 3 || !NegotiationTradeRules.TryGoal(parts[2], out var goal)) return "FAIL";
                    if (tradeGoal.HasValue)
                        return tradeGoal.Value == goal && !tradePaid ? "DEMAND_" + pendingDemand.ToString().ToUpperInvariant() : "FAIL";
                    bool moodReady = goal == NegotiationRewardKind.Recruit ? Joy >= 100 || Interest >= 100 : Joy >= 50 && Interest >= 50;
                    
                    if (!moodReady || canTrade == null || !canTrade(goal)) return "TRADE_DECLINED";
                    tradeGoal = goal;
                    pendingDemand = NegotiationTradeRules.ChooseDemand(personality, race, roll());
                    return "DEMAND_" + pendingDemand.ToString().ToUpperInvariant();
                case "PAY":
                    if (parts.Length != 3 || !NegotiationTradeRules.TryDemandKind(parts[2], out var kind) ||
                        !tradeGoal.HasValue || pendingDemand != kind) return "FAIL";
                    if (tradePaid) return SettleTrade();
                    if (canTrade == null || !canTrade(tradeGoal.Value)) return "TRADE_DECLINED";
                    if (pay == null || !pay(NegotiationTradeRules.Demand(kind))) return "INSUFFICIENT_ITEM";
                    tradePaid = true;

                    return SettleTrade();
                case "RECRUIT":
                    if (tradeGoal.HasValue) return "FAIL";
                    if (Recruited) return "SUCCESS_RECRUIT";
                    if ((Joy >= 100 || Interest >= 100) && recruit != null && recruit())
                    { Recruited = true; return "SUCCESS_RECRUIT"; }
                    return "FAIL_RECRUIT";
                case "ITEM":
                    if (tradeGoal.HasValue) return "FAIL";
                    if (RewardGranted) return "SUCCESS_ITEM";
                    if (Joy >= 50 && Interest >= 50 && giveItem != null && giveItem())
                    { RewardGranted = true; return "SUCCESS_ITEM"; }
                    return "FAIL_ITEM";
                case "GIVE":
                    if (tradeGoal.HasValue) return "FAIL";
                    if (parts.Length == 3 && parts[2] == "REFUSE") return "END";
                    if (parts.Length != 4 || parts[2] != "ACCEPT" ||
                        !NegotiationDemand.TryParse(parts[3], out var demand)) return "FAIL";
                    if (pay == null || !pay(demand)) return "INSUFFICIENT_ITEM";

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

        private string SettleTrade()
        {
            if (tradeResult != null) return tradeResult;
            if (!tradeGoal.HasValue || !tradePaid || MustStop) return "FAIL";

            if (NegotiationTradeRules.ClampRoll(roll()) < NegotiationTradeRules.FleeChance(personality))
            {
                Fled = flee != null && flee();
                return tradeResult = Fled ? "FLED" : "FAIL_REWARD";
            }
            var goal = tradeGoal.Value;
            bool succeeded = goal == NegotiationRewardKind.Recruit ? recruit != null && recruit() :
                giveReward != null && giveReward(goal);
            if (!succeeded) return tradeResult = "FAIL_REWARD";
            if (goal == NegotiationRewardKind.Recruit) Recruited = true;
            else RewardGranted = true;
            return tradeResult = "SUCCESS_" + (goal == NegotiationRewardKind.Recruit ? "RECRUIT" : goal.ToString().ToUpperInvariant());
        }
    }
}
