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
        public string locationName;

        [Header("UI 연결")]
        public GameObject messagePanel;
        public TextMeshProUGUI messageText;
        private Button panelButton;

        [Header("페이드 효과")]
        public Image fadeImage;
        public float fadeDuration = 1.0f;

        // 내부 변수
        private bool isPlayerInTrigger = false; // 플레이어가 범위 안에 있는가?
        private bool isTransporting = false;    // 이미 이동이 시작되었는가?

        void Start()
        {
            if (messagePanel != null)
            {
                panelButton = messagePanel.GetComponent<Button>();
            }
        }

        void Update()
        {
            if (isPlayerInTrigger && !isTransporting && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                StartTeleportSequence();
            }
        }

        // 트리거에 들어왔을 때 안내창 켜기
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInTrigger = true;
                
                // 안내창 활성화
                if (messagePanel != null)
                {
                    messagePanel.SetActive(true);
                    if (panelButton != null)
                    {
                        panelButton.onClick.RemoveListener(StartTeleportSequence); 
                        panelButton.onClick.AddListener(StartTeleportSequence);
                    }
                    
                    if (messageText != null)
                        messageText.text = $"-{locationName} 입구-\n이동 OK?";
                }
            }
        }

        // 트리거에서 나갔을 때 안내창 끄기
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInTrigger = false;
                
                if (messagePanel != null)
                    messagePanel.SetActive(false);
            }
        }

        // 이동 시작 (키를 눌렀을 때 실행됨)
        void StartTeleportSequence()
        {
            if (isTransporting) return;
            isTransporting = true;
            
            if (messagePanel != null) messagePanel.SetActive(false);

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var moveScript = player.GetComponent<WorldMapMovementController>();
                if (moveScript != null) moveScript.enabled = false;

                var rb = player.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

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
            
            DungeonManager.Instance.LoadDungeonFromJson(targetDungeonID);
            SceneManager.LoadScene(GameScene.DUNGEON_MAP_SCENE);
        }
    }
}