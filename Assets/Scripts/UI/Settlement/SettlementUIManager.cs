using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using UI.DungeonMapScene;
using TMPro;
using UnityEngine.EventSystems;
public class SettlementUIManager : MonoBehaviour
{
    [Header("Phase 1 & 2: Settlement Panels")]
    public GameObject settlementPanel;
    
    // 명세서 세부 내역을 표시할 Text UI들
    public TextMeshProUGUI txtCurrentMoney;     // 현재 소지금
    public TextMeshProUGUI txtRentalFee;        // 기기 대여비
    public TextMeshProUGUI txtPartnerSalary;    // 파트너 급여
    public TextMeshProUGUI txtDebtInterest;     // 채무 이자
    public TextMeshProUGUI txtTotalBilled;      // 총 청구액 합계
    public TextMeshProUGUI txtResultBalance;    // 결제 처리 상태 및 최종 잔액

    // 연산 및 애니메이션을 위한 데이터 캐싱
    private int _cachedCurrentMoney;
    private int _cachedTotalBilled;

    [Header("Phase 3: Partner Contract")]
    public GameObject partnerContractPanel;
    public Image partnerPortrait;               // 파트너 이미지
    public TextMeshProUGUI partnerInfoText;     // 파트너 정보
    public Button btnExtend;
    public Button btnTerminate;

    [Header("Phase 4: Finish")]
    public GameObject okPopupPanel;
    public Button btnOk;

    private bool _isSettlementPending = false;

