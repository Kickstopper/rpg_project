using UnityEngine;
using Manager;

namespace UI.WorldMapScene
{
    public class WorldMapInitializer : MonoBehaviour
    {
        [Header("연결할 오브젝트")]
        public Transform playerTransform; // 씬에 배치된 플레이어
        public WorldMapCameraFollow cameraFollow;
        private Data.BgmID bgmID;

        void Start()
        {
            Vector3 finalPos;

            if (ManagerRoot.World.isLoadGame)
            {
                // 불러오기로 진입한 경우, 저장된 좌표 사용
                finalPos = ManagerRoot.World.loadedPosition;
                ManagerRoot.World.isLoadGame = false; // 일회성 사용 후 초기화
            }
            else
            {
                // 일반적인 진입(던전에서 나옴 등)인 경우, 테마의 기본 시작 좌표 사용
                var theme = ManagerRoot.World.currentRegionTheme;
                finalPos = theme.startPosition;
                bgmID = theme.fieldBgmID;
                ManagerRoot.Sound.PlayBGM(bgmID);
            }

            // 플레이어 위치 이동
            if (playerTransform != null)
            {
                playerTransform.position = finalPos;
                ManagerRoot.World.currentPlayerTransform = playerTransform; 
            }

            // 카메라 복귀
            if (cameraFollow != null) cameraFollow.SnapToTarget();
        }

        void OnEnable()
        {
            ManagerRoot.Sound.PlayBGM(bgmID);
        }

        void OnDisable()
        {
            ManagerRoot.Sound.StopBGM();
        }
    }
}