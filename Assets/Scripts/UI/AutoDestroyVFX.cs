using UnityEngine;
namespace UI
{
    public class AutoDestroyVFX : MonoBehaviour
    {
        private ParticleSystem ps;

        void Start()
        {
            ps = GetComponent<ParticleSystem>();
        }

        void Update()
        {
            if (ps != null && !ps.IsAlive())
            {
                Destroy(gameObject);
            }
        }
    }
}
