using UnityEngine;
namespace UI.WorldMapScene
{
    [ExecuteInEditMode]
    public class GlobalShaderController : MonoBehaviour
    {
        public GameObject visual;
        void Update()
        {
            // _PlayerPosition이라는 이름으로 현재 플레이어의 위치를 알림
            Shader.SetGlobalVector("_PlayerPosition", new Vector3(transform.position.x, visual.transform.position.y, transform.position.z));
        }
    }
}
