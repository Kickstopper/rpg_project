using UnityEngine;
using Manager;
using Controller;

public class SceneUIProvider : MonoBehaviour
{
    [Header("Scene UI References")]
    public GameObject explorationCanvas;
    public GameObject BattleCanvas;
    public GameObject menuCanvas;

    // 인스펙터에서 연결하거나 Start에서 찾음
    public BattleManager BattletManager; 

    void Start()
    {
        if (BattletManager == null)
            BattletManager = FindFirstObjectByType<BattleManager>();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.RegisterSceneComponents(
                explorationCanvas, 
                BattleCanvas, 
                menuCanvas, 
                BattletManager
            );
        }
    }
}