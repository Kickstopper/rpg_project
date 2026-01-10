using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using UI.DungeonMapScene;

namespace Controller
{
    public class MonsterController : MonoBehaviour
    {
        // 런타임에 변하는 데이터 (현재 HP 등)
        public int currentHp;
        public int currentMp;

        [Header("Hit Feedback")]
        // 일반 피격 시 흔들림 강도/시간
        private float normalShakeMagnitude = 5f; 
        private float normalShakeDuration = 0.2f;

        // 크리티컬 피격 시 흔들림 강도/시간 (더 세고 길게)
        private float critShakeMagnitude = 15f;
        private float critShakeDuration = 0.5f;

        // 원본 데이터 참조 (이름, 스탯, 내성 등)
        // MonsterDatabase 클래스 안에 있는 MonsterEntry를 가져옵니다.
        public MonsterDatabase.MonsterEntry sourceData;

        [Header("UI Reference")]
        public Image monsterImage; // 몬스터의 모습을 보여줄 UI 컴포넌트

        // 후열일 때 적용할 색상 (약간 어두운 회색)
        private Color backRowColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
        private Color frontRowColor = Color.white;

        private Button button; // 자신의 버튼 컴포넌트
        private Color originalColor; // 원래 색상 (보통 흰색)
        private Coroutine highlightCoroutine; // 깜빡임 효과 제어용
        
        [Header("Position Info")]
        public int columnIndex; // 0: 왼쪽, 1: 가운데, 2: 오른쪽
        
        // 현재 방어 태세인지 확인하는 플래그
        public bool isGuarding = false;

        // 몬스터는 기본 스탯 그대로 반환
        public int GetTotalAgi() => sourceData.stats.agi;
        public int GetTotalLuc() => sourceData.stats.luc;
        public int GetTotalStr() => sourceData.stats.str;
        public int GetTotalMag() => sourceData.stats.mag;
        
        public void Initialize(MonsterDatabase.MonsterEntry data)
        {
            sourceData = data;

            // 1. 기본 스탯 설정
            currentHp = data.stats.vit * 5; // 예: 체력 * 5 공식 (게임에 맞게 수정)
            currentMp = data.stats.mag * 3;

            // 2. 이미지 교체
            if (monsterImage != null && data.image != null)
            {
                monsterImage.sprite = data.image[0];
                monsterImage.SetNativeSize(); // 이미지 원본 비율 맞춤
            }
            
            // 3. 이름 설정 (디버그용)
            gameObject.name = $"{data.name}_{data.id}";

            // 버튼 설정
            button = GetComponent<Button>();
            if (button != null)
            {
                // 버튼이 클릭되면 OnClicked 함수 실행하도록 연결
                button.onClick.RemoveAllListeners(); // 재사용 시 중복 방지
                button.onClick.AddListener(OnClicked);
            }

            if (monsterImage != null) originalColor = monsterImage.color;
        }

        // 열(Row)에 따라 색상 즉시 변경 (스폰 시 사용)
        public void SetRowAppearance(bool isFront)
        {
            // 목표 색상 결정
            Color targetColor = isFront ? frontRowColor : backRowColor;

            if (monsterImage != null)
            {
                monsterImage.color = targetColor;
            }

            // "이제부터 이 색이 나의 기본 색이다"라고 저장
            originalColor = targetColor;
        }

        // 이동 애니메이션용 색상 설정 (CombatManager에서 Lerp할 때 사용)
        public void SetColor(Color color)
        {
            if (monsterImage != null)
            {
                monsterImage.color = color;
            }
            
            // 색이 변했으면 기본 색 정보도 갱신
            originalColor = color; 
        }

        // 소환될 때 자신의 열 번호를 부여받음
        public void SetPositionInfo(int colIndex)
        {
            this.columnIndex = colIndex;
        }

        // 선택 상태 켜기/끄기
        public void SetSelectionState(bool isSelected)
        {
            if (monsterImage == null) return;

            if (isSelected)
            {
                // 선택됐을 때: 노란색(또는 지정색)으로 변경 + 깜빡임 등
                monsterImage.color = Color.yellow; 
                // (주의: 여기서 originalColor를 덮어쓰면 안 됨)
            }
            else
            {
                // 선택 해제됐을 때: 저장해둔 originalColor(어두운 색)로 복구
                monsterImage.color = originalColor; 
            }
        }

        // 외부(CombatManager)에서 호출할 함수
        public void TriggerHitShake(bool isCritical)
        {
            // 혹시 이미 흔들리고 있다면 멈추고 새로 시작
            StopCoroutine("ProcessHitShake");

            float magnitude = isCritical ? critShakeMagnitude : normalShakeMagnitude;
            float duration = isCritical ? critShakeDuration : normalShakeDuration;

            StartCoroutine(ProcessHitShake(magnitude, duration));
        }

