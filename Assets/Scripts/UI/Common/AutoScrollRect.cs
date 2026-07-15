using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Common
{
    [RequireComponent(typeof(ScrollRect))]
    public class AutoScrollRect : MonoBehaviour
    {
        private ScrollRect scrollRect;
        private RectTransform contentPanel;
        private RectTransform viewport;

        private GameObject lastSelected;

        void Start()
        {
            scrollRect = GetComponent<ScrollRect>();
            contentPanel = scrollRect.content;
            viewport = scrollRect.viewport;
        }

        void Update()
        {
            // 현재 선택된 UI 오브젝트 가져오기
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            
            // 포커스가 없거나, 포커스가 변하지 않았다면 무시
            if (selected == null || selected == lastSelected) return;

            // 선택된 오브젝트가 이 Scroll View의 Content 안에 있는 자식인지 확인
            if (selected.transform.IsChildOf(contentPanel))
            {
                lastSelected = selected;

                Transform targetTransform = selected.transform;
                // 선택된 UI가 화살표 버튼 등 하위 자식일 경우, contentPanel의 본체가 나올 때까지 부모 계층을 타고 올라감. ScrollRect 떨림 방지.
                while (targetTransform.parent != null && targetTransform.parent != contentPanel)
                {
                    targetTransform = targetTransform.parent;
                }
                
                ScrollToSelected(targetTransform.GetComponent<RectTransform>());
            }
        }

        void ScrollToSelected(RectTransform target)
        {
            // 타겟 버튼의 로컬 Y 위치. 절댓값으로 위에서부터 얼마나 떨어져 있는지 계산
            float targetTop = Mathf.Abs(target.anchoredPosition.y) - (target.rect.height * (1f - target.pivot.y));
            float targetBottom = Mathf.Abs(target.anchoredPosition.y) + (target.rect.height * target.pivot.y);

            // 현재 화면에 보이는 뷰포트의 상단과 하단 위치
            float contentY = contentPanel.anchoredPosition.y; // 스크롤을 내릴수록 증가함
            float viewportHeight = viewport.rect.height;

            // 포커스된 버튼이 뷰포트 위로 넘어갔을 때 (위 방향키를 눌러서 올라갈 때)
            if (targetTop < contentY)
            {
                // 타겟의 상단 끝부분에 맞춰서 스크롤 올림
                contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, targetTop);
            }
            // 포커스된 버튼이 뷰포트 아래로 넘어갔을 때 (아래 방향키를 눌러서 내려갈 때)
            else if (targetBottom > contentY + viewportHeight)
            {
                // 타겟의 하단 끝부분이 뷰포트의 바닥에 딱 닿도록 스크롤을 내림
                contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, targetBottom - viewportHeight);
            }
        }
    }
}
