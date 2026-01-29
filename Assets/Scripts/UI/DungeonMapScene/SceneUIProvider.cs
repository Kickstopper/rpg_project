using UnityEngine;
using Manager;

public class SceneUIProvider : MonoBehaviour
{
    [Header("Scene UI References")]
    public GameObject explorationCanvas;
    public GameObject combatCanvas;
    public GameObject menuCanvas;

    void Start()
    {
        // 씬이 시작되면 싱글톤 매니저에게 내 UI들을 등록함
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.RegisterSceneUI(explorationCanvas, combatCanvas, menuCanvas);
        }
        else
        {
            Debug.LogError("GameStateManager가 존재하지 않습니다!");
        }
    }
}