        // 실제 흔들림을 수행하는 코루틴
        private IEnumerator ProcessHitShake(float magnitude, float duration)
        {
            // 원래 위치 저장 (흔들린 후 돌아오기 위함)
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 랜덤한 방향으로 오프셋 생성
                // 2D 게임이므로 x, y축으로만 흔들림 적용
                float xOffset = Random.Range(-1f, 1f) * magnitude;
                float yOffset = Random.Range(-1f, 1f) * magnitude;

                // 위치 적용 (원래 위치 기준)
                transform.localPosition = originalPos + new Vector3(xOffset, yOffset, 0);

                elapsed += Time.deltaTime;
                // 다음 프레임까지 대기 (프레임마다 위치 변경 = 떨림 효과)
                yield return null; 
            }

            // 원래 위치로 복구
            transform.localPosition = originalPos;
        }

        // 노란색 <-> 원래색 반복해서 부드럽게 깜빡이는 연출
        IEnumerator AnimateHighlight()
        {
            while (true)
            {
                // PingPong을 이용해 0~1 사이 값을 오가게 함
                float time = Mathf.PingPong(Time.time * 5f, 1f); 
                
                // 원래색(White)과 선택색(Yellow) 사이를 부드럽게 섞음
                monsterImage.color = Color.Lerp(originalColor, Color.cyan, time);
                
                // (선택 사항) 약간 커졌다 작아졌다 하는 연출 추가
                // float scale = Mathf.Lerp(1.0f, 1.1f, time);
                // transform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        // 몬스터가 클릭되었을 때 실행됨
        void OnClicked()
        {
            // 전투 매니저에게 "나(this)를 타겟으로 선택했다"고 알림
            CombatManager.Instance.OnMonsterSelected(this);
        }

        // 상태 초기화 (턴 시작 시 호출)
        public void ResetStatus()
        {
            isGuarding = false;
            // 추후 독, 마비 등의 상태이상 해제 로직도 여기에 추가 가능
        }

        // AI 행동 결정 함수
        public CombatAction ChooseAction(List<PlayerController> players)
        {
            // 예시 AI: HP가 30% 미만이면 50% 확률로 방어
            float hpRatio = (float)currentHp / sourceData.stats.vit; // 혹은 maxHp
            
            if (hpRatio < 0.3f && Random.value < 0.5f)
            {
                Debug.Log($"{sourceData.name}: 위기 감지! 방어 태세!");
                
                // 방어 행동 생성 (속도 보정 +2000)
                int guardSpeed = sourceData.stats.agi + 2000;
                return new CombatAction(this.gameObject, this.gameObject, CombatAction.ActionType.Guard, guardSpeed);
            }

            // 기본 공격 로직 (기존 코드)
            PlayerController target = players[Random.Range(0, players.Count)];
            int speed = sourceData.stats.agi + Random.Range(0, 5);
            return new CombatAction(this.gameObject, target.gameObject, CombatAction.ActionType.Attack, speed);
        }

        // 데미지 받는 코루틴 (CombatManager에서 호출)
        public IEnumerator OnDamageTaken(int damage)
        {
            // 1. 체력 감소
            currentHp -= damage;
            Debug.Log($"<color=red>{sourceData.name}에게 {damage} 데미지!</color> (남은 HP: {currentHp})");

            // 2. 피격 연출 (빨간색으로 깜빡임)
            if (monsterImage != null)
            {
                Color originalColor = monsterImage.color;
                monsterImage.color = Color.red; // 빨갛게
                
                // 0.05초마다 왔다갔다 (진동 효과)
                Vector3 originalPos = transform.localPosition;
                transform.localPosition = originalPos + (Vector3.right * 10f); // 오른쪽으로 툭
                yield return new WaitForSeconds(0.05f);
                
                transform.localPosition = originalPos - (Vector3.right * 10f); // 왼쪽으로 툭
                yield return new WaitForSeconds(0.05f);
                
                transform.localPosition = originalPos; // 원위치
                monsterImage.color = originalColor; // 색상 복구
            }

            // 3. 사망 체크
            if (currentHp <= 0)
            {
                Die();
            }
        }

        void Die()
        {
            Debug.Log($"{sourceData.name}이(가) 쓰러졌습니다.");
            
            // 서서히 투명해지며 사라지는 연출 등을 넣을 수 있음
            gameObject.SetActive(false); 
            
            // CombatManager의 몬스터 목록에서 제거하는 로직 필요
            CombatManager.Instance.activeMonsters.Remove(this);
        }
    }
}
