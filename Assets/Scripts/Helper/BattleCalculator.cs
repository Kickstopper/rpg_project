using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Data;
using Controller;
using UI;
using UI.DungeonMapScene;

namespace Helper
{
    public static class BattleCalculator
    {
        // =======================================================
        // [데미지 공식]
        // =======================================================
        public static int CalculateDamage(BattleEntity attacker, BattleEntity defender, BattleAction action, bool isCritical, float damageMultiplier = 1.0f)
        {
            if (attacker == null || defender == null) return 0;

            bool isMagic = (action.actionData != null && action.actionData.element != ElementType.Physical);
            
            float rawDamage = 0f;
            ElementType element = ElementType.None;

            if (isMagic)
            {
                element = action.actionData.element;   
                
                // [마법 대미지]: (MATK * effectValue) / MDEF
                int matk = attacker.GetMagicAttack();
                int mdef = defender.GetMagicDefense();
                int effectValue = action.actionData != null ? action.actionData.effectValue : 0;
            
                rawDamage = (matk * effectValue) / Mathf.Max(1f, (float)mdef);
            }
            else
            {
                element = ElementType.Physical;
                bool isGun = (action.type == ActionType.Shoot);

                // [물리 대미지]: (ATK * (ATK / 4 + 4)) / DEF
                int atk = isGun ? ((PlayerController)attacker).GetGunAttack() : attacker.GetAttack();
                int def = defender.GetDefense();

                rawDamage = (atk * (atk / 4f + 4f)) / Mathf.Max(1f, (float)def);
            }

            // 성향 상성 및 내성 보정
            float alignBonus = AlignmentSystem.GetDamageModifier(attacker.align, defender.align);
            float resistanceMultiplier = GetResistanceValue(element, defender.GetResistances());

            rawDamage *= damageMultiplier * alignBonus * resistanceMultiplier;
            
            // 10% 난수 분산
            float randomVar = Random.Range(0.9f, 1.1f);
            int finalDamage = Mathf.RoundToInt(rawDamage * randomVar);

            // 방어 및 크리티컬 적용
            if (isCritical) finalDamage *= 2;
            if (defender.isGuarding) finalDamage = Mathf.FloorToInt(finalDamage * 0.5f);

            return Mathf.Max(1, finalDamage);
        }

        // 총기 대미지 헬퍼 (BattleManager 호환용)
        public static int CalculateGunDamage(PlayerController attacker, BattleEntity defender, bool isCritical)
        {
            int atk = attacker.GetGunAttack();
            int def = defender.GetDefense();
            
            float rawDmg = (atk * (atk / 4f + 4f)) / Mathf.Max(1f, (float)def);
            
            float randomVar = Random.Range(0.9f, 1.1f);
            int finalDamage = Mathf.RoundToInt(rawDmg * randomVar);

            if (isCritical) finalDamage *= 2; 
            if (defender.isGuarding) finalDamage = Mathf.FloorToInt(finalDamage * 0.5f);

            return Mathf.Max(1, finalDamage);
        }


        // =======================================================
        // [명중 및 회피율 공식 (256분율 기반)]
        // =======================================================
        public static bool CheckEvasion(BattleEntity attacker, BattleEntity defender, BattleAction action, float positionalEvasionBonus)
        {
            if (attacker == null || defender == null) return false;

            bool isMagic = (action != null && action.actionData != null && action.actionData.element != ElementType.Physical);
            float n = 0;

            if (isMagic)
            {
                // [마법 회피율]: n = ((나의 INT) + (나의 LUCK/4) + 24 - (상대의 MATK)) / 4
                n = (defender.GetTotalInt() + (defender.GetTotalLuc() / 4f) + 24f - attacker.GetMagicAttack()) / 4f;
            }
            else
            {
                // [물리 회피율]: n = (나의 EVA) + 24 - (적의 HIT / 4)
                int defEva = defender.GetEvasion();
                int atkHit = attacker.GetHitRate();

                n = defEva + 24f - (atkHit / 4f);
            }

            // 고전식 확률 곡선: n * (n / 2) / 256
            n = Mathf.Clamp(n, 0f, 255f);
            float dodgeChance = (n * (n / 2f)) / 256f;
            
            // 진형(후열 등)에 의한 추가 보너스
            dodgeChance += positionalEvasionBonus * 100f; 
            
            // 최종 확률 (0~100을 0~1로 변환)
            float finalChance = Mathf.Clamp(dodgeChance / 100f, 0f, 0.9f);

            return Random.value < finalChance;
        }

