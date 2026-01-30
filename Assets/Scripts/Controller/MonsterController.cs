using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UI.DungeonMapScene;

namespace Controller
{
    public class MonsterController : BattleEntity
    {
        private CombatController controller;
        public MonsterDatabase.MonsterEntry sourceData;
        [Header("VFX")]
        public Material baseAnaglyphMaterial; // 여기에 'Mat_Anaglyph'를 연결.

        // Monster 전용 필드
        private Color backRowColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
        private Color frontRowColor = Color.white;
        private Button button;

        private Material instanceMaterial;
        private const float FRONT_OFFSET = 0.01f;
        private const float BACK_OFFSET = 0.005f;

        // 상태 기억용 변수
        private bool cachedIsFront = false;      // 내가 전열인지 후열인지 기억
        private bool lastGlobalState = false;    // 최적화: 이전 프레임의 옵션 상태 기억

        // 외부(CombatController)에서 호출하는 함수
        public void SetAnaglyphDepth(bool isFront)
        {
            // 1. 내 위치 상태 저장
            cachedIsFront = isFront;

            // 2. 즉시 화면 갱신
            UpdateAnaglyphVisuals(true); 
        }

        // 매 프레임 옵션 변경 감지
        private void Update()
        {
            // RaycastScreen의 static 변수라고 가정 (접근 방식에 따라 수정 필요)
            bool currentGlobalState = RaycastScreen.useAnaglyph;

            // 옵션값이 이전 프레임과 달라졌을 때만 머티리얼 갱신 (성능 최적화)
            if (currentGlobalState != lastGlobalState)
            {
                UpdateAnaglyphVisuals(false);
            }
        }

        // 실제 머티리얼 값을 변경하는 함수
        private void UpdateAnaglyphVisuals(bool forceUpdate)
        {
            if (preferredImage == null) return;

            // 머티리얼 인스턴싱 (없으면 생성)
            if (instanceMaterial == null)
            {
                if (baseAnaglyphMaterial != null)
                {
                    instanceMaterial = new Material(baseAnaglyphMaterial);
                    preferredImage.material = instanceMaterial;
                }
                else return;
            }

            // 1. 현재 글로벌 설정 가져오기
            bool useEffect = RaycastScreen.useAnaglyph;
            
            // 상태 동기화
            lastGlobalState = useEffect;

            // 2. 오프셋 결정 로직
            // 옵션이 꺼져있으면(false) -> 오프셋 0 (평면)
            // 옵션이 켜져있으면(true)  -> 내 위치(cachedIsFront)에 따른 오프셋 적용
            float finalOffset = 0f;

            if (useEffect)
            {
                finalOffset = cachedIsFront ? FRONT_OFFSET : BACK_OFFSET;
            }

            // 3. 셰이더 적용
            instanceMaterial.SetFloat("_Offset", finalOffset);
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null) Destroy(instanceMaterial);
        }

        // [BattleEntity 구현] 스탯 반환
        public override int GetTotalStr() => sourceData.stats.str + level; 
        public override int GetTotalAgi() => sourceData.stats.agi + level;
        public override int GetTotalMag() => sourceData.stats.mag + level;
        public override int GetTotalLuc() => sourceData.stats.luc + level;
        public override int GetTotalVit() => sourceData.stats.vit + level;

        public override int GetAttack()
        {
            // 차후 구체화
            return GetTotalStr();
        }

        public override int GetMagicAttack()
        {
            // 차후 구체화
            return GetTotalMag();
        }

        public override int GetDefense()
        {
            int statDef = sourceData.stats.vit;
            int levelBonus = Mathf.FloorToInt(sourceData.stats.level * 0.5f);

            return statDef + levelBonus;
        }

        public override ResistanceData GetResistances()
        {
            return sourceData.resistances; 
        }

