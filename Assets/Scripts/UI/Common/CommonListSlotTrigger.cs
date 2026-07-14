using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace UI.Common
{
    // EventTrigger를 대체할 커스텀 트리거
    public class CommonListSlotTrigger : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Action onSelectAction;

        // 마우스가 올라갔을 때
        public void OnPointerEnter(PointerEventData eventData)
        {
            onSelectAction?.Invoke();
        }

        // 방향키로 선택(포커스)되었을 때
        public void OnSelect(BaseEventData eventData)
        {
            onSelectAction?.Invoke();
        }
    }
}