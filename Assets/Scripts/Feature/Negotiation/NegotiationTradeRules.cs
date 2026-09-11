using System;
using System.Collections.Generic;

namespace RPGProject.Feature.Negotiation
{
    public enum NegotiationRewardKind { Recruit, Item, Gold, HP, MP }

    public static class NegotiationTradeRules
    {
        public const int GoldDemand = 100;
        public const int HpDemand = 5;
        public const int MpDemand = 3;
        public const string ItemDemandID = "item_000";
        public const int GoldReward = 50;
        public const int HpReward = 10;
        public const int MpReward = 5;

        public static NegotiationDemand Demand(DemandKind kind) => new NegotiationDemand {
            Kind = kind, Amount = kind == DemandKind.Gold ? GoldDemand : kind == DemandKind.HP ? HpDemand :
                kind == DemandKind.MP ? MpDemand : 1, ItemID = kind == DemandKind.Item ? ItemDemandID : null };

        public static DemandKind ChooseDemand(Personality personality, Race race, float roll)
        {
            var options = new List<DemandKind> { DemandKind.Gold, DemandKind.Item, DemandKind.HP, DemandKind.MP };
            if (race == Race.Machine || race == Race.Construct) options.Remove(DemandKind.HP);
            if (race == Race.Spirit || race == Race.Divine || race == Race.Construct) options.Add(DemandKind.MP);
            if (race == Race.Undead || race == Race.Aberration) options.Add(DemandKind.HP);
            if (race == Race.Dragon || personality == Personality.Sly) options.Add(DemandKind.Gold);
            if (race == Race.Beast || personality == Personality.Foolish || personality == Personality.Childish)
                options.Add(DemandKind.Item);
            return options[Math.Min(options.Count - 1, (int)(ClampRoll(roll) * options.Count))];
        }

        public static float FleeChance(Personality personality)
        {
            switch (personality)
            {
                case Personality.Sly: return 0.35f;
                case Personality.Aggressive: return 0.15f;
                case Personality.Childish: return 0.10f;
                case Personality.Foolish: return 0.05f;
                case Personality.Proud: return 0.05f;
                default: return 0f;
            }
        }

        public static float ClampRoll(float value) => float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(1f, value));

        public static bool TryGoal(string text, out NegotiationRewardKind goal)
        {
            goal = default;
            foreach (string name in Enum.GetNames(typeof(NegotiationRewardKind)))
                if (string.Equals(name, text, StringComparison.Ordinal))
                { goal = (NegotiationRewardKind)Enum.Parse(typeof(NegotiationRewardKind), text); return true; }
            return false;
        }

        public static bool TryDemandKind(string text, out DemandKind kind)
        {
            kind = default;
            foreach (string name in Enum.GetNames(typeof(DemandKind)))
                if (string.Equals(name, text, StringComparison.Ordinal))
                { kind = (DemandKind)Enum.Parse(typeof(DemandKind), text); return true; }
            return false;
        }

        public static string ExpandText(string text, string itemName)
        {
            return (text ?? "").Replace("{DemandGold}", GoldDemand.ToString())
                .Replace("{DemandHP}", HpDemand.ToString()).Replace("{DemandMP}", MpDemand.ToString())
                .Replace("{DemandItem}", string.IsNullOrWhiteSpace(itemName) ? ItemDemandID : itemName)
                .Replace("{RewardGold}", GoldReward.ToString()).Replace("{RewardHP}", HpReward.ToString())
                .Replace("{RewardMP}", MpReward.ToString());
        }
    }
}
