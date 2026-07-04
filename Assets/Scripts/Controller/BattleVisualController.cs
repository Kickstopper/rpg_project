using UnityEngine;

public enum VfxID
{
    None, Hit, Cut, Slash, Stab, Claw, Gun_Shot, Gun_Auto, Magic, Guard, Reflect, Absorb,
}

public class BattleVisualController : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject vfxHitPrefab;  // 무기 없음. 기본 공격용
    public GameObject vfxCutPrefab;  // 베기
    public GameObject vfxSlashPrefab;  // 후려 베기
    public GameObject vfxStabPrefab;  // 찌르기
    public GameObject vfxClawbPrefab;  // 할퀴기
    public GameObject vfxGunAutoPrefab;  // 총 연사
    public GameObject vfxGunShotPrefab;  // 샷건
    public GameObject vfxMagicPrefab;  // 마법 공격용
    public GameObject vfxGuardHitPrefab;   // 방어 상태에서 맞았을 때
    public GameObject vfxReflectPrefab;    // 반사 발동 시
    public GameObject vfxAbsorbPrefab;     // 흡수 발동 시
    public Transform vfxContainer;
    public GameObject SpawnVFX(VfxID vfxID, Vector3 position)
    {
        GameObject vfx = null;
        switch(vfxID)
        {
            case VfxID.Hit: vfx=vfxSlashPrefab; break;
            case VfxID.Cut: vfx=vfxSlashPrefab; break;
            case VfxID.Slash: vfx=vfxSlashPrefab; break;
            case VfxID.Stab: vfx=vfxSlashPrefab; break;
            case VfxID.Claw: vfx=vfxSlashPrefab; break;

            case VfxID.Gun_Auto: vfx=vfxGunAutoPrefab; break;
            case VfxID.Gun_Shot: vfx=vfxGunShotPrefab; break;
            
            case VfxID.Magic: vfx=vfxMagicPrefab; break;
            case VfxID.Reflect: vfx=vfxReflectPrefab; break;
            case VfxID.Absorb: vfx=vfxAbsorbPrefab; break;
            
            case VfxID.Guard: vfx=vfxGuardHitPrefab; break;
            
            case VfxID.None:
            default:
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
