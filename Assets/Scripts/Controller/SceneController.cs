using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Manager;
using Data;
namespace Controller 
{
    public static class GameScene
    {
        public const string TITLE_SCENE = "TitleScene";
        public const string WORLD_MAP_SCENE = "WorldMapScene";
        public const string DUNGEON_MAP_SCENE = "DungeonMapScene";
    }

    public class SceneController : MonoBehaviour
    {
        
        [SerializeField] private GameObject confirmWindow; // 확인 팝업 연결
        
        [Header("UI Settings")]
        public GameObject UI_Canvas;
        public Image targetImage; // 이미지가 표시될 UI Image 컴포넌트
        public SaveLoadUIController saveLoadUI;
        public List<Button> allMenuBtns;
        public Button btnContinue; // 이어하기 버튼
        public Button btnLoad;     // 불러오기 버튼 (슬롯 선택)
        public Button btnNewGame;  // 새 게임

        private int currentBtnIndex = 0;
        
        [Header("Slide Settings")]
        public List<Sprite> backgroundImages; // 배경 이미지 목록
        public float fadeDuration = 1.5f; // 페이드 인/아웃 걸리는 시간
        public float displayDuration = 3.0f; // 이미지가 완전히 보이는 시간

        private bool isEnable = false;     // 팝업이 열려있는지 확인
        public bool IsEnable => isEnable;

        void Start()
        {
            if (UI_Canvas) UI_Canvas.SetActive(false);
        }


        public void ShowAnimation()
        {
            if (targetImage != null && backgroundImages.Count > 0)
            {
                StartCoroutine(SlideshowRoutine());
            }
            else
            {
                Debug.LogError("이미지 혹은 스프라이트 리스트가 비어있습니다.");
            }
        }

        private void CheckSuspendSaveData()
        {
            bool hasSuspend = SaveManager.Instance.HasSuspendData();

            // 중단 데이터가 있으면 '이어하기' 표시
            if (!hasSuspend)
            {
                btnContinue.gameObject.SetActive(false);
                allMenuBtns.Remove(btnContinue);
            }
        }

        void Update()
        {
            if (!isEnable && Input.anyKeyDown)
            {
                StopAllCoroutines();
                targetImage.sprite = backgroundImages[backgroundImages.Count-1];
                targetImage.color = Color.white;
                targetImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
                CheckSuspendSaveData();
                UI_Canvas.SetActive(true);

                isEnable = true;
                return;
            }
            else if (!isEnable || saveLoadUI.gameObject.activeSelf) return;
            HandleMenuNavigation(ref currentBtnIndex);
        }

        private IEnumerator SlideshowRoutine()
        {
            int currentBtnIndex = 0;
            Color color = targetImage.color;

            while (true) // 무한 반복
            {
                // 1. 현재 순서의 이미지로 교체
                targetImage.sprite = backgroundImages[currentBtnIndex];
                targetImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);

                // 2. Fade In (투명 -> 불투명)
                yield return StartCoroutine(FadeEffect(0f, 1f));

                // 3. 대기 (이미지 감상 시간)
                yield return new WaitForSeconds(displayDuration);

                // 4. Fade Out (불투명 -> 투명)
                yield return StartCoroutine(FadeEffect(1f, 0f));

                // 5. 다음 이미지 인덱스 계산 (리스트 끝에 도달하면 0번으로 돌아감)
                currentBtnIndex = (currentBtnIndex + 1) % backgroundImages.Count;

                if (!isEnable)
                {
                    CheckSuspendSaveData();
                    UI_Canvas.SetActive(true);
                    isEnable = true;
                }
            }
        }

        // 페이드 효과를 처리하는 함수 (재사용 가능)
        private IEnumerator FadeEffect(float startAlpha, float endAlpha)
        {
            float timer = 0f;
            Color color = targetImage.color;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;

                // 부드러운 곡선(SmoothStep)으로 알파값 계산
                float alpha = Mathf.SmoothStep(startAlpha, endAlpha, progress);

                color.a = alpha;
                targetImage.color = color;

                yield return null;
            }

            // 오차 보정: 최종값으로 확실하게 설정
            color.a = endAlpha;
            targetImage.color = color;
        }

        void HandleMenuNavigation(ref int currentBtnIndex)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0) return;
            bool changed = false;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentBtnIndex = (currentBtnIndex - 1 + allMenuBtns.Count) % allMenuBtns.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentBtnIndex = (currentBtnIndex + 1) % allMenuBtns.Count;
                changed = true;
            }

            if (changed) UpdateSelection(currentBtnIndex);

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (allMenuBtns[currentBtnIndex].interactable)
                {
                    allMenuBtns[currentBtnIndex].onClick.Invoke();
                }
            }
        }

        void UpdateSelection(int index)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0 || index < 0 || index >= allMenuBtns.Count) return;
            allMenuBtns[index].Select();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        public void OnClick_NewGame()
        {
            // TODO: 캐릭터 생성 씬 또는 오프닝 씬으로 이동
            SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
        }

        public void OnClick_LoadGame()
        {
            // 로드 창 활성화
            //confirmWindow.SetActive(true);
            saveLoadUI.Open(false);
        }

        // [이어하기] 버튼: 중단 데이터가 있으면 그것을 로드
        public void OnClick_Continue()
        {
            if (SaveManager.Instance.HasSuspendData())
            {
                // -1번(중단 데이터) 로드 -> 로드 내부에서 파일 삭제됨
                SaveManager.Instance.LoadGame(SaveManager.SUSPEND_SLOT_INDEX);
            }
        }

        public void OnClick_Quit()
        {
            // 에디터와 빌드 버전 분기 처리
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}

