using UnityEngine;
using Manager;
using Controller; // CombatController 참조

public class SceneUIProvider : MonoBehaviour
{
    [Header("Scene UI References")]
    public GameObject explorationCanvas;
    public GameObject combatCanvas; // 이게 BattleSystem 프리팹의 캔버스
    public GameObject menuCanvas;

    // 인스펙터에서 연결하거나 Start에서 찾음
    public CombatController combatController; 

    void Start()
    {
        // 만약 인스펙터 연결을 깜빡했다면 자동으로 찾기
        if (combatController == null)
            combatController = FindFirstObjectByType<CombatController>();

        // 캔버스 카메라 연결 로직 (이전 답변 참조) ...

        if (GameStateManager.Instance != null)
        {
            // 매니저에게 컨트롤러까지 함께 등록
            GameStateManager.Instance.RegisterSceneComponents(
                explorationCanvas, 
                combatCanvas, 
                menuCanvas, 
                combatController
            );
        }
    }
}