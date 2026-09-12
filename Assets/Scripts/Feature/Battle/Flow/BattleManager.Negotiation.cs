using System.Linq;
using RPGProject.Core;
using RPGProject.Feature.Negotiation;
using UnityEngine;

namespace RPGProject.Feature.Battle
{
    public partial class BattleManager
    {
        private bool CanTradeNegotiationReward(NegotiationRewardKind kind)
        {
            var target = negotiationTarget;
            if (target == null || target.sourceData == null || !target.IsAlive || target.sourceData.isBoss ||
                negotiationActor == null || !negotiationActor.IsAlive || negotiationActor.sourceData == null ||
                !fieldController.activeMonsters.Contains(target)) return false;
            if (kind == NegotiationRewardKind.Recruit)
                return ManagerRoot.Party != null && ManagerRoot.Party.GetCharacterByID(target.sourceData.id) == null;
            if (negotiationItemRecipients.Contains(target)) return false;
            switch (kind)
            {
                case NegotiationRewardKind.Item:
                    return ManagerRoot.Database != null && ManagerRoot.Inventory != null && target.sourceData.dropItemIds != null &&
                        target.sourceData.dropItemIds.Any(id => !string.IsNullOrWhiteSpace(id) && ManagerRoot.Database.GetItem(id) != null &&
                            ManagerRoot.Inventory.GetItemCount(id) < int.MaxValue);
                case NegotiationRewardKind.Gold:
                    return ManagerRoot.Finance != null && ManagerRoot.Finance.CurrentMoney <= int.MaxValue - NegotiationTradeRules.GoldReward;
                case NegotiationRewardKind.HP:
                    return negotiationActor.currentHp < negotiationActor.maxHp && Mathf.FloorToInt(NegotiationTradeRules.HpReward *
                        negotiationActor.StatusEffects.Multiplier(d => d.healingReceivedMultiplier)) > 0;
                case NegotiationRewardKind.MP:
                    return negotiationActor.currentMp < negotiationActor.maxMp;
                default: return false;
            }
        }

        private bool TryGiveNegotiationReward(NegotiationRewardKind kind)
        {
            if (!CanTradeNegotiationReward(kind)) return false;
            if (kind == NegotiationRewardKind.Item) return TryGiveNegotiationItem();
            bool success;
            switch (kind)
            {
                case NegotiationRewardKind.Gold:
                    negotiationItemRecipients.Add(negotiationTarget);
                    negotiationExitPending = true;
                    ManagerRoot.Finance.AddMoney(NegotiationTradeRules.GoldReward);
                    return true;
                case NegotiationRewardKind.HP:
                    success = negotiationActor.TryRestoreNegotiationResource(true, NegotiationTradeRules.HpReward); break;
                case NegotiationRewardKind.MP:
                    success = negotiationActor.TryRestoreNegotiationResource(false, NegotiationTradeRules.MpReward); break;
                default: return false;
            }
            if (success)
            {
                negotiationItemRecipients.Add(negotiationTarget);
                negotiationExitPending = true;
            }
            return success;
        }

        private bool TryFleeNegotiationTarget()
        {
            var target = negotiationTarget;
            if (target == null || target.sourceData == null || !target.IsAlive ||
                !fieldController.activeMonsters.Contains(target)) return false;
            fieldController.activeMonsters.Remove(target);
            fieldController.encounterLog.Remove(target.sourceData);
            target.SetSelectionState(false);
            target.gameObject.SetActive(false);
            target.transform.SetParent(transform, false);
            return true;
        }
    }
}
