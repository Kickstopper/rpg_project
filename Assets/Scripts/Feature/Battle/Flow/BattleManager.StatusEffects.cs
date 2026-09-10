using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RPGProject.Core;
using RPGProject.Feature.StatusEffects;
using RPGProject.Utilities;
using UnityEngine;

namespace RPGProject.Feature.Battle
{
    public partial class BattleManager
    {
        private Dictionary<BattleEntity, List<ActiveEffect>> pendingStatusOpportunities;

        IEnumerator PerformStatusAwareAction(BattleAction action)
        {
            var actor = action.actor != null ? action.actor.GetComponent<BattleEntity>() : null;
            if (actor == null || actor.currentHp <= 0) yield break;
            bool joint = action.type == ActionType.Union_Attack || action.type == ActionType.Rolling_Vulcan;
            var opportunities = new Dictionary<BattleEntity, List<ActiveEffect>> {
                [actor] = actor.StatusEffects.Snapshot()
            };
            if (joint)
                foreach (var partner in currentUnionParticipants)
                {
                    if (partner == null || partner.currentHp <= 0 || opportunities.ContainsKey(partner)) continue;
                    opportunities.Add(partner, partner.StatusEffects.Snapshot());
                    actionQueue.RemoveAll(a => a.actor == partner.gameObject &&
                        (a.type == ActionType.Next || a.type == ActionType.Guard));
                }
            pendingStatusOpportunities = opportunities;
            var restriction = actor.CheckActionRestriction();
            if (restriction == RestrictionType.SkipTurn ||
                (restriction == RestrictionType.Silence && action.type == ActionType.Skill))
            {
                string reason = restriction == RestrictionType.SkipTurn ? "행동 불가" : "침묵: 스킬 사용 불가";
                uiController.ShowLog($"{actor.entityName}: {reason}");
                yield return YieldCache.WaitForSeconds(0.8f);
            }
            else if (restriction == RestrictionType.Charm || restriction == RestrictionType.Panic)
            {
                var candidates = fieldController.activePlayers.Cast<BattleEntity>()
                    .Concat(fieldController.activeMonsters).Where(e => e != null && e != actor && e.currentHp > 0);
                if (restriction == RestrictionType.Charm)
                    candidates = candidates.Where(e => (e is PlayerController) == (actor is PlayerController));
                var targets = candidates.ToList();
                uiController.ShowLog($"{actor.entityName}: {(restriction == RestrictionType.Charm ? "매료" : "혼란")}");
                if (targets.Count == 0 || (restriction == RestrictionType.Panic && Random.value < 0.5f))
                    yield return YieldCache.WaitForSeconds(0.8f);
                else
                {
                    var forced = new BattleAction(actor.gameObject, targets[Random.Range(0, targets.Count)].gameObject,
                        ActionType.Attack, action.speed);
                    yield return ProcessSingleHit(forced, forced.target);
                }
            }
            else yield return PerformAction(action);

            yield return CompletePendingStatusOpportunities();
            if (joint) currentUnionParticipants.Clear();
            uiController.HideLog();
        }

        IEnumerator CompletePendingStatusOpportunities()
        {
            var pending = pendingStatusOpportunities;
            if (pending == null) yield break;
            foreach (var participant in pending.Keys.ToArray())
            {
                var snapshot = pending[participant];
                pending.Remove(participant);
                if (participant == null || participant.currentHp <= 0 || CheckBattleEnd(out _)) continue;
                int damage = participant.StatusEffects.CompleteAction(snapshot, participant.maxHp, () => Random.value);
                if (damage > 0)
                {
                    uiController.ShowLog($"{participant.entityName}: 상태이상 피해 {damage}");
                    yield return ApplyDamage(participant.gameObject, damage, false, true);
                }
            }
            if (ReferenceEquals(pendingStatusOpportunities, pending)) pendingStatusOpportunities = null;
            SyncStatusVitals();
        }

        void SyncStatusVitals()
        {
            foreach (var player in fieldController.GetPlayerControllers())
            {
                if (player == null || player.IsEmpty || player.sourceData == null) continue;
                player.sourceData.currentHp = player.currentHp;
                player.sourceData.currentMp = player.currentMp;
            }
        }

        void FinalizeBattleStatusEffects()
        {
            pendingStatusOpportunities = null;
            SyncStatusVitals();
            foreach (var player in fieldController.GetPlayerControllers())
                if (player != null && !player.IsEmpty) player.ClearBattleOnlyEffects();
                
            if (ManagerRoot.Party != null)
                foreach (var member in ManagerRoot.Party.partyData)
                    member?.StatusEffects.ClearBattleOnly();
        }
    }
}
