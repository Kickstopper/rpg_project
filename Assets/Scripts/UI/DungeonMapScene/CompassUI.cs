using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace UI.DungeonMapScene
{
    public class CompassUI : MonoBehaviour
    {
        [Header("Components")]
        // Inspector에서 순서대로 넣어야 함.
        // N(0), E(1), S(2), W(3)
        public List<TextMeshProUGUI> directionTexts; 

        [Header("Settings")]
        public float spacing = 300f; // 글자 간격 (화면 크기에 맞춰 조절)

        // =========================================================
        // 1. 초기화 및 즉시 정렬 (SetDirection)
        // =========================================================
        public void SetDirection(int dirIndex)
        {
            // 애니메이션 없이 즉시 해당 방향 기준으로 배치
            SnapToDirection(dirIndex);
        }

        // =========================================================
        // 2. 회전 애니메이션 (AnimateTurn)
        // =========================================================
        // fromDir: 회전 시작 전 바라보던 방향
        // toDir: 회전 후 바라볼 방향
        // step: 1(우회전), -1(좌회전)
        public void AnimateTurn(int fromDir, int toDir, int step, float duration)
        {
            // 연속 입력이 들어와도 항상 올바른 위치에서 시작하기 위해 시작 방향(fromDir) 기준으로 정렬.
            SnapToDirection(fromDir);

            // 모든 텍스트에 대해 트윈 실행
            for (int i = 0; i < directionTexts.Count; i++)
            {
                RectTransform rt = directionTexts[i].rectTransform;
                
                // 현재(출발) 슬롯 위치 (-1, 0, 1, 2)
                int currentSlot = GetSlotIndex(i, fromDir);
                
                // 목표(도착) 슬롯 위치
                int targetSlot = GetSlotIndex(i, toDir);

                // 목표 X 좌표
                float targetX = targetSlot * spacing;

                // -------------------------------------------------
                // Wrap-around 처리 (화면 끝에서 반대편으로 넘어가는 연출)
                // -------------------------------------------------
                
                // Case A: 우회전 (Step 1) -> 풍경은 왼쪽(<<)으로 이동
                // 슬롯 변화: 1 -> 0 -> -1 -> 2(숨겨짐)
                if (step > 0)
                {
                    // 왼쪽 끝(-1)에 있던 놈이 뒤쪽(2)으로 가야 하는 경우
                    // 실제로는 더 왼쪽(-2)으로 이동해서 사라지는 척해야 자연스러움
                    if (currentSlot == -1 && targetSlot == 2)
                    {
                        rt.DOAnchorPosX(-2 * spacing, duration).SetEase(Ease.Linear);
                    }
                    // 뒤쪽(2)에 있던 놈이 오른쪽 끝(1)으로 등장해야 하는 경우
                    // 시작 전에 오른쪽 밖(2)에 이미 대기 중이어야 함 (SnapToDirection이 보장함)
                    else 
                    {
                        rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
                    }
                }
                // Case B: 좌회전 (Step -1) -> 풍경은 오른쪽(>>)으로 이동
                // 슬롯 변화: -1 -> 0 -> 1 -> 2(숨겨짐)
                else if (step < 0)
                {
                    // 오른쪽 끝(1)에 있던 놈이 뒤쪽(2)으로 가야 하는 경우
                    // 실제로는 더 오른쪽(2)으로 이동 (이미 2가 오른쪽 끝 너머임)
                    if (currentSlot == 1 && targetSlot == 2)
                    {
                         rt.DOAnchorPosX(2 * spacing, duration).SetEase(Ease.Linear);
                    }
                    // 뒤쪽(2)에 있던 놈이 왼쪽 끝(-1)으로 등장해야 하는 경우
                    // 화면 왼쪽 밖(-2)에서 튀어나와야 함.
                    else if (currentSlot == 2 && targetSlot == -1)
                    {
                        // 애니메이션 시작 위해 강제로 -2 위치로 보냄
                        rt.anchoredPosition = new Vector2(-2 * spacing, 0);
                        rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
                    }
                    else
                    {
                        rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
                    }
                }
                // 180도 회전 등
                else
                {
                    rt.DOAnchorPosX(targetX, duration).SetEase(Ease.Linear);
                }
            }
        }

        // 텍스트들을 특정 방향(centerDir) 기준으로 즉시 줄 세우는 함수
        private void SnapToDirection(int centerDir)
        {
            for (int i = 0; i < directionTexts.Count; i++)
            {
                RectTransform rt = directionTexts[i].rectTransform;
                rt.DOKill(); // 진행 중인 트윈 즉시 중단

                int slot = GetSlotIndex(i, centerDir);
                
                // 슬롯 2(뒤쪽)는 우회전 시 등장 대기를 위해 오른쪽 끝(2*spacing)에 둠.
                // 좌회전 시 등장 로직은 AnimateTurn 내부에서 -2로 순간이동.
                rt.anchoredPosition = new Vector2(slot * spacing, 0);
            }
        }

        // 텍스트(i)가 기준 방향(center)으로부터 시각적으로 몇 번째 칸에 있는지 계산
        // 반환값: -1(Left), 0(Center), 1(Right), 2(Back)
        private int GetSlotIndex(int textIdx, int centerIdx)
        {
            // 거리 차이 계산 (text - center)
            int diff = textIdx - centerIdx;

            // -1 ~ 2 범위로 순환 정규화
            // 예(Center=0): 3(W) -> 3 -> -1 (Left)
            // 예(Center=3): 0(N) -> -3 -> 1 (Right)
            
            while (diff <= -2) diff += 4;
            while (diff > 2) diff -= 4;

            return diff;
        }
    }
}