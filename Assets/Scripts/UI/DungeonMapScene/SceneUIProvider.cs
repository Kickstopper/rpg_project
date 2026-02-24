using UnityEngine;
using Manager;
using Controller;
using UI.Shop;

public class SceneUIProvider : MonoBehaviour
{
    [Header("Scene UI References")]
    public GameObject explorationCanvas;
    public GameObject BattleCanvas;
    public GameObject menuCanvas;
    public GameObject shopCanvas;

    // 인스펙터에서 연결하거나 Start에서 찾음
    public BattleManager BattletManager; 
    public ShopModeSelectUI shopUI;

    void Start()
    {
        if (BattletManager == null)
            BattletManager = FindFirstObjectByType<BattleManager>();

        if (shopUI == null)
            shopUI = FindFirstObjectByType<ShopModeSelectUI>();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.RegisterSceneComponents(
                explorationCanvas, 
                BattleCanvas, 
                menuCanvas,
                shopCanvas,
                BattletManager,
                shopUI
            );
        }
    }
}