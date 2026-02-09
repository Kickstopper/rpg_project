using UnityEngine;
using System.Collections.Generic;
using Manager;
using Data;
using DG.Tweening;
using System.Collections;

namespace Controller
{
    public class MoveUIController : MonoBehaviour
    {
        public PlayerMenuController menuController;
        public GameObject playerPrefab;
        
        [Header("Position Slots")]
        public Transform[] formationSlots; 
        
        [Header("Highlight Settings")]
        private Color selectTargetColor = new Color32(128, 0, 178, 255); 
        private Color focusCursorColor = new Color32(0, 155, 155, 200);          

        private RuntimeCharacterData[] currentSlotData = new RuntimeCharacterData[6];
        private GameObject[] spawnedModels = new GameObject[6];
        private PlayerController[] spawnedControllers = new PlayerController[6];
        
        private int currentCursorIndex = 0; 
        private int firstSelectedIndex = -1; 
        private bool isAnimating = false;

        void OnEnable()
        {
            currentCursorIndex = 0;
            firstSelectedIndex = -1;
            isAnimating = false;
            ResolvePositionConflicts();
            RefreshFormation();
            UpdateVisualFeedback();
        }

        private void ResolvePositionConflicts()
        {
            var party = PartyManager.Instance.partyData;
            if (party == null) return;

            for (int i = 0; i < 6; i++) currentSlotData[i] = null;
            List<RuntimeCharacterData> pending = new List<RuntimeCharacterData>();

            foreach (var member in party)
            {
                int idx = GetIndexFromRowColumn(member.row, member.column);
                if (currentSlotData[idx] == null) currentSlotData[idx] = member;
                else pending.Add(member);
            }

            foreach (var member in pending)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (currentSlotData[i] == null)
                    {
                        currentSlotData[i] = member;
                        break;
                    }
                }
            }
        }

        public void RefreshFormation()
        {
            foreach (var model in spawnedModels) if (model != null) Destroy(model);
            for (int i = 0; i < 6; i++) spawnedControllers[i] = null;

            for (int i = 0; i < 6; i++)
            {
                GameObject go = Instantiate(playerPrefab, formationSlots[i]);
                go.transform.localPosition = Vector3.zero;
                spawnedModels[i] = go;

                PlayerController pc = go.GetComponent<PlayerController>();
                if (pc != null)
                {
                    var member = currentSlotData[i];
                    
                    if (member == null) pc.InitializeEmpty(i);
                    else pc.Initialize(member, null, 0);

                    spawnedControllers[i] = pc;
                }
            }
        }

        void Update()
        {
            if (isAnimating) return;
            HandleNavigationInput();
        }

        private void HandleNavigationInput()
        {
            bool moved = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { if (currentCursorIndex % 3 > 0) { currentCursorIndex--; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { if (currentCursorIndex % 3 < 2) { currentCursorIndex++; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { if (currentCursorIndex >= 3) { currentCursorIndex -= 3; moved = true; } }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { if (currentCursorIndex < 3) { currentCursorIndex += 3; moved = true; } }

            if (moved)
            {
                SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                UpdateVisualFeedback();
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ExecuteSelection();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (firstSelectedIndex != -1)
                {
                    firstSelectedIndex = -1;
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cancel);
                    UpdateVisualFeedback();
                }
                else
                {
                    ApplyChangesToPartyData();
                    menuController.CloseMoveUI();
                }
            }
        }

        private void ApplyChangesToPartyData()
        {
            for (int i = 0; i < 6; i++)
            {
                if (currentSlotData[i] != null)
                {
                    bool isFront = (i < 3);
                    currentSlotData[i].row = isFront ? RowType.Front : RowType.Back;
                    currentSlotData[i].column = (ColumnType)(isFront ? i : i - 3);
                }
            }
        }

        private void ExecuteSelection()
        {
            if (firstSelectedIndex == -1)
            {
                if (currentSlotData[currentCursorIndex] != null)
                {
                    firstSelectedIndex = currentCursorIndex;
                    SoundManager.Instance.PlaySFX(SfxID.UI_Click);
                    // 선택 시 살짝 위로 튀어오르는 효과
                    spawnedModels[firstSelectedIndex].transform.DOPunchPosition(Vector3.up * 10f, 0.3f, 2, 0.5f);
                }
            }
            else
            {
                // 데이터 스왑 및 애니메이션 실행
                StartCoroutine(SwapWithAnimation(firstSelectedIndex, currentCursorIndex));
                firstSelectedIndex = -1;
            }
            UpdateVisualFeedback();
        }

        private IEnumerator SwapWithAnimation(int idxA, int idxB)
        {
            if (idxA == idxB) yield break;

            isAnimating = true;
            SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            GameObject modelA = spawnedModels[idxA];
            GameObject modelB = spawnedModels[idxB];

            Sequence swapSeq = DOTween.Sequence();

            // A 모델을 B 슬롯 위치로 이동
            if (modelA != null)
            {
                modelA.transform.SetParent(formationSlots[idxB]);
                swapSeq.Join(modelA.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.OutCubic));
            }

            // B 모델을 A 슬롯 위치로 이동
            if (modelB != null)
            {
                modelB.transform.SetParent(formationSlots[idxA]);
                swapSeq.Join(modelB.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.OutCubic));
            }

            yield return swapSeq.WaitForCompletion();

            // 실제 데이터 및 참조 배열 스왑
            RuntimeCharacterData tempStat = currentSlotData[idxA];
            currentSlotData[idxA] = currentSlotData[idxB];
            currentSlotData[idxB] = tempStat;

            spawnedModels[idxA] = modelB;
            spawnedModels[idxB] = modelA;

            // 컨트롤러 참조도 함께 업데이트
            PlayerController tempCtrl = spawnedControllers[idxA];
            spawnedControllers[idxA] = spawnedControllers[idxB];
            spawnedControllers[idxB] = tempCtrl;

            isAnimating = false;
            UpdateVisualFeedback();
        }

        private void UpdateVisualFeedback()
        {
            for (int i = 0; i < 6; i++)
            {
                if (spawnedControllers[i] != null) spawnedControllers[i].ResetHighlightColor();
            }

            if (firstSelectedIndex != -1 && spawnedControllers[firstSelectedIndex] != null)
            {
                spawnedControllers[firstSelectedIndex].SetHighlightColor(selectTargetColor);
            }

            if (spawnedControllers[currentCursorIndex] != null)
            {
                spawnedControllers[currentCursorIndex].SetHighlightColor(focusCursorColor);
            }
        }
        
        private int GetIndexFromRowColumn(RowType row, ColumnType col)
        {
            // Row에 따른 시작 인덱스 결정 (Front는 0부터, Back은 3부터)
            int rowIndex = (row == RowType.Front) ? 0 : 3;

            // ColumnType 열거형 값을 정수로 변환하여 더함
            int colIndex = (int)col;

            return rowIndex + colIndex;
        }
    }
}