using UnityEngine;
using TMPro; 
using DG.Tweening; 
namespace Controller
{
    public class DamagePopupController : MonoBehaviour
    {
        public TextMeshProUGUI textMesh; 
        
        [Header("Animation Settings")]
        public float jumpHeight = 100f; // 점프 높이
        public float sideSpread = 50f;  // 좌우로 퍼지는 범위
        public float duration = 0.8f;   // 전체 지속 시간

        // 색상 설정 (기존 동일)
        public Color normalColor = Color.white;
        public Color criticalColor = Color.yellow;
        public Color healColor = Color.green;
        public Color missColor = Color.gray;

        public void Setup(int damageAmount, bool isCritical, bool isHeal = false, bool isMiss = false)
        {
            // 1. 텍스트/색상 설정
            if (isMiss)
            {
                textMesh.text = "MISS";
                textMesh.color = missColor;
                textMesh.fontSize = 32;
            }
            else
            {
                textMesh.text = damageAmount.ToString();
                
                if (isHeal)
                {
                    textMesh.color = healColor;
                    textMesh.text = "+" + damageAmount;
                    textMesh.fontSize = 48;
                }
                else if (isCritical)
                {
                    textMesh.color = criticalColor;
                    textMesh.fontSize = 64;
                    textMesh.text += "!";
                }
                else
                {
                    textMesh.color = normalColor;
                    textMesh.fontSize = 48;
                }
            }

            // =========================================================
            // 점프 애니메이션 로직
            // =========================================================

            // 2. 초기 상태 리셋
            textMesh.alpha = 1f;
            transform.localScale = Vector3.zero; // 0에서 시작

            // 3. 목표 지점 계산
            // 숫자가 겹치지 않게 좌우로 랜덤하게 퍼지도록 함
            float randomX = Random.Range(-sideSpread, sideSpread);
            Vector3 targetPos = transform.localPosition + new Vector3(randomX, 0, 0);

            // 4. DOTween 시퀀스 생성
            Sequence seq = DOTween.Sequence();

            // A. 스케일 애니메이션
            seq.Join(transform.DOScale(isCritical ? 1.5f : 1.0f, 0.3f).SetEase(Ease.OutBack));

            // B. 점프 애니메이션 (포물선 운동)
            // DOLocalJump(목표지점, 점프파워, 점프횟수, 시간)
            seq.Join(transform.DOLocalJump(targetPos, jumpHeight, 1, duration).SetEase(Ease.OutQuad));

            // C. 투명해지기 (절반 정도 지났을 때부터 서서히 사라짐)
            seq.Insert(duration * 0.5f, textMesh.DOFade(0, duration * 0.5f));

            // 5. 종료 후 삭제
            seq.OnComplete(() => Destroy(gameObject));
        }
    }
}
