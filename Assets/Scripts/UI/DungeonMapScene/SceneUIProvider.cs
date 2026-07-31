using UnityEngine;
using Manager;
using Controller;
using UI.Shop;
using UI;
using UI.Battle;

public class SceneUIProvider : MonoBehaviour
{
    [Header("Scene UI References")]
    public GameObject eventCanvas;
    public GameObject explorationCanvas;
    public GameObject BattleCanvas;
    public GameObject menuCanvas;
    public GameObject shopCanvas;
    public GameObject elevatorCanvas;
    public GameObject terminalCanvas;
    public GameObject officeCanvas;
    public GameObject fieldMapUI;

    // 인스펙터에서 연결하거나 Start에서 찾음
    public BattleManager BattletManager; 
    public ShopModeSelectUI shopUI;
    public DialogueUI dialogueUI;
    void Start()
    {
        if (BattletManager == null)
            BattletManager = FindFirstObjectByType<BattleManager>();

        if (shopUI == null)
            shopUI = FindFirstObjectByType<ShopModeSelectUI>();
        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (ManagerRoot.GameState != null)
        {
            ManagerRoot.GameState.RegisterSceneComponents(
                explorationCanvas,
                eventCanvas, 
                dialogueUI,
                menuCanvas,
                BattleCanvas,
                BattletManager,
                shopCanvas,
                shopUI,
                terminalCanvas,
                elevatorCanvas,
                officeCanvas,
                fieldMapUI
            );
        }
    }
}