        public static bool CheckCritical(BattleEntity attacker, BattleEntity defender, BattleAction action)
        {
            // 행운 수치 비례
            int atkLuc = attacker.GetTotalLuc();
            int defLuc = defender.GetTotalLuc();
            float baseCritChance = 0.05f;
            float totalChance = Mathf.Clamp(baseCritChance + (atkLuc - defLuc) * 0.005f, 0f, 0.5f);
            return Random.value < totalChance;
        }
        
        // =======================================================
        // 스폰되는 몬스터의 수 결정
        // =======================================================
        public static int DetermineSpawnCount(int maxCount = 6)
        {
            int spawnCount = 1;
            if (maxCount < 1) maxCount = spawnCount;
            float roll = Random.value;

            // 기획된 가중치 확률 적용
            if (roll < 0.30f)      spawnCount = 1; // 30%
            else if (roll < 0.60f) spawnCount = 2; // 30%
            else if (roll < 0.80f) spawnCount = 3; // 20%
            else if (roll < 0.90f) spawnCount = 4; // 10%
            else if (roll < 0.97f) spawnCount = 5; // 7%
            else                   spawnCount = 6; // 3%

            return Mathf.Min(spawnCount, maxCount);
        }

        // =======================================================
        // [경험치, 재화, 드랍 아이템 보상]
        // =======================================================
        public static BattleManager.BattleReward CalculateRewards(List<PlayerController> players, List<MonsterDatabase.MonsterEntry> encounterLog)
        {
            BattleManager.BattleReward reward = new BattleManager.BattleReward();
            reward.dropItems = new List<string>();

            int totalExp = 0;
            int calculatedMoney = 0;

            float partyAvgLv = (players.Count > 0) ? (float)players.Average(p => p.level) : 1;
            float partyAvgLuc = (players.Count > 0) ? (float)players.Average(p => p.GetTotalLuc()) : 1;

            foreach (var entry in encounterLog)
            {
                int monsterLv = entry.stats.level;
                int baseExp = GetMaxExpForLevel(monsterLv);
                int levelDiff = monsterLv - Mathf.RoundToInt(partyAvgLv);
                
                // 경험치 계산. 레벨 차이에 따른 보정이 들어감
                int finalExp = baseExp;
                if (levelDiff >= 4)
                {
                    int diffClamped = Mathf.Min(levelDiff, 20); // 상한 20
                    finalExp = Mathf.FloorToInt(baseExp * (diffClamped / 4f) * 2f);
                }
                else if (levelDiff <= -4)
                {
                    int absDiff = Mathf.Abs(levelDiff);
                    finalExp = Mathf.FloorToInt(baseExp / ((absDiff / 4f) + 1f));
                }
                totalExp += finalExp;

                // 재화 계산: 30 * (0 ~ 적의 레벨)
                int randLv = Random.Range(1, monsterLv + 1);
                calculatedMoney += (30 * randLv);

                // 아이템 획득 계산: (파티의 운 / 적의 운) / 2
                float enemyLuc = entry.stats.luc > 0 ? entry.stats.luc : 10f; 
                float itemChance = Mathf.Clamp01(partyAvgLuc / enemyLuc) / 2f; // 최대 50%
                
                if (entry.dropItemIds != null && entry.dropItemIds.Count > 0 && Random.value < itemChance)
                {
                    // 테이블 확률 분배: 1번째 25%, 2번째 17%, 3번째 13%, 4번째 10%, 5번째 1%
                    float dropRoll = Random.value;
                    int dropIdx = -1;

                    if (dropRoll < 0.25f) dropIdx = 0;
                    else if (dropRoll < 0.42f) dropIdx = 1;
                    else if (dropRoll < 0.55f) dropIdx = 2;
                    else if (dropRoll < 0.65f) dropIdx = 3;
                    else if (dropRoll < 0.66f) dropIdx = 4;

                    if (dropIdx >= 0 && dropIdx < entry.dropItemIds.Count)
                    {
                        reward.dropItems.Add(entry.dropItemIds[dropIdx]);
                    }
                }
            }

            reward.totalExp = totalExp;
            int livingCount = players.Count(p => p.currentHp > 0);
            reward.expPerMember = (livingCount > 0) ? reward.totalExp / livingCount : 0;
            reward.totalMoney = calculatedMoney;

            return reward;
        }