    private void Start()
    {
        ManagerRoot.Time.OnPayday += StartSettlementProcess;
        
        settlementPanel.SetActive(false);
        partnerContractPanel.SetActive(false);
        okPopupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ManagerRoot.Time != null)
        {
            ManagerRoot.Time.OnPayday -= StartSettlementProcess;
        }
    }

    // 즉시 실행하지 않고 대기 상태로 전환합니다.
    private void StartSettlementProcess()
    {
        // 이미 대기 중이거나 실행 중이라면 중복 실행 방지
        if (_isSettlementPending || ManagerRoot.GameState.CurrentState == GameState.Settlement) 
            return;
        
        _isSettlementPending = true;
        StartCoroutine(WaitAndExecuteSettlementRoutine());
    }

    // 게임 상태가 안전해질 때까지 기다리는 코루틴
    private IEnumerator WaitAndExecuteSettlementRoutine()
    {
        // 현재 프레임에서 실행 중인 전투 인카운터 판정 등 다른 로직이 완전히 끝날 때까지 대기
        yield return null;

        // 만약 직전의 걸음으로 인해 전투나 대화 상태로 넘어갔다면, 다시 탐험 상태로 돌아올 때까지 대기
        // 탐험 상태이면서, 동시에 맵 이동 등의 트랜지션 연출이 끝났을 때만 통과
        yield return new WaitUntil(() => 
        ManagerRoot.GameState.CurrentState == GameState.Exploration &&
            RaycastingController.Instance != null &&
            !RaycastingController.Instance.IsMoving &&
            !RaycastingController.Instance.IsTransitioning
        );

        // (옵션) 플레이어의 이동 연출이나 카메라 워킹이 완전히 끝날 때까지 대기
        // yield return new WaitUntil(() => !player.IsMoving);

        // 탐험 상태임이 보장되면, 곧바로 정산 로직을 시작
        if (_isSettlementPending)
        {
            _isSettlementPending = false;
            ManagerRoot.GameState.ChangeState(GameState.Settlement); 
            
            // 정산 UI 코루틴 실행
            yield return StartCoroutine(SettlementRoutine());
        }
    }

    private IEnumerator SettlementRoutine()
    {
        // 명세서 표시
        settlementPanel.SetActive(true);
        UpdateSettlementData(); 

        yield return null; 
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0));

        // 다음 페이즈로 넘어가기 전에 프레임을 한 칸 비워 입력 버퍼를 비움
        yield return null;

        // 결제 애니메이션
        yield return StartCoroutine(PlayPaymentAnimationRoutine());
        
        // 결제 애니메이션 직후, 실제 플레이어의 소지금을 검사
        int currentMoney = ManagerRoot.Finance.CurrentMoney;
        int maxDebtLimit = ManagerRoot.Finance.MaxDebtLimit;

        // 파산 판정: 소지금이 채무 한도 이하로 떨어졌을 경우
        if (currentMoney <= maxDebtLimit)
        {
            yield return StartCoroutine(BankruptcyGameOverRoutine());
            yield break; // 다음 페이즈로 넘어가지 않고 코루틴 강제 종료
        }

        // 빚을 졌더라도 한도 내라면 정상 진행 대기
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0));
        
        settlementPanel.SetActive(false);

        // 파트너 계약 연장 페이즈
        // 패널을 켜기 전에 최신 파트너 정보로 UI 갱신
        bool hasPartner = UpdatePartnerData();

        if (hasPartner)
        {
            partnerContractPanel.SetActive(true);
            
            // 키보드 조작을 위해 기존 포커스를 지우고 EXTEND 버튼에 포커스를 둠
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(btnExtend.gameObject);

            bool hasChosen = false;
            bool isExtending = false;

            // 리스너 등록
            btnExtend.onClick.AddListener(() => { isExtending = true; hasChosen = true; });
            btnTerminate.onClick.AddListener(() => { isExtending = false; hasChosen = true; });

            // 플레이어가 버튼을 누를 때까지 무한 대기, 포커스 이탈을 방지
            yield return new WaitUntil(() => 
            {
                // 포커스를 잃었는데 키보드 조작을 시도하면 강제로 btnExtend를 다시 잡아줌
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    if (Input.anyKeyDown) 
                    {
                        EventSystem.current.SetSelectedGameObject(btnExtend.gameObject);
                    }
                }
                return hasChosen;
            });

            // 리스너 해제 및 패널 닫기
            btnExtend.onClick.RemoveAllListeners();
            btnTerminate.onClick.RemoveAllListeners();
            partnerContractPanel.SetActive(false);

            //협상 종료 페이즈 (대화 및 OK 팝업)
            string dialogueID = isExtending ? "Partner_Extend_01" : "Partner_Terminate_01";
            bool isDialogueFinished = false;

            ManagerRoot.GameState.StartEventDialogue(dialogueID, (result) => {
                isDialogueFinished = true;
                ManagerRoot.GameState.ChangeState(GameState.Settlement); 
            });
            
            // 대화가 끝날 때까지 대기
            yield return new WaitUntil(() => isDialogueFinished);
        }
        else
        {
            // 파트너 없음. 연장 계약 페이즈 스킵
        }

        // 대화 종료(혹은 스킵) 후 최종 OK 팝업 표시
        okPopupPanel.SetActive(true);
        
        // 팝업이 뜨면 바로 Space/Return으로 닫을 수 있도록 OK 버튼을 포커싱
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btnOk.gameObject);
        
        bool isOkPressed = false;
        btnOk.onClick.AddListener(() => isOkPressed = true);
        // 버튼 클릭뿐만 아니라, 포커스를 잃었을 때를 대비하여 키보드 스페이스/엔터 입력도 함께 검사
        yield return new WaitUntil(() => 
            isOkPressed || 
            Input.GetKeyDown(KeyCode.Space) || 
            Input.GetKeyDown(KeyCode.Return)
        );
        btnOk.onClick.RemoveAllListeners();
        
        okPopupPanel.SetActive(false);

        ManagerRoot.GameState.ChangeState(GameState.Exploration);
    }

    // 파산 시 실행될 게임 오버 연출 코루틴
    private IEnumerator BankruptcyGameOverRoutine()
    {
        // 시각적 연출 (예: 빨간색 경고 텍스트, 사이렌 소리, 화면 흔들림 등)
        Debug.LogWarning("채무 한도 초과! 회사로부터 파산 선고를 받았습니다.");
        // TODO: UI에 "BANKRUPT" 도장 찍히는 연출 등
        yield return YieldCache.WaitForSeconds(2.0f); // 연출 대기 시간

        // 대화에 집중할 수 있도록 뒤에 깔린 정산 패널 숨기기
        settlementPanel.SetActive(false);

        // 전용 배드엔딩(파산) 대화 호출
        bool isDialogueFinished = false;
        string bankruptcyEventID = "Event_Bankruptcy_01"; // (TODO: 파산 대화 ID로 변경)

        ManagerRoot.GameState.StartEventDialogue(bankruptcyEventID, (result) => 
        {
            isDialogueFinished = true;
            
            // 대화가 끝나는 즉시 상태를 초기화하고 타이틀 씬으로 이동
            ManagerRoot.GameState.ChangeState(GameState.None);
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
        });

        // 씬 전환이 일어날 때까지 코루틴을 대기
        yield return new WaitUntil(() => isDialogueFinished);
    }

    private void UpdateSettlementData()
    {
        // 소지금 가져오기 
        _cachedCurrentMoney = ManagerRoot.Finance.CurrentMoney;

        int partnerSalary = ManagerRoot.Finance.GetMonthlyPayForPartners();
        int deviceRentalFee = ManagerRoot.Finance.DeviceRentalFee;
        int debtInterest = (_cachedCurrentMoney < 0) ? Mathf.RoundToInt(_cachedCurrentMoney * 0.1f) : 0; // 이자율 10%

        // 총 청구액 계산 (모두 양수로 합산)
        _cachedTotalBilled = deviceRentalFee + partnerSalary + Mathf.Abs(debtInterest);

        // UI 텍스트에 내역 셋팅 (CLI 스타일의 포맷팅 적용)
        txtCurrentMoney.text  = $"[CURRENT ASSET]   : {_cachedCurrentMoney:N0} G";
        txtRentalFee.text     = $"DEVICE RENTAL     : -{deviceRentalFee:N0} G";
        txtPartnerSalary.text = $"PARTNER SALARY    : -{partnerSalary:N0} G";
        txtDebtInterest.text  = $"DEBT INTEREST     : -{Mathf.Abs(debtInterest):N0} G";
        
        txtTotalBilled.text   = $"[TOTAL BILLED]    : -{_cachedTotalBilled:N0} G";

        // 결과창은 입력을 받기 전까지 깜빡이는 커서 연출로 대기
        txtResultBalance.text = "PRESS ANY KEY TO PAY_";
    }

    // 현재 파트너 정보로 초상화와 텍스트 UI를 갱신
    private bool UpdatePartnerData()
    {
        if (ManagerRoot.Party == null || ManagerRoot.Party.partyData == null) return false;

        var partyData = ManagerRoot.Party.partyData;
        var currentPartner = partyData.Find(member => !member.isCommander && !member.isMonster && member.isRegular);

        if (currentPartner != null)
        {
            // 초상화 갱신
            var partnerSource = ManagerRoot.Database.charDB.GetEntry(currentPartner.characterId);
            if (partnerSource != null && partnerSource.portraitImage != null)
            {
                partnerPortrait.sprite = partnerSource.portraitImage;
                partnerPortrait.color = Color.white;
            }
            else
            {
                partnerPortrait.color = Color.clear;
            }

            // 파트너 상태 및 요구 급여 텍스트 갱신
            int expectedSalary = currentPartner.stats.level * ManagerRoot.Finance.SalaryPerPartner;
            
            partnerInfoText.text = 
                $"NAME   : {currentPartner.name}\n" +
                $"LEVEL  : {currentPartner.stats.level}\n" +
                $"HP     : {currentPartner.currentHp} / {currentPartner.maxHp}\n" +
                $"--------------------------------\n" +
                $"SALARY : {expectedSalary:N0} G";
                
            return true;
        }

        return false;
    }

    private IEnumerator PlayPaymentAnimationRoutine()
    {
        // 처리 중 텍스트 연출
        txtResultBalance.text = "> PROCESSING PAYMENT...";
        
        // TODO: (선택) 삐빅거리는 기계음이나 프린트 효과음 1회 재생
        // ManagerRoot.Sound.PlaySFX("Terminal_Process");
        
        yield return YieldCache.WaitForSeconds(0.8f);

        // 숫자 차감 연출 셋팅
        float duration = 1.0f; // 애니메이션 진행 시간 (1초)
        float elapsed = 0f;
        
        int startMoney = _cachedCurrentMoney;
        int targetMoney = _cachedCurrentMoney - _cachedTotalBilled;

        // TODO: (선택) 타라락 거리는 카운트다운 효과음 루프 재생 시작

        // 지정된 시간 동안 숫자를 보간(Lerp)하여 타라락 깎이는 효과 적용
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            int currentDisplayMoney = Mathf.RoundToInt(Mathf.Lerp(startMoney, targetMoney, t));
            txtResultBalance.text = $"> BALANCE UPDATE  : {currentDisplayMoney:N0} G";
            
            yield return null; // 다음 프레임까지 대기
        }

        // TODO: (선택) 루프 효과음 정지 및 "결제 완료" 청명한 효과음 1회 재생

        // 소수점 오차 방지를 위해 최종 잔액을 정확하게 한 번 더 출력
        txtResultBalance.text = $"> FINAL BALANCE   : {targetMoney:N0} G";

        // 실제 소지금 갱신
        ManagerRoot.Finance.SetMoney(targetMoney);

        // 애니메이션이 끝난 후 플레이어가 결과를 인지할 수 있도록 짧은 딜레이 제공
        yield return YieldCache.WaitForSeconds(0.5f);
    }
}