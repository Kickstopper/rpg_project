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
    public GameObject SpawnVFX(VfxID vfxID, Vector3 position)
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
            
            // 슬롯의 완벽한 월드 좌표(회전, 깊이 포함)로 우선 일치시킴
            spawnedVfx.transform.position = position;
            
            // 부모(vfxContainer 혹은 Canvas)가 기울어진 각도를 기준으로,
            // Z축(카메라를 바라보는 방향)으로 살짝 당겨서 파티클이 UI 위에 오도록 만듦.
            Vector3 localPos = spawnedVfx.transform.localPosition;
            localPos.z -= 10f;
            spawnedVfx.transform.localPosition = localPos;
            return spawnedVfx;
        }
        return null;
    }
}
