using UnityEngine;
namespace Controller
{
    [ExecuteInEditMode] // 에디터에서도 실시간으로 보이게 함
    public class GlobalShaderController : MonoBehaviour
    {
        public GameObject visual;
        void Update()
        {
            // "_PlayerPosition"이라는 이름으로 현재 내 위치를 전 세계(Shader)에 방송한다.
            Shader.SetGlobalVector("_PlayerPosition", new Vector3(transform.position.x, visual.transform.position.y, transform.position.z));
        }
    }
}
