using System.Collections.Generic;
using Manager;
using UnityEngine;
using UnityEngine.UI;
namespace Controller
{
    public class PlayerMenuController : MonoBehaviour
    {
        public List<Button> allMenuBtns;
        private int currentBtnIndex;

        private bool isMenuOpen = false;

        void Start()
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            OnGameStateChanged(GameStateManager.Instance.CurrentState);
        }

        void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.PlayerMenu)
            {
                isMenuOpen = true;
            }
            else
            {
                isMenuOpen = false;
            }
        }

        void Update()
        {
            if (!isMenuOpen) return;

            HandleMenuNavigation(ref currentBtnIndex);
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
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Escape))
            {
                GameStateManager.Instance.ChangeState(GameState.Exploration);
            }
        }

        void UpdateSelection(int index)
        {
            if (allMenuBtns == null || allMenuBtns.Count == 0 || index < 0 || index >= allMenuBtns.Count) return;
            allMenuBtns[index].Select();
            SoundManager.Instance.PlaySFX(Data.SfxID.UI_Cursor);
        }
        
        public void OnClick_Skill()
        {
            Debug.Log("SKILL 미구현");
        }

        public void OnClick_Item()
        {
            Debug.Log("ITEM 미구현");
        }

        public void OnClick_Status()
        {
            Debug.Log("STATUS 미구현");
        }
        
        public void OnClick_Equip()
        {
            Debug.Log("EQUIP 미구현");
        }
        
        public void OnClick_Move()
        {
            Debug.Log("MOVE 미구현");
        }

        public void OnClick_System()
        {
            Debug.Log("SYSTEM 미구현");
        }
        public void OnClick_Suspend()
        {
            // 확인 팝업("저장하고 종료하시겠습니까?") 띄운 후 실행
            GameStateManager.Instance.ChangeState(GameState.TitleScreen);
            
            // 1. 슬롯 없이 -1번 인덱스로 저장
            SaveManager.Instance.SaveGame(SaveManager.SUSPEND_SLOT_INDEX);

            // 2. 타이틀 화면으로 이동 (또는 게임 종료)
            Debug.Log("중단 저장 완료. 타이틀로 이동합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameScene.TITLE_SCENE);
        }
        
    }
}

