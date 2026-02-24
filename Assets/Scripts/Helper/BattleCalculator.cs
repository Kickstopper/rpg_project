using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Data;
using Controller;
using UI;
using UI.DungeonMapScene;

namespace Helper
{
    public static class CombatCalculator
    {
        // -------------------------------------------------------
        // [데미지 공식]
        // -------------------------------------------------------
        public static int CalculateDamage(BattleEntity attacker, BattleEntity defender, CombatAction action, bool isCritical, float damageMultiplier)
        {
            if (attacker == null || defender == null) return 0;

            // 기본 공격력
            int baseAtk = attacker.GetTotalStr();
            int skillPower = 0;
            if (action.type == ActionType.Skill || action.type == ActionType.Item)
            {
                if (action.itemData != null) skillPower = action.itemData.effectValue;
            }

            int totalAtk = baseAtk + skillPower;
            
            // 방어력 및 저항
            int totalDef = defender.GetDefense();
            float resistanceValue = GetResistanceValue(action.skillData, defender.GetResistances());
            float resistanceMultiplier = 1.0f - resistanceValue;

            // 기초 데미지 계산
            float rawDamage = Mathf.Max(1, totalAtk - (totalDef * 0.5f));

            // 성향(Alignment) 상성 보정
            float alignBonus = AlignmentSystem.GetDamageModifier(attacker.align, defender.align);
            
            // 최종 연산
            rawDamage *= damageMultiplier * alignBonus;
            float randomVar = Random.Range(0.9f, 1.1f); // 10% 랜덤 분산
            
            int finalDamage = Mathf.RoundToInt(rawDamage * resistanceMultiplier * randomVar);

            // 6. 크리티컬 및 방어 상태 적용
            if (isCritical) finalDamage *= 2;
            if (defender.isGuarding) finalDamage = Mathf.FloorToInt(finalDamage * 0.5f);
            
            return Mathf.Max(1, finalDamage);
        }

        public static int CalculateGunDamage(PlayerController attacker, BattleEntity defender, bool isCritical)
        {
            int baseAtk = attacker.GetGunAttack();
            int def = defender.GetTotalVit(); // 몬스터의 경우 VIT를 방어력으로 임시 사용
            
            float rawDmg = Mathf.Max(1, baseAtk - (def * 0.5f));
            if (isCritical) rawDmg *= 1.5f;

            return Mathf.RoundToInt(rawDmg);
        }

        // -------------------------------------------------------
        // [확률 체크: 명중/회피/크리티컬]
        // -------------------------------------------------------
        public static bool CheckEvasion(BattleEntity attacker, BattleEntity defender, float positionalEvasionBonus)
        {
            if (attacker == null || defender == null) return false;

            int attackerAgi = attacker.GetTotalAgi();
            int attackerLuc = attacker.GetTotalLuc();
            int defenderAgi = defender.GetTotalAgi();
            int defenderLuc = defender.GetTotalLuc();

            float baseEvasionChance = 0.05f;
            float agiBonus = Mathf.Clamp((defenderAgi - attackerAgi) * 0.01f, -0.2f, 0.2f);
            float lucBonus = Mathf.Clamp((defenderLuc - attackerLuc) * 0.005f, -0.1f, 0.1f);
            
            float totalChance = Mathf.Clamp(baseEvasionChance + agiBonus + lucBonus + positionalEvasionBonus, 0f, 0.9f);

            return Random.value < totalChance;
        }

        public static bool CheckCritical(BattleEntity attacker, BattleEntity defender, CombatAction action)
        {
            if (attacker == null || defender == null) return false;

            bool isMagic = (action.skillData != null && action.skillData.element != ElementType.Physical);
            
            int atkLuc = attacker.GetTotalLuc();
            int atkMainStat = isMagic ? attacker.GetMagicAttack() : attacker.GetAttack();
            
            int defLuc = defender.GetTotalLuc();
            int defAgi = defender.GetTotalAgi();

            float baseCritChance = 0.05f;
            float lucBonus = (atkLuc - defLuc) * 0.002f;
            float statBonus = (atkMainStat - defAgi) * 0.001f;

            float totalChance = Mathf.Clamp(baseCritChance + lucBonus + statBonus, 0f, 0.7f);

            return Random.value < totalChance;
        }

        // -------------------------------------------------------
        // [특성 체크: 반사/흡수/저항/상성]
        // -------------------------------------------------------
        public static bool CheckReflection(BattleEntity target, ActionType type)
        {
            bool isPhysical = (type == ActionType.Attack || type == ActionType.Shoot);
            bool isMagic = (type == ActionType.Skill);

            if (isPhysical && target.isPhysicalReflect) return true;
            if (isMagic && target.isMagicReflect) return true;
            return false;
        }

        public static bool CheckAbsorption(BattleEntity target, ActionType type)
        {
            bool isPhysical = (type == ActionType.Attack || type == ActionType.Shoot);
            bool isMagic = (type == ActionType.Skill);

            if (isPhysical && target.isPhysicalAbsorb) return true;
            if (isMagic && target.isMagicAbsorb) return true;
            return false;
        }

