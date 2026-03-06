using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
namespace UI
{
    public class VFXAutoDestroy : MonoBehaviour
    {
        private VisualEffect vfx;

        [Header("타이머 설정 (초)")]
        [Tooltip("create 이벤트 후 hit 이벤트까지의 대기 시간")]
        public float timeToHit = 0.5f; 
        
        [Tooltip("hit 이벤트 후 end(종료 연출) 이벤트까지의 대기 시간")]
        public float timeToEnd = 0.2f;

        [Tooltip("end 이벤트 후 파티클이 완전히 사라지고 삭제될 때까지의 대기 시간")]
        public float timeToDestroy = 1.0f; 

        private void Awake()
        {
            vfx = GetComponent<VisualEffect>();
        }

        private void Start()
        {
            if (vfx != null)
            {
                StartCoroutine(PlayVFXSequence());
            }
            else
            {
                Destroy(gameObject, 1f);
            }
        }

        private IEnumerator PlayVFXSequence()
        {
            // 생성 후 자동으로 루프 상태
            vfx.SendEvent("create");
            yield return new WaitForSeconds(timeToHit);

            // 피격
            vfx.SendEvent("hit");
            yield return new WaitForSeconds(timeToEnd);

            // 파티클이 사라지는 연출
            vfx.SendEvent("end");
            
            // 파티클이 화면에서 완전히 사라질 때까지 대기
            yield return new WaitForSeconds(timeToDestroy);

            vfx.SendEvent("stop");
            Destroy(gameObject);
        }
    }
}
