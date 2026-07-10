using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Manager;
using Data;
using DG.Tweening;
using UnityEngine.EventSystems;

public static class GameScene
{
    public const string INTRO_SCENE = "IntroScene";
    public const string TITLE_SCENE = "TitleScene";
    public const string CHARACTER_CREATION_SCENE = "CharacterCreationScene";
    public const string DUNGEON_MAP_SCENE = "DungeonMapScene";
    public const string WORLD_MAP_SCENE = "WorldMapScene";
}

namespace Controller 
{
    public class SceneController : MonoBehaviour
    {
        [SerializeField] private GameObject confirmWindow; 
        
        [Header("UI Settings")]
        public GameObject UI_Canvas;
        public Image targetImage; 
        public SaveLoadUIController saveLoadUI;
        public List<Button> allMenuBtns;
        public Button btnContinue; 
        public Button btnLoad;     
        public Button btnNewGame;
        public Button btnQuit;

        private int currentBtnIndex = 0;
        
        [Header("Slide Settings")]
        public List<Sprite> backgroundImages; 
        public float fadeDuration = 1f; 
        public float displayDuration = 0.5f; 

        private bool isEnable = false;     
        public bool IsEnable => isEnable;

        private Coroutine slideshowCoroutine;

        void Start()
        {
            if (UI_Canvas) UI_Canvas.SetActive(false);
#if UNITY_ANDROID || UNITY_IOS
        // 모바일용으로 빌드할 경우 QUIT버튼 비활성화
        if (btnQuit != null && allMenuBtns != null && allMenuBtns.Contains(btnQuit))
        {
            allMenuBtns.Remove(btnQuit);
            btnQuit.gameObject.SetActive(false);
        }
#endif
            ShowAnimation();
        }

        public void ShowAnimation()
        {
            if (targetImage != null && backgroundImages.Count > 0)
            {
                if (backgroundImages.Count > 1)
                {
                    slideshowCoroutine = StartCoroutine(SlideshowRoutine());
                }
                else
                {
                    targetImage.SetNativeSize();
                    Sequence seq = DOTween.Sequence();
                    seq.Append(targetImage.DOFade(1f, fadeDuration).OnComplete(EnableUI));
                    seq.AppendInterval(displayDuration); // 이미지 감상 시간 추가
                    seq.Append(targetImage.DOFade(0.5f, fadeDuration));
                }
            }
            else
            {
                Debug.LogError("이미지 혹은 스프라이트 리스트가 비어있습니다.");
            }
        }

        private void CheckSuspendSaveData()
        {
            bool hasSuspend = SaveManager.Instance.HasSuspendData();

            if (!hasSuspend)
            {
                btnContinue.gameObject.SetActive(false);
                if (allMenuBtns.Contains(btnContinue))
                {
                    allMenuBtns.Remove(btnContinue);
                }
            }
        }

        void Update()
        {
            // 스킵 처리
            if (!isEnable && Input.anyKeyDown)
            {
                SkipIntroAnimation();
                return;
            }
            
            // UI가 켜져있고, 세이브로드 창이 없을 때만 키보드 네비게이션 작동
            if (isEnable && !saveLoadUI.gameObject.activeSelf)
            {
                HandleMenuNavigation(ref currentBtnIndex);
            }
        }

        private void SkipIntroAnimation()
        {
            if (slideshowCoroutine != null)
            {
                StopCoroutine(slideshowCoroutine);
            }
            
            targetImage.DOKill();

            // 스킵 시 최종 화면 상태 강제 세팅
            targetImage.sprite = backgroundImages[backgroundImages.Count - 1];
            targetImage.color = new Color(1, 1, 1, 0.85f);
            targetImage.SetNativeSize();
            
            EnableUI();
        }

        private IEnumerator SlideshowRoutine()
        {
            int imageIndex = 0;

            while (true) // 무한 반복
            {
                targetImage.sprite = backgroundImages[imageIndex];
                targetImage.SetNativeSize();
                yield return targetImage.DOFade(1f, fadeDuration).WaitForCompletion();

                // 대기
                yield return new WaitForSeconds(displayDuration);

                // 첫 번째 이미지가 완전히 켜진 후 UI를 띄움
                if (!isEnable)
                {
                    targetImage.DOFade(0.85f, fadeDuration);
                    EnableUI();
                }

                yield return targetImage.DOFade(0f, fadeDuration).WaitForCompletion();

                imageIndex = (imageIndex + 1) % backgroundImages.Count;
            }
        }

        private void EnableUI()
        {
            if (isEnable) return;
            
            CheckSuspendSaveData();
            UI_Canvas.SetActive(true);
            isEnable = true;

            // UI가 켜질 때 첫 번째 버튼을 선택
            currentBtnIndex = 0;
            UpdateSelection(currentBtnIndex);
        }

        void HandleMenuNavigation(ref int currentIndex)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0) return;
            
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

            // 화면 빈 공간을 클릭해 선택된 객체가 아예 없을 때 포커스 유실 방지 
            if (currentSelected == null)
            {
                EventSystem.current.SetSelectedGameObject(allMenuBtns[currentIndex].gameObject);
            }
            else
            {
                // 마우스 클릭 등으로 선택된 버튼이 변경되었다면 currentIndex 갱신
                int selectedIndex = allMenuBtns.FindIndex(b => b.gameObject == currentSelected);
                if (selectedIndex != -1 && selectedIndex != currentIndex)
                {
                    currentIndex = selectedIndex;
                }
            }

            bool changed = false;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A))
            {
                currentIndex = (currentIndex - 1 + allMenuBtns.Count) % allMenuBtns.Count;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
            {
                currentIndex = (currentIndex + 1) % allMenuBtns.Count;
                changed = true;
            }
            
            if (changed) 
            {
                EventSystem.current.SetSelectedGameObject(null); // 기존 포커스 해제
                UpdateSelection(currentIndex);                   // 포커스 이동 및 사운드 재생
            }
            
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (allMenuBtns[currentIndex].interactable)
                {
                    allMenuBtns[currentIndex].onClick.Invoke();
                }
            }
        }

        void UpdateSelection(int index)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0 || index < 0 || index >= allMenuBtns.Count) return;
            allMenuBtns[index].Select();
            ManagerRoot.Sound.PlaySFX(SfxID.UI_Cursor);
        }

        public void OnClick_NewGame()
        {
            if (ManagerRoot.DungeonEvent != null) ManagerRoot.DungeonEvent.ResetAllEvents();
            if (InventoryManager.Instance != null) ManagerRoot.Inventory.ClearInventory();
            if (ManagerRoot.Flag != null) ManagerRoot.Flag.ClearFlag();
            if (ManagerRoot.Party != null) ManagerRoot.Party.Initialize();
            if (ManagerRoot.DungeonMapState != null) ManagerRoot.DungeonMapState.ClearAllMapData();
            if (ManagerRoot.Module != null) ManagerRoot.Module.Initialize();
            SceneManager.LoadScene(GameScene.CHARACTER_CREATION_SCENE);
        }

        public void OnClick_LoadGame()
        {
            saveLoadUI.Open(false);
        }

        public void OnClick_Continue()
        {
            if (SaveManager.Instance.HasSuspendData())
            {
                SaveManager.Instance.LoadGame(SaveManager.SUSPEND_SLOT_INDEX);
            }
        }

        public void OnClick_Quit()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}