        // =======================================================
        // [도주 공식]
        // =======================================================
        public static bool CalculateEscapeSuccess(List<BattleEntity> players, List<BattleEntity> monsters, int attempts, int guaranteedAttempts)
        {
            if (attempts >= 4 || attempts >= guaranteedAttempts) return true; // 4회 시도 무조건 성공
            if (players.Count == 0) return false;
            if (monsters.Count == 0) return true;

            float playerAvgAgi = (float)players.Average(p => p.GetTotalAgi());
            float enemyAvgAgi = (float)monsters.Average(m => m.GetTotalAgi());

            // seed = 1 - (그룹의 AGI 평균 / 파티의 AGI 평균)
            float seed = 1f - (enemyAvgAgi / playerAvgAgi);
            
            float chance = Mathf.Clamp(seed, 0.1f, 1.0f); // 최소 10% 도주 확률 보장

            return Random.value < chance;
        }


        // =======================================================
        // [기타 헬퍼 및 내부 계산식]
        // =======================================================
        public static int GetMaxExpForLevel(int level)
        {
            // 필요한 EXP 수식: ((LV^2) + (INT/2) + MAG)/8. 차후 수정하자
            return Mathf.FloorToInt((level * level * 10f) + 15f); 
        }

        public static int GetMaxHP(int level, int str, int vit)
        {
            return ((str + vit) * (level + 1)) / 4 + 14;
        }

        public static int GetMaxMP(int level, int mag, int intel)
        {
            return  ((mag + intel) * (level + 4)) / 8 + 4;
        }

        public static float GetResistanceValue(ElementType element, ResistanceData resist) 
        {
            ResistTier tier = resist.GetResistanceTier(element);

            switch (tier)
            {
                case ResistTier.Weak:   return 1.5f;  // 약점: 1.5배 데미지
                case ResistTier.Normal: return 1.0f;  // 보통: 1.0배 (100%) 데미지
                case ResistTier.Resist: return 0.5f;  // 내성: 0.5배 (50%) 데미지
                case ResistTier.Null:   return 0.0f;  // 무효: 0 배율 (데미지 없음)
                
                case ResistTier.Repel:
                case ResistTier.Drain:
                default:
                    return 1.0f;
            }
        }
        public static bool IsAlignCompatible(Align a, Align b) { return a == b || a == Align.True_Neutral || b == Align.True_Neutral; }
        public static void GetPositionalModifiers(BattleFieldController.BattlePosition atkPos, BattleFieldController.BattlePosition defPos, WeaponType wType, out float damageMultiplier, out float evasionBonus) { damageMultiplier=1f; evasionBonus=0f; }
        public static void ProcessSkillStatusEffect(BattleEntity attacker, BattleEntity defender, SkillData skill) { }
    }
}