        public static float GetResistanceValue(BaseRootData data, ResistanceData resist)
        {
            if (data == null) return resist.phys;
            switch (data.element)
            {
                case ElementType.Fire: return resist.fire;
                case ElementType.Ice: return resist.ice;
                case ElementType.Elec: return resist.elec;
                case ElementType.Force: return resist.force;
                case ElementType.Psyche: return resist.psyche;
                default: return resist.phys;
            }
        }

        public static bool IsAlignCompatible(Align a, Align b)
        {
            return a == b || a == Align.True_Neutral || b == Align.True_Neutral;
        }

        // -------------------------------------------------------
        // [위치 보정]
        // -------------------------------------------------------
        public static void GetPositionalModifiers(BattleFieldController.BattlePosition atkPos, BattleFieldController.BattlePosition defPos, WeaponType wType, out float damageMultiplier, out float evasionBonus)
        {
            damageMultiplier = 1.0f;
            evasionBonus = 0f;

            if (wType == WeaponType.Melee)
            {
                if (!atkPos.isFrontRow) damageMultiplier *= 0.7f; // 후열 공격 페널티
                if (!defPos.isFrontRow) // 후열 방어 보너스
                {
                    damageMultiplier *= 0.8f;
                    evasionBonus += 0.1f;
                }
            }

            int colDiff = Mathf.Abs(atkPos.columnIndex - defPos.columnIndex);
            if (colDiff == 1) damageMultiplier *= 0.95f;
            else if (colDiff >= 2)
            {
                damageMultiplier *= 0.90f;
                evasionBonus += 0.05f;
            }
        }

        // -------------------------------------------------------
        // [도망 및 보상]
        // -------------------------------------------------------
        public static bool CalculateEscapeSuccess(List<BattleEntity> players, List<BattleEntity> monsters, int attempts, int guaranteedAttempts)
        {
            if (attempts >= guaranteedAttempts) return true;
            if (players.Count == 0) return false;
            if (monsters.Count == 0) return true;

            float playerAvgAgi = (float)players.Average(p => p.GetTotalAgi());
            float playerAvgLuc = (float)players.Average(p => p.GetTotalLuc());
            
            float enemyAvgAgi = (float)monsters.Average(m => m.GetTotalAgi());
            float enemyAvgLuc = (float)monsters.Average(m => m.GetTotalLuc());

            float baseChance = 50f;
            float agiBonus = (playerAvgAgi - enemyAvgAgi) * 2.0f;
            float lucBonus = (playerAvgLuc - enemyAvgLuc) * 1.0f;
            
            float finalChance = Mathf.Clamp(baseChance + agiBonus + lucBonus, 10f, 100f);

            return Random.Range(0f, 100f) < finalChance;
        }

        public static BattleManager.BattleReward CalculateRewards(List<PlayerController> players, List<MonsterDatabase.MonsterEntry> encounterLog)
        {
            BattleManager.BattleReward reward = new BattleManager.BattleReward();
            reward.dropItems = new List<string>();

            long totalMonsterExp = 0;
            int calculatedMoney = 0;

            foreach (var entry in encounterLog)
            {
                totalMonsterExp += GetMaxExpForLevel(entry.stats.level);
                
                // 골드
                if (Random.value >= 0.7f) // 30% 확률
                {
                    int lv = entry.stats.level;
                    calculatedMoney += Random.Range(lv * 10, lv * 30);
                }

                // 드롭 아이템
                if (entry.dropItemIds != null && entry.dropItemIds.Count > 0 && Random.value >= 0.6f) // 40% 확률
                {
                    reward.dropItems.Add(entry.dropItemIds[Random.Range(0, entry.dropItemIds.Count)]);
                }
            }

            // 레벨 보정
            float partyAvgLv = (players.Count > 0) ? (float)players.Average(p => p.level) : 1;
            float monsterAvgLv = (encounterLog.Count > 0) ? (float)encounterLog.Average(m => m.stats.level) : 1;
            float levelBonusRatio = Mathf.Clamp(monsterAvgLv / partyAvgLv, 0.5f, 1.5f);

            reward.totalExp = Mathf.FloorToInt(totalMonsterExp * levelBonusRatio);
            
            int livingCount = players.Count(p => p.currentHp > 0);
            reward.expPerMember = (livingCount > 0) ? reward.totalExp / livingCount : 0;
            reward.totalMoney = calculatedMoney;

            return reward;
        }

        // 몬스터 경험치 계산 헬퍼 함수
        public static int GetMaxExpForLevel(int level)
        {
            float exponent = 2.2f; // LevelSystem과 동일하게 맞춤
            float baseExp = 15f;   // 1레벨 몬스터가 주는 경험치 (플레이어 1->2 필요 경험치가 100이라면 약 15% 정도)

            // 공식: 15 * (Level ^ 2.2)
            // Lv 1 = 15
            // Lv 10 = 2,377
            // Lv 50 = 82,382 (플레이어 요구량의 약 15% 유지)
            return Mathf.FloorToInt(baseExp * Mathf.Pow(level, exponent));
        }

    }
}