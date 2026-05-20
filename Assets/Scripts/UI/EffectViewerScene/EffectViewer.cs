using UnityEngine;
using TMPro;
namespace UI.EffectViewerScene
{
    public class EffectViewer : MonoBehaviour
    {
        [Header("Viewer Settings")]
        [Tooltip("검토할 프리팹")]
        public GameObject[] effectPrefabs;
        
        [Tooltip("이펙트가 생성될 UI 부모 (예: Canvas 내의 빈 패널)")]
        public Transform canvasParent;
        
        [Tooltip("프리팹 파일명을 표시할 UI Text")]
        public TextMeshProUGUI nameText;

        private int currentIndex = 0;
        private GameObject currentInstance;

        void Start()
        {
            if (effectPrefabs.Length > 0)
            {
                ShowEffect(0);
            }
            else
            {
                Debug.LogWarning("[EffectViewer] 등록된 프리팹이 없습니다.");
            }
        }

        void Update()
        {
            if (effectPrefabs.Length == 0) return;

            // 이전 프리팹 보기 (왼쪽 방향키 또는 A)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                currentIndex--;
                if (currentIndex < 0) currentIndex = effectPrefabs.Length - 1; // 배열 끝으로 순환
                ShowEffect(currentIndex);
            }
            // 다음 프리팹 보기 (오른쪽 방향키 또는 D)
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                currentIndex++;
                if (currentIndex >= effectPrefabs.Length) currentIndex = 0; // 배열 처음으로 순환
                ShowEffect(currentIndex);
            }

            // 반복 재생 로직
            if (currentInstance == null)
            {
                SpawnCurrentEffect();
            }
        }

        private void ShowEffect(int index)
        {
            // 기존에 재생 중이던 이펙트가 남아있다면 강제 파괴
            if (currentInstance != null)
            {
                Destroy(currentInstance);
            }

            currentIndex = index;
            
            // 화면 상단에 인덱스와 프리팹 이름 표시
            if (nameText != null)
            {
                nameText.text = $"[{currentIndex + 1} / {effectPrefabs.Length}] {effectPrefabs[currentIndex].name}";
            }

            SpawnCurrentEffect();
        }

        private void SpawnCurrentEffect()
        {
            // 프리팹 생성 및 부모 지정
            currentInstance = Instantiate(effectPrefabs[currentIndex], canvasParent);
            
            // UI 중앙에 오도록 로컬 위치 초기화
            currentInstance.transform.localPosition = Vector3.zero;
            currentInstance.transform.localScale = Vector3.one * 4f;
        }
    }
}
