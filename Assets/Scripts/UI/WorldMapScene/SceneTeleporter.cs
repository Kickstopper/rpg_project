using System.Collections;
using Controller;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace UI.WorldMapScene
{
    public class SceneTeleporter : MonoBehaviour
    {
        [Header("이동 설정")]
        public string targetDungeonID = "Dungeon_Floor1";
        public string locationName = "지하 1층 던전"; // UI에 띄울 장소 이름

        [Header("UI 연결")]
        public GameObject messagePanel; // 안내 메시지 패널 (껐다 켰다 할 것)
        public TextMeshProUGUI messageText;        // (선택) 장소 이름을 띄울 텍스트 컴포넌트
        
        [Header("페이드 효과")]
        public Image fadeImage;
        public float fadeDuration = 1.0f;

        // 내부 변수
        private bool isPlayerInTrigger = false; // 플레이어가 범위 안에 있는가?
        private bool isTransporting = false;    // 이미 이동이 시작되었는가?

        void Update()
        {
            // 플레이어가 범위 안에 있고(AND)
            // 이동 중이 아니고(AND)
            // 스페이스바(또는 엔터)를 눌렀다면?
            if (isPlayerInTrigger && !isTransporting && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                StartTeleportSequence();
            }
        }

        // 트리거에 들어왔을 때 -> 안내창 켜기
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInTrigger = true;
                
                // 안내창 활성화
                if (messagePanel != null)
                {
                    messagePanel.SetActive(true);
                    
                    // (선택) 텍스트 내용 변경: "지하 1층 던전(으)로 이동 (Space)"
                    if (messageText != null)
                    {
                        messageText.text = $"-{locationName} 입구-\n이동 OK?";
                    }
                }
            }
        }

        // 트리거에서 나갔을 때 -> 안내창 끄기
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInTrigger = false;
                
                // 안내창 비활성화
                if (messagePanel != null)
                {
                    messagePanel.SetActive(false);
                }
            }
        }

        // 이동 시작 (키를 눌렀을 때 실행됨)
        void StartTeleportSequence()
        {
            isTransporting = true;
            
            // 이동 시작되면 안내창은 바로 끔.
            if (messagePanel != null) messagePanel.SetActive(false);

            // 플레이어 움직임 멈추기 로직 (찾아서 끄기)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var moveScript = player.GetComponent<WorldMapMovementController>();
                if (moveScript != null) moveScript.enabled = false;

                var rb = player.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            // 페이드 아웃 코루틴 시작
            StartCoroutine(FadeAndLoadScene());
        }

        IEnumerator FadeAndLoadScene()
        {
            float timer = 0f;
            Color color = fadeImage.color;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeDuration);
                fadeImage.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            fadeImage.color = new Color(color.r, color.g, color.b, 1f);
            LevelManager.Instance.LoadLevelFromJson(targetDungeonID);
            SceneManager.LoadScene(GameScene.DUNGEON_MAP_SCENE);
        }
    }
}