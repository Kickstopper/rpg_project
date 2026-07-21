using UnityEngine;

public enum VfxID
{
    None, Hit, Blow, Tackle, Cut, Slash, Stab, Claw, 
    Gun_Shot, Gun_Auto, 
    Fire, Ice, Elec, Force, 
    Heal, Revive, Buff, Debuff, 
    Poison, Curse, Paralyze, Silence, Guard, Reflect, Absorb, Sleep, Stone, Panic, Charm, Lullaby,
}
namespace UI.Battle
{
    public class BattleVisualController : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject vfxHitPrefab;  // 무기 없음. 기본 공격용
        public GameObject vfxBlowPrefab;
        public GameObject vfxTacklePrefab;
        public GameObject vfxCutPrefab;  // 베기
        public GameObject vfxSlashPrefab;  // 후려 베기
        public GameObject vfxStabPrefab;  // 찌르기
        public GameObject vfxClawPrefab;  // 할퀴기
        public GameObject vfxGunAutoPrefab;  // 총 연사
        public GameObject vfxGunShotPrefab;  // 샷건
        
        public GameObject vfxMagicFallbackPrefab;  // 마법 공격 fallback
        
        public GameObject vfxMagicFirePrefab;
        public GameObject vfxMagicIcePrefab;
        public GameObject vfxMagicElecPrefab;
        public GameObject vfxMagicForcePrefab;
        
        public GameObject vfxMagicHealPrefab;
        public GameObject vfxMagicRevivePrefab;
        public GameObject vfxMagicBuffPrefab;
        public GameObject vfxMagicDebuffPrefab;

        public GameObject vfxMagicPoisonPrefab;
        public GameObject vfxMagicCursePrefab;
        public GameObject vfxMagicSilencePrefab;
        public GameObject vfxMagicParalyzePrefab;
        public GameObject vfxMagicSleepPrefab;
        public GameObject vfxMagicLullabyPrefab;
        public GameObject vfxMagicCharmPrefab;
        public GameObject vfxMagicStonePrefab;
        public GameObject vfxMagicPanicPrefab;

        public GameObject vfxGuardHitPrefab;   // 방어 상태에서 맞았을 때
        public GameObject vfxReflectPrefab;    // 반사 발동 시
        public GameObject vfxAbsorbPrefab;     // 흡수 발동 시
        
        public Transform vfxContainer;

        public GameObject SpawnVFX(VfxID vfxID, Vector3 position)
        {
            GameObject vfx = null;
            switch(vfxID)
            {
                case VfxID.Hit: vfx = vfxHitPrefab; break;
                case VfxID.Blow: vfx = vfxBlowPrefab; break;
                case VfxID.Tackle: vfx = vfxTacklePrefab; break;

                case VfxID.Cut: vfx = vfxCutPrefab; break;
                case VfxID.Slash: vfx = vfxSlashPrefab; break;
                case VfxID.Stab: vfx = vfxStabPrefab; break;
                case VfxID.Claw: vfx = vfxClawPrefab; break;
                
                case VfxID.Gun_Shot: vfx = vfxGunShotPrefab; break;
                case VfxID.Gun_Auto: vfx = vfxGunAutoPrefab; break;
                
                case VfxID.Fire: vfx = vfxMagicFirePrefab; break;
                case VfxID.Ice: vfx = vfxMagicIcePrefab; break;
                case VfxID.Elec: vfx = vfxMagicElecPrefab; break;
                case VfxID.Force: vfx = vfxMagicForcePrefab; break;

                case VfxID.Heal: vfx = vfxMagicHealPrefab; break;
                case VfxID.Revive: vfx = vfxMagicRevivePrefab; break;
                
                case VfxID.Buff: vfx = vfxMagicBuffPrefab; break;
                case VfxID.Debuff: vfx = vfxMagicDebuffPrefab; break;

                case VfxID.Poison: vfx = vfxMagicPoisonPrefab; break;
                case VfxID.Curse: vfx = vfxMagicCursePrefab; break;
                case VfxID.Paralyze: vfx = vfxMagicParalyzePrefab; break;
                case VfxID.Silence: vfx = vfxMagicSilencePrefab; break;
                case VfxID.Sleep: vfx = vfxMagicSleepPrefab; break;
                case VfxID.Stone: vfx = vfxMagicStonePrefab; break;
                case VfxID.Panic: vfx = vfxMagicPanicPrefab; break;
                case VfxID.Charm: vfx = vfxMagicCharmPrefab; break;
                case VfxID.Lullaby: vfx = vfxMagicLullabyPrefab; break;
                
                case VfxID.Guard: vfx = vfxGuardHitPrefab; break;
                case VfxID.Reflect: vfx = vfxReflectPrefab; break;
                case VfxID.Absorb: vfx = vfxAbsorbPrefab; break;
                
                case VfxID.None:
                default:
                    vfx = vfxMagicFallbackPrefab;
                break;
            }

            if (vfx != null)
            {
                GameObject spawnedVfx = Instantiate(vfx, vfxContainer);
                
                spawnedVfx.transform.position = position;
                return spawnedVfx;
            }
            return null;
        }
    }

}
