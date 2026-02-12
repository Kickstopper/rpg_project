using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace UI.DungeonMapScene
{
    public class CompassUI : MonoBehaviour
    {
        [Header("Components")]
        // 순서대로 N(0), E(1), S(2), W(3) 텍스트를 할당.
        public List<TextMeshProUGUI> directionTexts; 

        [Header("Settings")]
        public float spacing = 150f; // 글자 간격 (픽셀)

        // 내부 상태 변수
        private int _currentDirIndex = 0; // 0:N, 1:E, 2:S, 3:W

        // 초기화 (순간 이동)
        public void SetDirection(int dirIndex)
        {
            _currentDirIndex = dirIndex;
            UpdatePositions(0f); // 시간 0초 (즉시 반영)
        }

        // 회전 애니메이션 (turnStep: 1=우회전, -1=좌회전, 2=뒤로돌기)
        public void AnimateTurn(int targetDirIndex, int turnStep, float duration)
        {
            // 1. 방향 전환 시 글자들이 이동해야 할 방향 결정
            // 플레이어가 우회전(Turn Right)하면, 나침반 글자들은 왼쪽(Left)으로 흘러가야 함.
            // 플레이어가 좌회전(Turn Left)하면, 나침반 글자들은 오른쪽(Right)으로 흘러가야 함.
            
            _currentDirIndex = targetDirIndex;

            // 각 텍스트 요소에 대해 목표 위치 계산 및 트윈 실행
            for (int i = 0; i < directionTexts.Count; i++)
            {
                RectTransform rt = directionTexts[i].rectTransform;
                
                // 목표 상대 인덱스 (-1 ~ 2 범위로 정규화)
                // 예: Target이 N(0)일 때 -> W(-1), N(0), E(1), S(2)
                int relativeIndex = GetRelativeIndex(i, targetDirIndex);
                float targetX = relativeIndex * spacing;

                // 순환 처리
                // 글자가 화면 끝에서 반대편으로 자연스럽게 넘어가는 로직
                
                if (turnStep > 0) // 우회전 중 (글자들은 왼쪽으로 이동)
                {
                    // 만약 목표 위치가 맨 오른쪽(2)인데, 현재 위치가 맨 왼쪽(-1) 근처라면
                    // 이미 왼쪽으로 사라진 글자를 오른쪽 끝에서 나타나게 해야 함
                    if (relativeIndex == 2 && rt.anchoredPosition.x < 0)
                    {
                        rt.anchoredPosition = new Vector2((relativeIndex + 1) * spacing, 0); // 3칸 위치로 순간이동
                    }
                }
                else if (turnStep < 0) // 좌회전 중 (글자들은 오른쪽으로 이동)
                {
                    // 만약 목표 위치가 맨 왼쪽(-1)인데, 현재 위치가 맨 오른쪽(2) 근처라면
                    // 이미 오른쪽으로 사라진 글자를 왼쪽 끝에서 나타나게 해야 함
                    if (relativeIndex == -1 && rt.anchoredPosition.x > spacing)
                    {
                        rt.anchoredPosition = new Vector2((relativeIndex - 1) * spacing, 0); // -2칸 위치로 순간이동
                    }
                }
                // 180도 회전(turnStep 2 or -2)은 그냥 트윈으로 처리 (2칸 이동)

                rt.DOKill();
                rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
            }
        }

        // 모든 텍스트의 위치를 즉시 갱신 (애니메이션 없음)
        private void UpdatePositions(float duration)
        {
            for (int i = 0; i < directionTexts.Count; i++)
            {
                int relativeIndex = GetRelativeIndex(i, _currentDirIndex);
                float targetX = relativeIndex * spacing;

                RectTransform rt = directionTexts[i].rectTransform;
                rt.DOKill();
                
                if (duration <= 0)
                {
                    rt.anchoredPosition = new Vector2(targetX, 0);
                }
                else
                {
                    rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
                }
            }
        }

        // 현재 바라보는 방향(centerDir)을 기준으로 텍스트(i)가 어디에 위치해야 하는지 계산
        // 반환값: -1(Left), 0(Center), 1(Right), 2(Hidden/FarRight)
        private int GetRelativeIndex(int textIndex, int centerDir)
        {
            // 차이 계산 (예: 텍스트 E(1) - 중심 N(0) = 1)
            int diff = textIndex - centerDir;

            // -1 ~ 2 범위로 순환 보정
            // 공식: ((diff + 1) % 4 + 4) % 4 - 1
            // 결과 예시 (Target N(0)): W(-1), N(0), E(1), S(2)
            
            return ((diff + 1) % 4 + 4) % 4 - 1;
        }
    }
}