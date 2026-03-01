using UnityEngine;
using System.Collections.Generic;
using UI.DungeonMapScene;

namespace UI.WorldMapScene
{
    public class WorldMapEncounterController : MonoBehaviour
    {
        [Header("인카운터 시스템")]
        public EncounterSystem encounterSystem; 

        [Header("이동 거리 설정")]
        public Transform playerTransform; // 플레이어의 Transform
        public float unitsPerStep = 2.0f; // 몇 유닛을 이동해야 1 걸음?

        [Header("이 지역 몬스터 목록")]
        public List<string> regionMonsters;

        private Vector3 lastPosition;
        private float accumulatedDistance = 0f;

        void Start()
        {
            if (encounterSystem != null)
                encounterSystem.Initialize(regionMonsters);

            if (playerTransform != null)
                lastPosition = playerTransform.position;
        }

        void Update()
        {
            if (playerTransform == null) return;

            // 지난 프레임부터 지금까지 얼마나 이동했는지 거리 계산
            float distanceMoved = Vector3.Distance(playerTransform.position, lastPosition);
            
            // 이동한 거리를 누적
            accumulatedDistance += distanceMoved;
            
            // 현재 위치를 다시 갱신
            lastPosition = playerTransform.position;

            // 누적된 거리가 설정한 unitsPerStep 기준을 넘었을 때 던전에서의 1스텝으로 계산
            if (accumulatedDistance >= unitsPerStep)
            {
                accumulatedDistance -= unitsPerStep; // 걸음 수 수치를 차감하고 나머지 거리는 보존
                
                if (encounterSystem != null)
                    encounterSystem.OnStepTaken();
            }
        }
    }
}