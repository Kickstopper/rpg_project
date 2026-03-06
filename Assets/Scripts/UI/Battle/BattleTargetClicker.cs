using UnityEngine;
using UnityEngine.EventSystems;
using Controller;

public class BattleTargetClicker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    private BattleEntity entity;
    private BattleManager battleManager;

    void Start()
    {
        entity = GetComponentInParent<BattleEntity>();
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ProcessHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ProcessClick();
    }

    private void OnMouseEnter()
    {
        // UI가 앞에 띄워져 있을 때는 뚫고 클릭되지 않도록 막음
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        ProcessHover();
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        ProcessClick();
    }

    private void ProcessHover()
    {
        if (battleManager != null && entity != null)
        {
            battleManager.OnTargetHovered(entity);
        }
    }

    private void ProcessClick()
    {
        if (battleManager != null && entity != null)
        {
            battleManager.OnTargetClicked(entity);
        }
    }
}