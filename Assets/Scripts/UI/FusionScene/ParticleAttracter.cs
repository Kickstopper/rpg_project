using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleAttractor : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // 화면 중앙 (CenterPoint)
    
    [Header("Vortex Settings")]
    public float suckSpeed = 5.0f;   // 중앙으로 빨려가는 속도
    public float rotateSpeed = 5.0f; // 회전 속도 (나선형 궤적)
    public float stopDistance = 0.1f; // 중앙 도달 판정 거리

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        // Simulation Space가 World인지 코드로도 한 번 더 강제 설정
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void LateUpdate()
    {
        if (target == null) return;

        int count = ps.GetParticles(particles);
        Vector3 targetPos = target.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 particlePos = particles[i].position;
            
            // 타겟까지의 거리와 방향 계산
            float distance = Vector3.Distance(particlePos, targetPos);
            Vector3 directionToTarget = (targetPos - particlePos).normalized;

            // 도달했으면 파티클 삭제 (흡수)
            if (distance < stopDistance)
            {
                particles[i].remainingLifetime = 0;
                continue;
            }

            // 움직임 계산 (벡터 합성)
            // 흡입력: 타겟 방향으로 이동
            Vector3 suctionVector = directionToTarget * suckSpeed;

            // 회전력: 타겟 방향의 수직(Tangent) 방향 계산
            // (2D 게임이므로 앞/뒤(Z축)를 기준으로 수직 벡터를 구함)
            Vector3 rotationVector = Vector3.Cross(directionToTarget, Vector3.forward) * rotateSpeed;
            
            // 거리가 멀수록 회전 반경을 크게 하기 위해 거리 비례 적용 (선택 사항)
            // 가까워질수록 회전보다는 흡입력이 강해지도록 연출
            
            // 최종 위치 적용
            // Time.deltaTime을 곱해 프레임 드랍에도 부드럽게 이동
            particles[i].position += (suctionVector + rotationVector) * Time.deltaTime;
        }

        ps.SetParticles(particles, count);
    }
}