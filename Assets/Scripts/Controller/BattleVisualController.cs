using UnityEngine;

public enum VfxID
{
    None, Slash, Gun, Magic, Guard, Reflect, Absorb,
}
public class BattleVisualController : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject vfxSlashPrefab;  // 물리 공격용
    public GameObject vfxGunPrefab;  // 총 공격용
    public GameObject vfxMagicPrefab;  // 마법 공격용
    public GameObject vfxGuardHitPrefab;   // 방어 상태에서 맞았을 때
    public GameObject vfxReflectPrefab;    // 반사 발동 시
    public GameObject vfxAbsorbPrefab;     // 흡수 발동 시
    public Transform vfxContainer;
    public void SpawnVFX(VfxID vfxID, Vector3 position)
    {
        GameObject vfx = null;
        switch(vfxID)
        {
            case VfxID.Slash: vfx=vfxSlashPrefab; break;
            case VfxID.Gun: vfx=vfxGunPrefab; break;
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
            spawnedVfx.transform.position = new Vector3(position.x, position.y, -5f);
        }
    }
}
