using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace UI
{
    public class ContinuousButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Events")]
        public UnityEvent onPointerDown;
        public UnityEvent onPointerUp;
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (onPointerDown != null) onPointerDown.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (onPointerUp != null) onPointerUp.Invoke();
        }
    }
}