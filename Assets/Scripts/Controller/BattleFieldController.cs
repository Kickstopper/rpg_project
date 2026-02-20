using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Controller;
using Data;
using DG.Tweening;
using Manager;
using UnityEngine;
using UnityEngine.UI;

public class BattleFieldController : MonoBehaviour
{
    [Header("Reference")]
    public BattleManager manager;

    [Header("Managers & Data")]
    public MonsterDatabase monsterDB;
    
    [Header("Prefabs")]
    public GameObject defaultMonsterPrefab;
    public GameObject playerPrefab;
    [Header("Spawn Points")]
    public Transform enemyFrontRowContainer;
    public Transform enemyBackRowContainer;
    public Transform playerFrontRowContainer;
    public Transform playerBackRowContainer;
    
    [Header("Player Slots")]
    // 아군 슬롯 리스트
    private List<Transform> playerFrontSlots = new();
    private List<Transform> playerBackSlots = new();
    [Header("Highlight Colors")]
    private Color currentTargetColor = new Color32(128, 0, 178, 255);
    private Color moveSourceColor = Color.gray;   // 이동하려는 내 캐릭터 색상
    // 렌더링 및 그리드 관리용 리스트 (Empty 포함, 총 6개 고정)
    [HideInInspector] public List<PlayerController> allSlotControllers = new();
    [HideInInspector] public List<BattleEntity> activePlayers = new(); 
    [HideInInspector] public List<BattleEntity> activeMonsters = new();
        

    [Header("Slot Management")]
    // 몬스터들의 슬롯을 관리할 리스트 (0,1,2: 전열 / 0,1,2: 후열)
    private List<Transform> frontSlots = new(); 
    private List<Transform> backSlots = new();

    // 점멸 효과 트윈
    private List<Tween> blinkTweens = new List<Tween>();

    [HideInInspector] public int currentPlayerIndex = 0; // 지금 누구 차례?

    
    // 전투 로직용 리스트 (데이터가 있는 캐릭터만)
    [HideInInspector] public List<MonsterDatabase.MonsterEntry> encounterLog = new();

    // 타겟팅 로직 변수
    [HideInInspector] public List<BattleEntity> validTargets = new();
    [HideInInspector] public int currentTargetIndex = 0;

    public bool IsCurrentTargetInFront()
    {
        BattleEntity currentEntity = GetCurrentValidTarget();

        // 타겟 그룹 판별 (플레이어 대상인지 몬스터 대상인지)
        Transform targetFrontContainer = GetTargetFrontContainer();
        
        // 현재 타겟이 전열에 있는지 확인
        return (currentEntity.transform.parent.parent == targetFrontContainer);

    }

    private WaitForSeconds wait10 = new WaitForSeconds(1f);

    public void InitializeSlots()
    {
        activePlayers.Clear();
        activeMonsters.Clear();
        encounterLog.Clear();

        // 파괴되거나 null인 슬롯 참조를 리스트에서 제거
        frontSlots.RemoveAll(slot => slot == null);
        backSlots.RemoveAll(slot => slot == null);
        playerFrontSlots.RemoveAll(slot => slot == null);
        playerBackSlots.RemoveAll(slot => slot == null);

        if (frontSlots.Count == 0) CreateSlotsFor(enemyFrontRowContainer, frontSlots);
        if (backSlots.Count == 0) CreateSlotsFor(enemyBackRowContainer, backSlots);
        ClearSlotContents(frontSlots); 
        ClearSlotContents(backSlots);

        if (playerFrontSlots.Count == 0) CreateSlotsFor(playerFrontRowContainer, playerFrontSlots);
        if (playerBackSlots.Count == 0) CreateSlotsFor(playerBackRowContainer, playerBackSlots);
        ClearSlotContents(playerFrontSlots); 
        ClearSlotContents(playerBackSlots);
    }