        public void Initialize(MonsterDatabase.MonsterEntry data, CombatController controller)
        {
            sourceData = data;
            entityName = $"{data.name}_{data.id}"; // 부모 필드 사용

            level = data.stats.level;
            
            // HP/MP 설정
            maxHp = data.stats.vit * 5; 
            currentHp = maxHp;
            maxMp = data.stats.mag * 3;
            currentMp = maxMp;

            // 이미지 설정
            if (preferredImage != null && data.image != null)
            {
                preferredImage.sprite = data.image[0];
                preferredImage.SetNativeSize();
                originalColor = preferredImage.color; // 부모 필드 사용
            }
            
            gameObject.name = entityName;

            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
            }
        }

        // 열(Row)에 따라 색상 즉시 변경 (스폰 시 사용)
        public void SetRowAppearance(bool isFront)
        {
            // 목표 색상 결정
            Color targetColor = isFront ? frontRowColor : backRowColor;

            if (preferredImage != null)
            {
                preferredImage.color = targetColor;
            }

            // "이제부터 이 색이 나의 기본 색이다"라고 저장
            originalColor = targetColor;
        }

        // 이동 애니메이션용 색상 설정 (CombatController에서 Lerp할 때 사용)
        public void SetColor(Color color)
        {
            if (preferredImage != null)
            {
                preferredImage.color = color;
            }
            
            // 색이 변했으면 기본 색 정보도 갱신
            originalColor = color; 
        }

        // 소환될 때 자신의 열 번호를 부여받음
        public void SetPositionInfo(int colIndex)
        {
            this.columnIndex = colIndex;
        }

        // [BattleEntity 구현] 선택 강조
        public override void SetSelectionState(bool isSelected)
        {
            if (preferredImage == null) return;

            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);

            if (isSelected)
            {
                // 몬스터 전용 깜빡임 코루틴 시작
                highlightCoroutine = StartCoroutine(AnimateHighlight());
            }
            else
            {
                preferredImage.color = originalColor;
            }
        }

        // 노란색 <-> 원래색 반복해서 부드럽게 깜빡이는 연출
        IEnumerator AnimateHighlight()
        {
            while (true)
            {
                float time = Mathf.PingPong(Time.time * 5f, 1f); 
                preferredImage.color = Color.Lerp(originalColor, Color.cyan, time);
                yield return null;
            }
        }

        // 몬스터가 클릭되었을 때 실행됨
        void OnClicked()
        {
            controller.OnTargetSelected(this);
        }

        // AI 행동 결정 함수
        public CombatAction ChooseAction(List<BattleEntity> players)
        {
            // 예시 AI: HP가 30% 미만이면 50% 확률로 방어
            float hpRatio = (float)currentHp / sourceData.stats.vit; // 혹은 maxHp
            
            if (hpRatio < 0.3f && Random.value < 0.5f)
            {
                Debug.Log($"{sourceData.name}: 위기 감지! 방어 태세!");
                
                // 방어 행동 생성 (속도 보정 +2000)
                int guardSpeed = sourceData.stats.agi + 2000;
                return new CombatAction(this.gameObject, this.gameObject, UI.ActionType.Guard, guardSpeed);
            }

            // 기본 공격 로직
            BattleEntity target = players[Random.Range(0, players.Count)];
            int speed = sourceData.stats.agi + Random.Range(0, 5);
            return new CombatAction(this.gameObject, target.gameObject, UI.ActionType.Attack, speed);
        }

        // [BattleEntity 구현] 데미지 처리
        public override IEnumerator OnDamageTaken(int damage)
        {
            currentHp -= damage;
            Debug.Log($"<color=red>{sourceData.name}에게 {damage} 데미지!</color> (남은 HP: {currentHp})");

            // 몬스터 전용 피격 연출 (빨간색 깜빡임 + 진동)
            if (preferredImage != null)
            {
                preferredImage.color = Color.red; 
                Vector3 originalPos = transform.localPosition;
                yield return new WaitForSeconds(0.1f); 
                
                transform.localPosition = originalPos;
                preferredImage.color = originalColor;
            }

            if (currentHp <= 0) Die();
        }

        protected override void UpdateUI()
        {
        }

        void Die()
        {
            Debug.Log($"{sourceData.name} 사망");
            gameObject.SetActive(false);
            controller.activeMonsters.Remove(this);
        }
    }
}
