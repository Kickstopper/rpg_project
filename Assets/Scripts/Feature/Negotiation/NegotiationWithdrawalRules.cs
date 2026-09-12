using System;
using System.Collections.Generic;

namespace RPGProject.Feature.Negotiation
{
    public static class NegotiationWithdrawalRules
    {
        public const int HostileAngerThreshold = 75;
        public const float MaximumPeaceChance = 0.95f;

        public static float BasePeaceChance(Personality personality)
        {
            switch (personality)
            {
                case Personality.Polite: return 0.45f;
                case Personality.Aggressive: return 0.05f;
                case Personality.Sly: return 0.20f;
                case Personality.Foolish: return 0.35f;
                case Personality.Childish: return 0.25f;
                case Personality.Proud: return 0.10f;
                case Personality.Principled: return 0.40f;
                case Personality.Rational: return 0.50f;
                default: return 0.25f;
            }
        }

        public static float PeaceChance(Personality personality, int anger, int joy, int interest, int choices)
        {
            anger = ClampMood(anger);
            if (anger >= HostileAngerThreshold) return 0f;
            float chance = BasePeaceChance(personality) + (ClampMood(joy) + ClampMood(interest)) * 0.002f
                + Math.Max(0, Math.Min(4, choices)) * 0.05f - anger * 0.006f;
            return Math.Max(0f, Math.Min(MaximumPeaceChance, chance));
        }

        private static int ClampMood(int value) => Math.Max(0, Math.Min(100, value));
        public static bool IsPeaceful(Personality personality, int anger, int joy, int interest, int choices, float roll) =>
            NegotiationTradeRules.ClampRoll(roll) < PeaceChance(personality, anger, joy, interest, choices);

        public static void PrepareScript(List<Dictionary<string, string>> rows, Personality personality, string name)
        {
            if (rows == null) return;
            bool usesWithdrawal = false;
            foreach (var row in rows)
            {
                string next = NegotiationScriptValidator.Value(row, "NextID");
                if (NegotiationScriptValidator.Value(row, "Type").Equals("BRANCH", StringComparison.OrdinalIgnoreCase)
                    && next == "FAREWELL") row["NextID"] = next = "CHECK_MOOD:WITHDRAW";
                if (next == "CHECK_MOOD:WITHDRAW") usesWithdrawal = true;
            }
            if (!usesWithdrawal) return;
            AddIfMissing(rows, "WITHDRAW_PEACE", name, PeaceLine(personality));
            AddIfMissing(rows, "WITHDRAW_HOSTILE", name, HostileLine(personality));
        }

        private static void AddIfMissing(List<Dictionary<string, string>> rows, string seq, string name, string text)
        {
            if (rows.Exists(r => NegotiationScriptValidator.Value(r, "Seq") == seq)) return;
            rows.Add(new Dictionary<string, string> { ["Seq"] = seq, ["Type"] = "TALK", ["Name"] = name ?? "",
                ["Text"] = text, ["Action"] = "", ["Condition"] = "", ["NextID"] = "END" });
        }

        public static string HostileLine(Personality p)
        {
            switch (p)
            {
                case Personality.Polite: return "먼저 말을 걸어 놓고 이대로 끝내시겠다고요? 저를 우습게 보셨군요!";
                case Personality.Aggressive: return "말을 걸어 놓고 그만 얘기하겠다고? 장난하냐? 각오해라!";
                case Personality.Sly: return "내 시간을 빼앗아 놓고 그냥 가겠다고? 그건 너무 염치없는 거 아니야?";
                case Personality.Foolish: return "어? 벌써 끝이야? 나 놀린 거지? 가만 안 둬!";
                case Personality.Childish: return "먼저 말 걸어 놓고 이제 싫다고? 나랑 장난해? 정말 화났어!";
                case Personality.Proud: return "내 시간을 허비하게 하고 물러나겠다고? 그 무례의 대가를 치러라.";
                case Personality.Principled: return "대화할 뜻도 없이 나를 떠본 것이냐? 그런 태도는 용납할 수 없다.";
                case Personality.Rational: return "협상의 의지가 없다는 뜻으로 받아들이겠다. 이제 말로 해결할 단계는 지났다.";
                default: return "말을 걸어 놓고 그만 얘기하겠다고? 장난하냐?";
            }
        }

        public static string PeaceLine(Personality p)
        {
            switch (p)
            {
                case Personality.Polite: return "서로 다칠 필요는 없겠지요. 오늘은 여기서 물러나겠습니다. 무사히 가세요.";
                case Personality.Aggressive: return "흥, 오늘은 싸울 기분이 아니다. 서로 갈 길이나 가자!";
                case Personality.Sly: return "싸워 봤자 남는 것도 없겠네. 오늘은 서로 못 본 걸로 하자고.";
                case Personality.Foolish: return "그럼 그만 싸우자! 괜히 다치면 아프잖아. 잘 가!";
                case Personality.Childish: return "응, 싸우는 건 이제 싫어. 오늘은 여기까지! 나 갈게!";
                case Personality.Proud: return "더 겨룰 가치는 없겠군. 이번에는 서로 물러나는 것으로 끝내겠다.";
                case Personality.Principled: return "더는 다툴 이유가 없군. 서로 무기를 거두고 이쯤에서 물러나자.";
                case Personality.Rational: return "싸워 봤자 서로 얻을 것은 없을 것 같군. 이쯤에서 물러날까.";
                default: return "싸워 봤자 서로 얻을 것은 없겠군. 이쯤에서 물러나자.";
            }
        }
    }
}