    void CreateSlotsFor(Transform container, List<Transform> slotList)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
        slotList.Clear();
        for (int i = 0; i < 3; i++)
        {
            GameObject slot = new GameObject($"Slot_{i}");
            slot.transform.SetParent(container, false);
            slot.AddComponent<RectTransform>();
            slotList.Add(slot.transform);
        }
    }

    void ClearSlotContents(List<Transform> slotList)
    {
        foreach (var slot in slotList) foreach (Transform child in slot) Destroy(child.gameObject);
    }

    public int ActivePlayerCount()
    {
        return activePlayers.Count;
    }

    public void ResetPartyStatus()
    {
        foreach (var player in activePlayers) player.ResetStatus(); 
    }

    public void ResetMonstersStatus()
    {
        foreach (var monster in activeMonsters) monster.ResetStatus(); 
    }

    public void SpawnParty()
    {
        activePlayers.Clear();
        allSlotControllers.Clear();

        // 6개의 슬롯에 들어갈 데이터 배열 (null이면 빈자리)
        RuntimeCharacterData[] slotAssignments = new RuntimeCharacterData[6];
        
        // 자리를 잡지 못한 캐릭터들을 모아둘 리스트
        List<RuntimeCharacterData> pendingCharacters = new List<RuntimeCharacterData>();

        int partyCount = PartyManager.Instance.partyData.Count;

        // 선호하는 위치에 우선 배치
        for (int i = 0; i < partyCount; i++)
        {
            var member = PartyManager.Instance.GetMember(i);
            if (member == null || member.currentHp <= 0) continue;

            // 데이터상의 위치를 인덱스로 변환
            // 전열(0,1,2), 후열(3,4,5)
            int rowIndex = (member.row == RowType.Front) ? 0 : 3;
            int colIndex = (int)member.column; // Left(0), Center(1), Right(2)
            
            // 안전장치: 컬럼이 범위를 벗어나면 Center(1)로 보정하거나 Clamp
            colIndex = Mathf.Clamp(colIndex, 0, 2);

            int targetSlotIndex = rowIndex + colIndex;

            // 자리가 비어있다면 -> 배정
            if (slotAssignments[targetSlotIndex] == null)
            {
                slotAssignments[targetSlotIndex] = member;
            }
            else
            {
                // 자리가 이미 있다면 -> 대기열로 이동
                pendingCharacters.Add(member);
            }
        }

        // 남은 빈자리에 대기 인원 배치
        foreach (var pendingMember in pendingCharacters)
        {
            for (int i = 0; i < 6; i++)
            {
                // 빈 자리를 발견하면
                if (slotAssignments[i] == null)
                {
                    slotAssignments[i] = pendingMember;

                    // 실제 배치된 위치에 맞춰 데이터 갱신 (저장 시 반영되도록)
                    bool isFront = (i < 3);
                    pendingMember.row = isFront ? RowType.Front : RowType.Back;
                    pendingMember.column = (ColumnType)(isFront ? i : i - 3);
                    
                    break; // 배치 완료했으니 다음 대기 인원으로
                }
            }
        }

        // ---------------------------------------------------------
        // 2단계: 결정된 배치대로 실제 오브젝트 생성 (Instantiate)
        // ---------------------------------------------------------
        for (int i = 0; i < 6; i++)
        {
            // 1. 타겟 슬롯 Transform 찾기
            bool isFront = (i < 3);
            int localIndex = isFront ? i : (i - 3);
            Transform targetSlot = isFront ? playerFrontSlots[localIndex] : playerBackSlots[localIndex];

            // 2. 프리팹 생성
            GameObject go = Instantiate(playerPrefab, targetSlot);
            go.transform.localPosition = Vector3.zero;

            PlayerController pc = go.GetComponent<PlayerController>();
            allSlotControllers.Add(pc);

            // 생성된 플레이어 버튼의 자동 내비게이션 비활성화
            if (pc.selectButton != null)
            {
                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                pc.selectButton.navigation = nav;
            }

            // 3. 데이터 주입
            RuntimeCharacterData assignedData = slotAssignments[i];

            if (assignedData != null)
            {
                // 실제 캐릭터 초기화
                pc.Initialize(assignedData, manager, true);
                
                pc.columnIndex = i;
                pc.gameObject.name = pc.entityName;
                activePlayers.Add(pc);
            }
            else
            {
                // 빈 슬롯 초기화
                pc.InitializeEmpty(i);
            }
        }
    }

    public void SpawnMonster(string id)
    {
        SoundManager.Instance.PlaySFX(SfxID.Encounter);
        var entry = monsterDB.GetEntry(id);
        if (entry == null) return;
        // 생성된 몬스터의 데이터를 로그에 기록 (보상 계산용)
        encounterLog.Add(entry);

        // 1. 선호하는 열(Row) 선택
        List<Transform> targetSlots = (entry.preferredRow == RowType.Front) ? frontSlots : backSlots;
        
        // 꽉 찼으면 다른 열로
        if (IsRowFull(targetSlots))
        {
            targetSlots = (targetSlots == frontSlots) ? backSlots : frontSlots;
            if (IsRowFull(targetSlots)) return; // 자리 없음
        }

        // 2. 빈 자리 찾기 (랜덤 또는 순차)
        // ColumnType에 맞춰 배치하려면 여기서 특정 인덱스를 선호하게 할 수 있음
        // 예: "Center 우선" 로직 등. 지금은 랜덤 빈자리 유지.
        List<int> emptyIndices = new List<int>();
        for (int i = 0; i < targetSlots.Count; i++) 
            if (targetSlots[i].childCount == 0) emptyIndices.Add(i);

        int randomIndex = emptyIndices[Random.Range(0, emptyIndices.Count)];
        Transform selectedSlot = targetSlots[randomIndex];

        // 3. 생성
        GameObject prefabToUse = (entry.prefab != null) ? entry.prefab : defaultMonsterPrefab;
        if (prefabToUse == null) return;

        GameObject newMonsterObj = Instantiate(prefabToUse, selectedSlot);
        newMonsterObj.transform.localPosition = Vector3.zero;

        MonsterController controller = newMonsterObj.GetComponentInChildren<MonsterController>();
        if (controller == null) { Destroy(newMonsterObj); return; }

        controller.Initialize(entry, manager);
        newMonsterObj.name = $"{controller.sourceData.race} {controller.sourceData.name}";

        if (controller.currentHp <= 0) { Destroy(newMonsterObj); return; }

        // 몬스터 버튼의 자동 내비게이션 비활성화
        if (controller.selectButton != null)
        {
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            controller.selectButton.navigation = nav;
        }

        // 배치된 위치 정보를 컨트롤러에 주입
        bool isFront = (targetSlots == frontSlots);
        
        controller.SetPositionInfo(randomIndex); // 기존 인덱스 설정
        
        // Enum 정보 설정
        controller.currentRow = isFront ? RowType.Front : RowType.Back;
        controller.currentColumn = (ColumnType)randomIndex; // 0, 1, 2 -> Left, Center, Right 매핑

        controller.SetRowAppearance(isFront); 
        controller.SetAnaglyphDepth(isFront); 
        
        activeMonsters.Add(controller);
    }

    public void ResetPlayerSlotHighlights()
    {
        foreach (PlayerController player in allSlotControllers)
        {
            player.SetMessage(string.Empty);
            player.ResetHighlightColor();
        } 
    }

    public void HighlightToCurrentCharacter()
    {
        (activePlayers[currentPlayerIndex] as PlayerController).SetHighlightColor(currentTargetColor);
    }

    public void ShowBlinkHighlight(List<PlayerController> targets)
    {
        blinkTweens.Clear();
        foreach (var p in targets)
        {
            p.SetHighlightColor(currentTargetColor);
            Image img = p.highlightImage;
            if (img)
            {
                Tween t = img.DOFade(0.4f, 0.3f).SetLoops(-1, LoopType.Yoyo);
                blinkTweens.Add(t);
            }
        }
    }

    public void StopBlinkEffects()
    {
        foreach (var t in blinkTweens) t.Kill(true); // 트윈 즉시 종료 및 원상복구
        blinkTweens.Clear();
        // 알파값 완전 복구
        foreach (var p in activePlayers) (p as PlayerController).ResetHighlightColor();
    }
    public void UpdateValidTargetsHighlight()
    {
        foreach (var monster in validTargets) monster.SetSelectionState(false);
        if (validTargets.Count > 0) validTargets[currentTargetIndex].SetSelectionState(true);
    }

    // 매개변수에 targetSlotTransform 추가 및 이름 명확히 변경
    public void SwapPosition(PlayerController currentActor, PlayerController targetChar, Transform targetSlotTransform)
    {
        Transform originSlotTransform = currentActor.transform.parent;
        
        // 리스트(allSlotControllers) 내의 순서 교체
        int actorListIndex = GetPlayerSlotIndex(currentActor);
        // targetChar가 null(빈 자리)일 수 있으므로, 슬롯 자체의 인덱스를 구하도록 안전 처리
        int targetListIndex = (targetChar != null) ? GetPlayerSlotIndex(targetChar) : GetPlayerSlotIndex(targetSlotTransform);
        
        if (actorListIndex != -1 && targetListIndex != -1)
        {
            allSlotControllers[actorListIndex] = targetChar;
            allSlotControllers[targetListIndex] = currentActor;
        }

        // 물리적 부모 변경 및 인덱스 갱신
        if (targetChar != null) 
        { 
            targetChar.transform.SetParent(originSlotTransform, true); 
            targetChar.columnIndex = GetPlayerSlotIndex(originSlotTransform); 
        }
        
        // 핵심 버그 수정: targetChar의 transform이 아닌 실제 슬롯(targetSlotTransform)을 부모로 설정
        currentActor.transform.SetParent(targetSlotTransform, true);
        currentActor.columnIndex = GetPlayerSlotIndex(targetSlotTransform);

        // 실제 데이터(RuntimeCharacterData) 동기화
        for (int i = 0; i < allSlotControllers.Count; i++)
        {
            PlayerController pc = allSlotControllers[i];
            if (pc != null && !pc.IsEmpty && pc.sourceData != null)
            {
                // 인덱스 0,1,2는 전열(Front), 3,4,5는 후열(Back)
                bool isFront = (i < 3);
                pc.sourceData.row = isFront ? RowType.Front : RowType.Back;
                
                // 컬럼 값 계산 (Left=0, Center=1, Right=2)
                pc.sourceData.column = (ColumnType)(isFront ? i : i - 3);
            }
        }
    }

    // Rolling Vulcan 참가자 선별 (사각형 형성 멤버만 추출)
    public List<PlayerController> GetRollingVulcanParticipants()
    {
        List<PlayerController> participants = new List<PlayerController>();

        // 각 열의 전/후열이 모두 찼는지 확인
        bool col0Full = IsSlotActive(0) && IsSlotActive(3); // 좌측 열
        bool col1Full = IsSlotActive(1) && IsSlotActive(4); // 중앙 열
        bool col2Full = IsSlotActive(2) && IsSlotActive(5); // 우측 열

        // 사각형 형성 여부
        bool isLeftSquare = col0Full && col1Full; // 0,1열 (좌측 사각형)
        bool isRightSquare = col1Full && col2Full; // 1,2열 (우측 사각형)

        List<int> validIndices = new List<int>();

        // 우선순위: 6명(양쪽 모두) -> 왼쪽 -> 오른쪽
        if (isLeftSquare && isRightSquare)
        {
            // 6명 전원 참가
            validIndices.AddRange(new int[] { 0, 1, 2, 3, 4, 5 });
        }
        else if (isLeftSquare)
        {
            // 좌측 4명만 참가 (0, 1, 3, 4)
            validIndices.AddRange(new int[] { 0, 1, 3, 4 });
        }
        else if (isRightSquare)
        {
            // 우측 4명만 참가 (1, 2, 4, 5)
            validIndices.AddRange(new int[] { 1, 2, 4, 5 });
        }

        // 인덱스를 실제 캐릭터 객체로 변환
        foreach (int idx in validIndices)
        {
            if (idx < allSlotControllers.Count)
            {
                var pc = allSlotControllers[idx];
                if (pc != null && pc.currentHp > 0)
                    participants.Add(pc);
            }
        }

        return participants;
    }

    public void ResetCharacterMessage() 
    { 
        foreach(PlayerController pc in activePlayers) pc.SetMessage(string.Empty); 
    }

    public IEnumerator Refresh()
    {
        SetEnemyVisualsActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(enemyFrontRowContainer as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerFrontRowContainer as RectTransform);
        yield return wait10;
    }

    public IEnumerator ProcessEnemyRowShift()
    {
        var backRowMonsters = activeMonsters.Where(m => backSlots.Contains(m.transform.parent)).OrderBy(m => m.columnIndex).ToList();
        foreach (MonsterController monster in backRowMonsters) yield return StartCoroutine(CheckAndMoveForward(monster));
    }

    public IEnumerator ProcessPlayerRowShift()
    {
        for (int col = 0; col < 3; col++)
        {
            int frontIdx = col;
            int backIdx = col + 3;

            PlayerController frontPC = allSlotControllers[frontIdx];
            PlayerController backPC = allSlotControllers[backIdx];

            bool backCanMove = !backPC.IsEmpty && backPC.currentHp > 0;
            if (!backCanMove) continue;

            bool frontIsOpen = frontPC.IsEmpty || frontPC.currentHp <= 0;

            if (frontIsOpen)
            {
                yield return SwapPlayerSlots(frontIdx, backIdx);
            }
        }
    }

    // 슬롯 교체 애니메이션
    IEnumerator SwapPlayerSlots(int frontIdx, int backIdx)
    {
        PlayerController frontPC = allSlotControllers[frontIdx];
        PlayerController backPC = allSlotControllers[backIdx];

        Transform frontSlot = playerFrontSlots[frontIdx]; 
        Transform backSlot = playerBackSlots[backIdx - 3];

        Debug.Log($"[전진] {backPC.name}가 전열로 이동");

        allSlotControllers[frontIdx] = backPC;
        allSlotControllers[backIdx] = frontPC;

        backPC.columnIndex = frontIdx;
        frontPC.columnIndex = backIdx;

        // 부모 변경
        backPC.transform.SetParent(frontSlot, true);
        frontPC.transform.SetParent(backSlot, true);

        // 두 캐릭터를 동시에 이동
        Sequence seq = DOTween.Sequence();
        seq.Join(backPC.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.InOutSine));
        seq.Join(frontPC.transform.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.InOutSine));
        
        yield return seq.WaitForCompletion();
    }

    // 몬스터 전진 연출
    IEnumerator CheckAndMoveForward(MonsterController monster)
    {
        if (frontSlots.Contains(monster.transform.parent)) yield break;

        Transform myFrontSlot = frontSlots[monster.columnIndex];
        bool isSlotEmpty = (myFrontSlot.childCount == 0);

        if (!isSlotEmpty)
        {
            var frontMonster = myFrontSlot.GetChild(0).GetComponent<MonsterController>();
            if (frontMonster != null && frontMonster.currentHp <= 0)
            {
                activeMonsters.Remove(frontMonster);
                Destroy(frontMonster.gameObject);
                isSlotEmpty = true; 
            }
        }

        if (isSlotEmpty)
        {
            Debug.Log($"[전진] {monster.sourceData.name} -> 전열 이동");
            monster.transform.SetParent(myFrontSlot);
            monster.SetAnaglyphDepth(true);

            Sequence seq = DOTween.Sequence();
            seq.Join(monster.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.OutQuad));
            seq.Join(monster.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutQuad));
            // 색상 보간
            Color startColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            seq.Join(DOVirtual.Color(startColor, Color.white, 0.5f, (c) => monster.SetColor(c)));

            yield return seq.WaitForCompletion();
        }
    }
    
    // 타겟 위치에 따른 진형 변경 분기 처리
    public IEnumerator ApplyFormationChange(int targetCol)
    {
        if (targetCol == 0) // 왼쪽 몬스터 공격
        {
            // 반시계 방향 회전 (Counter-Clockwise)
            Debug.Log("[Formation] Left Target -> Rotate CCW");
            yield return StartCoroutine(RotateParty(false));
        }
        else if (targetCol == 2) // 오른쪽 몬스터 공격
        {
            // 시계 방향 회전 (Clockwise)
            Debug.Log("[Formation] Right Target -> Rotate CW");
            yield return StartCoroutine(RotateParty(true));
        }
        else // 가운데(1) 또는 그 외
        {
            // 전열 3명 랜덤 셔플
            Debug.Log("[Formation] Center Target -> Shuffle Front Row");
            yield return StartCoroutine(ShuffleFrontRowOnly());
        }
    }

    // 롤링발칸 등에서 참여자들만 회전시키는 함수
    public IEnumerator FastRotateParticipants(List<PlayerController> participants, bool clockwise, float duration)
    {
        // 1. 전체 슬롯의 시계 방향 순서 정의 (0 -> 1 -> 2 -> 5 -> 4 -> 3)
        int[] ringOrder = { 0, 1, 2, 5, 4, 3 };

        // 2. 현재 참여자들이 위치한 인덱스만 추출 (순서 유지)
        // 예: 4명(좌+중앙 열)인 경우 -> [0, 1, 4, 3] 추출됨
        List<int> currentIndices = new List<int>();
        foreach (int slotIdx in ringOrder)
        {
            // 해당 슬롯의 캐릭터가 참가자 명단에 있는지 확인
            if (slotIdx < allSlotControllers.Count)
            {
                PlayerController pc = allSlotControllers[slotIdx];
                if (participants.Contains(pc))
                {
                    currentIndices.Add(slotIdx);
                }
            }
        }

        // 만약 참여자가 1명 이하라면 회전 불필요
        if (currentIndices.Count < 2) yield break;

        // 3. 이동 목표 설정 (매핑)
        // Key: 캐릭터, Value: 이동할 목표 슬롯 인덱스
        Dictionary<PlayerController, int> moveMap = new Dictionary<PlayerController, int>();
        int count = currentIndices.Count;

        for (int i = 0; i < count; i++)
        {
            // 현재 슬롯의 주인
            int currentSlotIdx = currentIndices[i];
            PlayerController pc = allSlotControllers[currentSlotIdx];

            // 목표 슬롯 찾기
            // 시계 방향: 내 다음 순번의 슬롯으로 이동
            // 반시계 방향: 내 이전 순번의 슬롯으로 이동
            int nextIndex = clockwise ? (i + 1) : (i - 1);
            
            // 인덱스 보정 (Circular)
            if (nextIndex >= count) nextIndex = 0;
            if (nextIndex < 0) nextIndex = count - 1;

            int targetSlotIdx = currentIndices[nextIndex];
            moveMap.Add(pc, targetSlotIdx);
        }

        // 4. 데이터 갱신 및 애니메이션 실행
        // 데이터 꼬임 방지를 위해 리스트 복제본 생성
        List<PlayerController> nextAllSlots = new List<PlayerController>(allSlotControllers);
        Sequence seq = DOTween.Sequence();

        foreach (var kvp in moveMap)
        {
            PlayerController pc = kvp.Key;
            int targetIdx = kvp.Value;

            // A. 데이터 구조 상의 위치 변경 (임시 리스트에 기록)
            nextAllSlots[targetIdx] = pc;

            // B. 물리적 위치(부모) 및 인덱스 정보 변경
            Transform targetSlot = (targetIdx < 3) ? playerFrontSlots[targetIdx] : playerBackSlots[targetIdx - 3];
            pc.transform.SetParent(targetSlot, true);
            pc.columnIndex = targetIdx;

            // C. 애니메이션 (Duration 동안 이동)
            seq.Join(pc.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.Linear));
        }

        // 5. 실제 데이터 리스트 교체
        allSlotControllers = nextAllSlots;

        yield return seq.WaitForCompletion();
    }

    // 6명 전체 회전 로직
    // 슬롯 배치: 전열(0,1,2), 후열(3,4,5)
    // 시각적 배치:
    // [0] [1] [2]  (Front)
    // [3] [4] [5]  (Back)
    IEnumerator RotateParty(bool clockwise)
    {
        // 새로운 순서를 담을 임시 배열
        PlayerController[] newOrder = new PlayerController[6];

        if (clockwise)
        {
            // 시계 방향 (0->1->2->5->4->3->0)
            newOrder[1] = allSlotControllers[0]; // 0 -> 1
            newOrder[2] = allSlotControllers[1]; // 1 -> 2
            newOrder[5] = allSlotControllers[2]; // 2 -> 5 (전열우측 -> 후열우측)
            newOrder[4] = allSlotControllers[5]; // 5 -> 4
            newOrder[3] = allSlotControllers[4]; // 4 -> 3
            newOrder[0] = allSlotControllers[3]; // 3 -> 0 (후열좌측 -> 전열좌측)
        }
        else
        {
            // 반시계 방향 (0->3->4->5->2->1->0)
            newOrder[3] = allSlotControllers[0]; // 0 -> 3 (전열좌측 -> 후열좌측)
            newOrder[4] = allSlotControllers[3]; // 3 -> 4
            newOrder[5] = allSlotControllers[4]; // 4 -> 5
            newOrder[2] = allSlotControllers[5]; // 5 -> 2 (후열우측 -> 전열우측)
            newOrder[1] = allSlotControllers[2]; // 2 -> 1
            newOrder[0] = allSlotControllers[1]; // 1 -> 0
        }

        // 변경 적용
        yield return StartCoroutine(ApplyPartyReorder(newOrder.ToList()));
    }

    // 전열(0,1,2)만 섞는 로직
    IEnumerator ShuffleFrontRowOnly()
    {
        // 현재 리스트 복사
        List<PlayerController> newOrderList = new List<PlayerController>(allSlotControllers);
        
        // 전열 인덱스(0,1,2)만 추출하여 섞기
        List<PlayerController> frontRow = new List<PlayerController>();
        for(int i=0; i<3; i++) frontRow.Add(allSlotControllers[i]);

        // Fisher-Yates Shuffle
        for (int i = 0; i < frontRow.Count; i++)
        {
            PlayerController temp = frontRow[i];
            int randomIndex = Random.Range(i, frontRow.Count);
            frontRow[i] = frontRow[randomIndex];
            frontRow[randomIndex] = temp;
        }

        // 섞인 결과를 다시 앞부분에 배치
        for(int i=0; i<3; i++)
        {
            newOrderList[i] = frontRow[i];
        }
        // 후열(3,4,5)은 그대로 유지

        yield return StartCoroutine(ApplyPartyReorder(newOrderList));
    }

    // [공통] 재배치 적용 및 애니메이션
    IEnumerator ApplyPartyReorder(List<PlayerController> newOrderedControllers)
    {
        Sequence shuffleSeq = DOTween.Sequence();

        for (int i = 0; i < 6; i++)
        {
            PlayerController pc = newOrderedControllers[i];
            
            // 목표 슬롯 결정
            Transform targetSlot = (i < 3) ? playerFrontSlots[i] : playerBackSlots[i - 3];
            
            // 데이터 갱신
            pc.columnIndex = i; 
            
            // 부모 변경 (WorldPositionStays=true로 하여 순간이동 방지)
            pc.transform.SetParent(targetSlot, true);
            
            // 이동 애니메이션 (LocalPosition 0으로 부드럽게 이동)
            shuffleSeq.Join(pc.transform.DOLocalMove(Vector3.zero, 0.5f).SetEase(Ease.InOutQuad));
        }
        
        // 메인 리스트 갱신
        allSlotControllers = newOrderedControllers;

        yield return shuffleSeq.WaitForCompletion();
        
        // UI 갱신
        ResetPlayerSlotHighlights();
    }

    // 빠른 회전 (사격 간격에 맞춘 속도)
    IEnumerator FastRotateParty(bool clockwise, float duration)
    {
        // RotateParty 로직을 가져오되, DOTween 시간을 duration에 맞춤
        PlayerController[] newOrder = new PlayerController[6];

        if (clockwise)
        {
            newOrder[1] = allSlotControllers[0];
            newOrder[2] = allSlotControllers[1];
            newOrder[5] = allSlotControllers[2];
            newOrder[4] = allSlotControllers[5];
            newOrder[3] = allSlotControllers[4];
            newOrder[0] = allSlotControllers[3];
        }

        // 위치 이동
        Sequence seq = DOTween.Sequence();
        for (int i = 0; i < 6; i++)
        {
            PlayerController pc = newOrder[i];
            Transform targetSlot = (i < 3) ? playerFrontSlots[i] : playerBackSlots[i - 3];
            
            pc.columnIndex = i;
            pc.transform.SetParent(targetSlot, true);
            
            // duration 만큼 빠르게 이동
            seq.Join(pc.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.Linear));
        }
        allSlotControllers = newOrder.ToList();
        
        yield return seq.WaitForCompletion();
    }

    public void RefreshMoveHighlights(int cursorSlotIndex)
    {
        ResetPlayerSlotHighlights();
        if (currentPlayerIndex < activePlayers.Count)
        {
            PlayerController sourcePlayer = activePlayers[currentPlayerIndex] as PlayerController;
            sourcePlayer.SetHighlightColor(moveSourceColor);
        }

        if (cursorSlotIndex < 0) return;
        Transform targetSlot = GetPlayerSlotByIndex(cursorSlotIndex);
        if (targetSlot != null)
        {
            PlayerController targetChar = targetSlot.GetComponentInChildren<PlayerController>();
            if (targetChar != null) targetChar.SetHighlightColor(currentTargetColor);
        }
    }
    
    public void HideTurnOrderUI()
    {
        foreach(var p in activePlayers) if(p.turnOrderText) p.turnOrderText.gameObject.SetActive(false);
        foreach(var m in activeMonsters) if(m.turnOrderText) m.turnOrderText.gameObject.SetActive(false);
    }

    public void CalculateAndShowTurnOrder()
    {
        activePlayers.Sort((a, b) => 
        {
            // 1. 사망자 처리 (죽은 사람은 뒤로)
            bool aAlive = a.currentHp > 0;
            bool bAlive = b.currentHp > 0;
            if (aAlive && !bAlive) return -1; // a 생존, b 사망 -> a가 앞
            if (!aAlive && bAlive) return 1;  // a 사망, b 생존 -> b가 앞
            if (!aAlive && !bAlive) return 0;

            // 2. 속도 계산 (AGI - Penalty)
            // Next나 Gun으로 인한 nextTurnSpeedPenalty가 여기서 반영.
            int speedA = a.GetTotalAgi() - a.nextTurnSpeedPenalty;
            int speedB = b.GetTotalAgi() - b.nextTurnSpeedPenalty;
            
            // 3. 속도 비교 (내림차순: 속도 높은 사람이 먼저)
            if (speedA != speedB) return speedB.CompareTo(speedA);

            // 4. 동점일 경우 행운(LUC) 비교
            return b.GetTotalLuc().CompareTo(a.GetTotalLuc());
        });

        // 정렬된 순서대로 UI 텍스트 갱신
        int orderCounter = 1;
        foreach (var player in activePlayers)
        {
            if (player.turnOrderText != null)
            {
                if (player.currentHp > 0)
                {
                    player.turnOrderText.gameObject.SetActive(true);
                    player.turnOrderText.text = orderCounter.ToString();
                    orderCounter++;
                }
                else
                {
                    player.turnOrderText.gameObject.SetActive(false);
                }
            }
        }
    }

    public GameObject FindNearestLivingTarget(GameObject attacker)
    {
        GameObject bestTarget = null;
        float closestDistance = float.MaxValue;
        Vector3 attackerPos = attacker.transform.position;

        if (attacker.GetComponent<PlayerController>() != null)
        {
            foreach (var monster in activeMonsters)
            {
                if (monster != null && monster.currentHp > 0 && monster.gameObject.activeSelf)
                {
                    float dist = Vector3.Distance(attackerPos, monster.transform.position);
                    if (dist < closestDistance) { closestDistance = dist; bestTarget = monster.gameObject; }
                }
            }
        }
        else if (attacker.GetComponent<MonsterController>() != null)
        {
            foreach (var player in activePlayers)
            {
                if (player != null && player.currentHp > 0 && player.gameObject.activeSelf)
                {
                    float dist = Vector3.Distance(attackerPos, player.transform.position);
                    if (dist < closestDistance) { closestDistance = dist; bestTarget = player.gameObject; }
                }
            }
        }
        return bestTarget;
    }

    public void ClearMonsterField()
    {
        activeMonsters.Clear();
        
        ClearSlotContents(frontSlots);
        ClearSlotContents(backSlots);
    }
    
    public bool IsAllEnemiesDead()
    {
        return activeMonsters.TrueForAll(m => m.currentHp <= 0);
    }

    public bool IsAllPartyDead()
    {
        return activePlayers.TrueForAll(p => p.currentHp <= 0);
    }

    bool IsRowFull(List<Transform> slots)
    {
        foreach (var slot in slots) if (slot.childCount == 0) return false; 
        return true; 
    }

    // 해당 슬롯 인덱스의 플레이어가 전투 가능한 상태인지 확인
    public bool IsSlotActive(int index)
    {
        if (index < 0 || index >= allSlotControllers.Count) return false;
        PlayerController pc = allSlotControllers[index];
        
        // pc가 존재하고, 빈 슬롯이 아니며, 체력이 0보다 커야 함
        return pc != null && !pc.IsEmpty && pc.currentHp > 0;
    }

    public bool IsCharacterInFrontRow(PlayerController pc)
    {
        return (pc.transform.parent.parent == playerFrontRowContainer);
    }

    public bool IsMonsterInFrontRow(BattleEntity monster)
    {
        return (monster.transform.parent.parent == enemyFrontRowContainer);;
    }

    public PlayerController GetCurrentCharacter()
    {
        return activePlayers[currentPlayerIndex] as PlayerController;
    }

    public int GetPlayerSlotIndex(PlayerController slot)
    {
        return allSlotControllers.IndexOf(slot);
    }

    public int GetCurrentChracterIndex()
    {
        var slot = activePlayers[currentPlayerIndex] as PlayerController;
        return allSlotControllers.IndexOf(slot);
    }

    public int GetPlayerSlotIndex(Transform slot)
    {
        int index = playerFrontSlots.IndexOf(slot);
        if (index != -1) return index; 
        index = playerBackSlots.IndexOf(slot);
        if (index != -1) return index + 3; 
        return 0; 
    }

    public Transform GetPlayerSlotByIndex(int index)
    {
        if (index < 0 || index >= 6) return null;
        if (index < 3) { if (index < playerFrontSlots.Count) return playerFrontSlots[index]; }
        else { int backIndex = index - 3; if (backIndex < playerBackSlots.Count) return playerBackSlots[backIndex]; }
        return null; 
    }

    public List<PlayerController> GetPlayerControllers()
    {
        return activePlayers.OfType<PlayerController>().ToList();
    }

    public List<BattleEntity> GetLivingParty()
    {
        return activePlayers.Where(p => p.currentHp > 0).ToList();
    }

    public List<BattleEntity> GetLivingMonsters()
    {
        return activeMonsters.Where(m => m.currentHp > 0).ToList();
    }

    public List<PlayerController> GetCharactersInFrontRow()
    {
        return activePlayers
                .Where(p => p.currentHp > 0 && p.columnIndex < 3)
                .Select(p => p as PlayerController)
                .ToList();
    }

    public BattleEntity GetCurrentValidTarget()
    {
        return validTargets[currentTargetIndex];
    }

    public Transform GetTargetFrontContainer()
    {
        return (validTargets.Count > 0 && validTargets[0] is PlayerController) ? playerFrontRowContainer : enemyFrontRowContainer;
    }

    public int GetFrontLivingCharacterCount()
    {
        return allSlotControllers.Take(3).Count(p => p != null && !p.IsEmpty && p.currentHp > 0);
    }

    // 전투 유닛 및 슬롯 컨테이너 표시/숨김 제어
    public void SetEnemyVisualsActive(bool isActive)
    {
        if (enemyFrontRowContainer) enemyFrontRowContainer.gameObject.SetActive(isActive);
        if (enemyBackRowContainer) enemyBackRowContainer.gameObject.SetActive(isActive);
    }

    public void SetPlayerVisualsActive(bool isActive)
    {
        if (playerFrontRowContainer) playerFrontRowContainer.gameObject.SetActive(isActive);
        if (playerBackRowContainer) playerBackRowContainer.gameObject.SetActive(isActive);
        
    }

    public void SetValidMonsterTargets()
    {
        validTargets.Clear();
        // 전열 몬스터만 필터링
        validTargets = activeMonsters
            .Where(m => m.currentHp > 0 && m.transform.parent.parent == enemyFrontRowContainer)
            .ToList();
    }

    public void SetValidTargetsByTargetScope(TargetScope scope)
    {
        validTargets.Clear();
        if (scope == TargetScope.Single_Enemy)
            validTargets.AddRange(activeMonsters.Where(m => m != null && m.currentHp > 0));
        else if (scope == TargetScope.One_Ally) 
            validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp > 0));
        else if (scope == TargetScope.Dead_Ally)
            validTargets.AddRange(activePlayers.Where(p => p != null && p.currentHp <= 0));
    }

    public void SetValidTargets(List<BattleEntity> targets)
    {
        validTargets = targets;
    }

    public void SetCurrentValidTargetIndex(BattleEntity target)
    {
        currentTargetIndex = validTargets.IndexOf(target);
    }

}
