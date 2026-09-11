using System.Collections.Generic;
using System.Linq;
using RPGProject.Core;
using RPGProject.Feature.Negotiation;
using RPGProject.Feature.Skills;
using RPGProject.Shared.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGProject.Feature.Battle
{
    public partial class BattleManager
    {
        private sealed class KinshipOffer
        {
            public KinshipGift Gift;
            public int Gold;
            public string ItemID;
            public List<Dictionary<string, string>> Lines;
        }

        // Prepare presentation only. The gift is committed after its offer line is acknowledged.
        private KinshipOffer CreateKinshipOffer(MonsterController target)
        {
            var data = target.sourceData;
            var gifts = new List<KinshipGift> { KinshipGift.Farewell };
            if (ManagerRoot.Finance != null && ManagerRoot.Finance.CurrentMoney < int.MaxValue)
                gifts.Add(KinshipGift.Gold);
            var drops = new List<string>();
            if (ManagerRoot.Database != null && ManagerRoot.Inventory != null && data.dropItemIds != null)
                drops = data.dropItemIds.Where(id => !string.IsNullOrWhiteSpace(id) &&
                    ManagerRoot.Database.GetItem(id) != null && ManagerRoot.Inventory.GetItemCount(id) < int.MaxValue).Distinct().ToList();
            if (drops.Count > 0) gifts.Add(KinshipGift.Item);
            FindKinshipRecovery(target, out var recoveryTarget, out _);
            if (recoveryTarget != null) gifts.Add(KinshipGift.Recovery);
            // Equal probability among usable categories, not among all skills/items.
            var offer = new KinshipOffer
            {
                Gift = gifts[Random.Range(0, gifts.Count)],
                Lines = NegotiationKinshipRules.CreateDialogues(data)
            };
            if (offer.Gift == KinshipGift.Gold) offer.Gold = NegotiationKinshipRules.GoldAmount(data.stats.level, Random.value);
            if (offer.Gift == KinshipGift.Item) offer.ItemID = drops[Random.Range(0, drops.Count)];
            offer.Lines.Find(row => row["Seq"] == "KIN_OFFER")["Text"] = NegotiationKinshipRules.GiftLine(data.personality, offer.Gift);
            return offer;
        }

        private string ResolveKinshipNegotiation(KinshipOffer offer)
        {
            var target = negotiationTarget;
            if (target == null || target.sourceData == null || !target.IsAlive ||
                !fieldController.activeMonsters.Contains(target) ||
                !NegotiationKinshipRules.HasCompanion(target.sourceData, ManagerRoot.Party?.partyData)) return "FAIL";
            string receipt = "";
            switch (offer.Gift)
            {
                case KinshipGift.Farewell: return "END";
                case KinshipGift.Gold:
                    if (ManagerRoot.Finance == null) break;
                    // Debt is valid in this game. Subtract in long to avoid overflow for negative balances.
                    int amount = (int)System.Math.Min(offer.Gold, (long)int.MaxValue - ManagerRoot.Finance.CurrentMoney);
                    if (amount <= 0) break;
                    ManagerRoot.Finance.AddMoney(amount);
                    receipt = $"돈 {amount:N0}을 받았다.";
                    break;
                case KinshipGift.Item:
                    if (ManagerRoot.Database == null || ManagerRoot.Inventory == null ||
                        string.IsNullOrWhiteSpace(offer.ItemID) || ManagerRoot.Inventory.GetItemCount(offer.ItemID) == int.MaxValue ||
                        target.sourceData.dropItemIds == null || !target.sourceData.dropItemIds.Contains(offer.ItemID)) break;
                    var item = ManagerRoot.Database.GetItem(offer.ItemID);
                    if (item == null) break;
                    string itemName = string.IsNullOrWhiteSpace(item.dataName) ? offer.ItemID : item.dataName;
                    ManagerRoot.Inventory.AddItem(offer.ItemID, 1);
                    receipt = $"{itemName} 1개를 받았다.";
                    break;
                case KinshipGift.Recovery:
                    // Re-evaluate the lowest ratio after the offer, in case another system changed vitals.
                    FindKinshipRecovery(target, out var recoveryTarget, out var recoverySkill);
                    if (recoveryTarget == null) break;
                    bool hp = recoverySkill.effectType == EffectType.Recover_HP;
                    int before = hp ? recoveryTarget.currentHp : recoveryTarget.currentMp;
                    if (!recoveryTarget.TryRestoreNegotiationResource(hp, recoverySkill.effectValue)) break;
                    int restored = (hp ? recoveryTarget.currentHp : recoveryTarget.currentMp) - before;
                    string skillName = string.IsNullOrWhiteSpace(recoverySkill.dataName) ? "회복 마법" : recoverySkill.dataName;
                    receipt = $"{skillName}: {recoveryTarget.sourceData.name}의 {(hp ? "HP" : "MP")}가 {restored} 회복되었다.";
                    break;
            }
            var result = offer.Lines.Find(row => row["Seq"] == "KIN_RESULT");
            result["Name"] = receipt.Length == 0 ? target.sourceData.name : "";
            result["Text"] = receipt.Length == 0 ? "지금은 도와줄 수 없겠군. 우리는 이만 물러나겠다." : receipt;
            return "KIN_RESULT";
        }

        private void FindKinshipRecovery(MonsterController monster, out PlayerController target, out SkillData skill)
        {
            target = null; skill = null;
            if (monster == null || monster.sourceData?.skills == null) return;
            var players = fieldController.GetPlayerControllers();
            // Check HP first so an exact HP/MP ratio tie has a stable outcome.
            foreach (var effect in new[] { EffectType.Recover_HP, EffectType.Recover_MP })
            {
                var strongest = monster.sourceData.skills.Where(s => s != null && s.effectType == effect && s.effectValue > 0)
                    .OrderByDescending(s => s.effectValue).FirstOrDefault();
                var candidate = NegotiationKinshipRules.FindRecoveryTarget(players, strongest);
                if (candidate == null) continue;
                int current = effect == EffectType.Recover_HP ? candidate.currentHp : candidate.currentMp;
                int max = effect == EffectType.Recover_HP ? candidate.maxHp : candidate.maxMp;
                if (target != null)
                {
                    int oldCurrent = skill.effectType == EffectType.Recover_HP ? target.currentHp : target.currentMp;
                    int oldMax = skill.effectType == EffectType.Recover_HP ? target.maxHp : target.maxMp;
                    if ((long)current * oldMax >= (long)oldCurrent * max) continue;
                }
                target = candidate; skill = strongest;
            }
        }

        // A peaceful exit has no enemy phase, victory rewards or result screen.
        private void EndBattleByNegotiation()
        {
            if (isEndingBattle) return;
            isEndingBattle = true;
            state = BattleState.Won;
            isSelectingTarget = false;
            actionQueue.Clear();
            currentProcessingAction = null;
            currentActingEntity = null;
            currentUnionParticipants.Clear();
            EventSystem.current?.SetSelectedGameObject(null);
            fieldController.StopBlinkEffects();
            fieldController.HideTurnOrderUI();
            uiController.SetTargetCursorVisible(false);
            uiController.SetCmdPanelVisible(false);
            uiController.SetBreakSliderVisible(false);
            uiController.HideStateMessage();
            uiController.HideMessage();
            uiController.HideLog();
            fieldController.SyncPositionsToPartyManager();
            FinalizeBattleStatusEffects();
            fieldController.SetPartyVisible(true);
            foreach (var player in fieldController.GetPlayerControllers())
                if (player != null) player.RefreshView();
            fieldController.validTargets.Clear();
            // Process only real recorded kills and recruitment flags. Surviving enemies are not kills.
            GetCompletedQuests();
            fieldController.ClearMonsterField();
            fieldController.encounterLog.Clear();
            uiController.ShowBattleEndAnimation(() => ManagerRoot.GameState.ChangeState(GameState.Exploration));
        }
    }
}
