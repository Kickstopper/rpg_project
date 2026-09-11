using System;
using System.Collections.Generic;
using RPGProject.Feature.Battle;
using RPGProject.Feature.Characters;
using RPGProject.Feature.Skills;
using RPGProject.Shared.Gameplay;
using UnityEngine;

namespace RPGProject.Feature.Negotiation
{
    public enum KinshipGift { Farewell, Gold, Item, Recovery }

    /// <summary>Data selection only. No inventory, finance, UI or party mutations.</summary>
    public static class NegotiationKinshipRules
    {
        public const int MinGoldPerLevel = 5;
        public const int MaxGoldPerLevel = 15;

        public static bool HasCompanion(MonsterDatabase.MonsterEntry monster, IEnumerable<RuntimeCharacterData> party)
        {
            if (monster == null || string.IsNullOrWhiteSpace(monster.id) || party == null) return false;
            foreach (var member in party)
                if (member != null && member.isMonster && member.characterId == monster.id) return true;
            return false;
        }

        public static int GoldAmount(int level, float roll)
        {
            long lv = Math.Max(1, level);
            long min = Math.Min(int.MaxValue, lv * MinGoldPerLevel);
            long max = Math.Min(int.MaxValue, lv * MaxGoldPerLevel);
            long offset = (long)Math.Floor((max - min + 1) * (double)NegotiationTradeRules.ClampRoll(roll));
            return (int)Math.Min(max, min + offset);
        }

        public static PlayerController FindRecoveryTarget(IEnumerable<PlayerController> players, SkillData skill)
        {
            if (players == null || skill == null || skill.effectValue <= 0) return null;
            bool hp = skill.effectType == EffectType.Recover_HP;
            if (!hp && skill.effectType != EffectType.Recover_MP) return null;
            PlayerController best = null;
            long bestCurrent = 0, bestMax = 1;
            foreach (var player in players)
            {
                // Cards are hidden during dialogue, so activeInHierarchy is intentionally not required.
                if (player == null || player.sourceData == null || !player.sourceData.isRegular || !player.IsAlive) continue;
                int current = hp ? player.currentHp : player.currentMp;
                int max = hp ? player.maxHp : player.maxMp;
                if (max <= 0 || current < 0 || current >= max) continue;
                if (hp && Mathf.FloorToInt(skill.effectValue * player.StatusEffects.Multiplier(d => d.healingReceivedMultiplier)) <= 0) continue;
                // Exact ratio comparison; equal ratios keep party/slot order.
                if (best == null || (long)current * bestMax < bestCurrent * max)
                { best = player; bestCurrent = current; bestMax = max; }
            }
            return best;
        }

        public static string Introduction(Personality personality)
        {
            switch (personality)
            {
                case Personality.Aggressive: return "뭐야, 네 동료 중에 우리 동족이 있었잖아! 그 녀석 잘 챙겨라. 알겠지?";
                case Personality.Sly: return "어라, 우리 동족과 한패였어? 진작 말하지. 그 친구 잘 부탁해.";
                case Personality.Foolish: return "어? 저기 우리 동족이다! 너 좋은 녀석이구나. 우리 친구 잘 부탁한다!";
                case Personality.Childish: return "앗! 우리 친구랑 같이 다니는 거야? 그럼 싸우면 안 되지! 잘 대해 줘!";
                case Personality.Proud: return "그대의 일행에 내 동족이 있었군. 그 인연을 보아 물러나겠다. 동족을 잘 부탁한다.";
                case Personality.Principled: return "우리 동족을 동료로 받아 주었군. 동료의 벗에게 칼을 겨눌 수는 없지. 앞으로도 잘 부탁한다.";
                case Personality.Rational: return "일행에서 동족을 확인했다. 서로 싸울 이유는 없겠군. 그 동료를 잘 부탁한다.";
                default: return "일행에 제 동족이 있었군요. 동족을 잘 부탁드립니다. 저희는 물러나겠습니다.";
            }
        }

        public static string GiftLine(Personality personality, KinshipGift gift)
        {
            bool polite = personality == Personality.Polite;
            bool child = personality == Personality.Childish;
            switch (gift)
            {
                case KinshipGift.Gold:
                    return polite ? "여비에 보태세요. 소중히 써 주시면 좋겠습니다." : child ? "이 돈 가져! 친구랑 맛있는 거 사 먹어!" : "돈을 주마. 소중히 쓰도록 해.";
                case KinshipGift.Item:
                    return polite ? "이 물건을 받아 주세요. 도움이 되었으면 좋겠습니다." : child ? "이거 줄게! 너희가 쓰면 좋겠어!" : "이거라도 가져가라. 소중히 쓰도록 해.";
                case KinshipGift.Recovery:
                    return polite ? "지쳐 보이는 분이 계시군요. 제 회복 마법을 받아 주세요." : child ? "많이 지쳤구나? 내가 회복시켜 줄게!" : "지친 동료가 있군. 내 회복 마법을 받아라.";
                default:
                    return polite ? "저희는 이만 가 보겠습니다. 무사히 여행하시길 바랍니다." : child ? "그럼 우리 갈게! 친구랑 사이좋게 지내!" : "우리는 물러나겠다. 동족을 잘 부탁한다.";
            }
        }

        public static List<Dictionary<string, string>> CreateDialogues(MonsterDatabase.MonsterEntry monster)
        {
            return new List<Dictionary<string, string>>
            {
                Row("INTRO", monster.name, Introduction(monster.personality), "KIN_OFFER"),
                Row("KIN_OFFER", monster.name, GiftLine(monster.personality, KinshipGift.Farewell), "CHECK_MOOD:KINSHIP"),
                Row("KIN_RESULT", monster.name, GiftLine(monster.personality, KinshipGift.Farewell), "END"),
                Row("FAIL", monster.name, GiftLine(monster.personality, KinshipGift.Farewell), "END")
            };
        }

        private static Dictionary<string, string> Row(string seq, string name, string text, string next)
        {
            return new Dictionary<string, string>
            {
                ["EventID"] = "KINSHIP", ["Seq"] = seq, ["Type"] = "TALK", ["Name"] = name,
                ["Text"] = text, ["Condition"] = "", ["Action"] = "", ["NextID"] = next
            };
        }
    }
}
