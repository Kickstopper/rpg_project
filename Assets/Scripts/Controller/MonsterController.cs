using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UI.DungeonMapScene;
using Data;
using Manager;
using Helper;
using System.Linq;

namespace Controller
{
    public class MonsterController : BattleEntity, IBattleTarget
    {
        private BattleManager manager;
        public MonsterDatabase.MonsterEntry sourceData;
        [Header("VFX")]
        public Material baseAnaglyphMaterial;
        
        // Monster 전용 필드
        public Color backRowColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
        public Color frontRowColor = Color.white;
        public Button selectButton;

        public RowType currentRow;
        public ColumnType currentColumn;

        // 애니메이션용 코루틴
        private Coroutine animationCoroutine;
        private Sprite[] animFrames;
        private float animSpeed;

        // [IBattleTarget 구현]
        public bool IsAlive => currentHp > 0;
        public bool IsMaxHp => currentHp >= maxHp;
        public bool IsMaxMp => currentMp >= maxMp;

        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int CurrentMp => currentMp;
        public int MaxMp => maxMp;

        public int CurrentAnger { get; set; } = 0;
        public int CurrentJoy { get; set; } = 0;
        public int CurrentInterest { get; set; } = 0;

        private Material instanceMaterial;
        private const float FRONT_OFFSET = 0.01f;
        private const float BACK_OFFSET = 0.005f;

        // 상태 기억용 변수
        private bool cachedIsFront = false;      // 전열인지 후열인지 기억
        private bool lastGlobalState = false;    // 이전 프레임의 옵션 상태 기억

        // 외부(BattleManager)에서 호출하는 함수
        public void SetAnaglyphDepth(bool isFront)
        {
            // 내 위치 상태 저장
            cachedIsFront = isFront;

            // 즉시 화면 갱신
            UpdateAnaglyphVisuals(true); 
        }

        // 매 프레임 옵션 변경 감지
        private void Update()
        {
            bool currentGlobalState = GameSettingManager.Instance.useAnaglyph;
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

            // 현재 글로벌 설정 가져오기
            bool useEffect = GameSettingManager.Instance.useAnaglyph;
            
            // 상태 동기화
            lastGlobalState = useEffect;

            // 오프셋 결정 로직
            // 옵션이 꺼져있으면(false) -> 오프셋 0 (평면)
            // 옵션이 켜져있으면(true)  -> 내 위치(cachedIsFront)에 따른 오프셋 적용
            float finalOffset = 0f;

            if (useEffect)
            {
                finalOffset = cachedIsFront ? FRONT_OFFSET : BACK_OFFSET;
            }

            // 셰이더 적용
            instanceMaterial.SetFloat("_Offset", finalOffset);
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null) Destroy(instanceMaterial);
        }

        // [BattleEntity 구현] 스탯 반환
        public override int GetTotalStr() => sourceData.stats.str; 
        public override int GetTotalAgi() => sourceData.stats.agi;
        public override int GetTotalMag() => sourceData.stats.mag;
        public override int GetTotalLuc() => sourceData.stats.luc;
        public override int GetTotalVit() => sourceData.stats.vit;
        public override int GetTotalInt() => sourceData.stats.intel;

        public override VfxID GetBasicAttackVfx()
        {
            return sourceData.basicAttackVfxId;
        }

        public override VfxID GetGunAttackVfx()
        {
           return VfxID.Gun_Auto;
        }

        public override int GetAttack()
        {
            float baseAtk = (level + GetTotalStr()) * 2;
            return Mathf.RoundToInt(baseAtk * GetBuffMultiplier(buffPhysAtk));
        }

        public override int GetDefense()
        {
            float baseDef = sourceData.stats.vit + Mathf.FloorToInt(sourceData.stats.level * 0.5f);
            return Mathf.RoundToInt(baseDef * GetBuffMultiplier(buffPhysDef));
        }
        
        public override int GetMagicAttack()
        {
            float baseMagAtk = (GetTotalInt() / 2) + (GetTotalMag() / 2);
            return Mathf.RoundToInt(baseMagAtk * GetBuffMultiplier(buffMagAtk));
        }

        public override int GetMagicDefense()
        {
            float baseMagDef = GetTotalInt() + (GetTotalMag() / 4) + (GetTotalVit() / 2) + (level / 2);
            return Mathf.RoundToInt(baseMagDef * GetBuffMultiplier(buffMagDef));
        }

        public override int GetHitRate()
        {
            return ((GetTotalStr() + GetTotalLuc()) / 4) + GetTotalAgi() + (level / 2) + level;
        }

        public override int GetEvasion()
        {
            return GetTotalAgi() + (GetTotalInt() / 4) + (GetTotalLuc() / 4) + level + (level / 2);
        }

        public override ResistanceData GetResistances()
        {
            return sourceData.resistances; 
        }

        public void Initialize(MonsterDatabase.MonsterEntry data, BattleManager manager)
        {
            this.manager = manager;
            
            backRowColor = Color.Lerp(manager.fogColor, Color.white, 0.4f);
            frontRowColor = Color.Lerp(manager.fogColor, Color.white, 0.8f);

            sourceData = data;
            entityName = data.name;
            level = data.stats.level;
            
            // HP/MP 설정
            maxHp = data.stats.vit * 5; 
            currentHp = maxHp;
            maxMp = data.stats.mag * 3;
            currentMp = maxMp;

            // 이미지 설정
            if (preferredImage != null && data.image != null && data.image.Length > 0) 
            {
                preferredImage.sprite = data.image[0];
                preferredImage.SetNativeSize();
                originalColor = preferredImage.color;
                preferredImage.rectTransform.pivot = new Vector2(0.5f, 0f);
                
                if (data.image.Length > 1 && data.animInterval > 0f) 
                {
                    animFrames = data.image;
                    animSpeed = data.animInterval;
                }
            }
            
            gameObject.name = entityName;

            selectButton = GetComponent<Button>();
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnClicked);
            }
        }

        private void OnEnable()
        {
            if (animFrames != null && animFrames.Length > 1 && animSpeed > 0f) 
            {
                if (animationCoroutine != null) StopCoroutine(animationCoroutine);
                animationCoroutine = StartCoroutine(AnimateSprite(animFrames, animSpeed));
            }
        }

        private void OnDisable()
        {
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        }

        // 몬스터 대기 모션 애니메이션
        private IEnumerator AnimateSprite(Sprite[] frames, float interval)
        {
            int currentFrame = 0;
            WaitForSeconds waitTime = new WaitForSeconds(interval); // 캐싱

            while (true)
            {
                // 설정된 시간만큼 대기
                yield return waitTime;

                currentFrame = (currentFrame + 1) % frames.Length;
                
                if (preferredImage != null)
                {
                    preferredImage.sprite = frames[currentFrame];
                    // preferredImage.SetNativeSize(); 
                }
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

        // 이동 애니메이션용 색상 설정
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

        // 타겟 지정
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

        // 노란색 깜박임
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
            manager.OnTargetSelected(this);
        }

        // AI 행동 결정 함수
        public BattleAction ChooseAction(BattleContext context)
        {
            // 상태이상에 대한 공통 처리
            RestrictionType restriction = CheckActionRestriction();

            if (restriction == RestrictionType.SkipTurn)
            {
                Debug.Log($"{this.name}은(는) 상태이상으로 움직일 수 없다!");
                return new BattleAction(this.gameObject, this.gameObject, UI.ActionType.Next, 0);
            }
            else if (restriction == RestrictionType.Confusion || restriction == RestrictionType.Charm)
            {
                Debug.Log($"{this.name}은(는) 혼란에 빠졌다!");
                // 아군 적군 구분 없이 무작위 타겟을 골라 평타를 치는 액션 강제 반환
                var allTargets = context.activePlayers.Concat(context.activeMonsters).Where(e => e.currentHp > 0).ToList();
                var randomTarget = allTargets[Random.Range(0, allTargets.Count)];
                return new BattleAction(this.gameObject, randomTarget.gameObject, UI.ActionType.Attack, this.GetTotalAgi());
            }

            // 상태이상 통과 시, AI에 판단 위임
            if (sourceData.aiProfile != null)
                return sourceData.aiProfile.DecideAction(this, context);

            return new BattleAction(this.gameObject, this.gameObject, UI.ActionType.Next, 0);
        }

        // 데미지 처리
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

        public void ApplyHpChange(int amount)
        {
            currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        }

        public void ApplyMpChange(int amount)
        {
            currentMp = Mathf.Clamp(currentMp + amount, 0, maxMp);
        }

        public void ApplyRevive(int percent)
        {
            // 몬스터 부활 로직
            int healAmount = Mathf.FloorToInt(maxHp * (percent / 100f));
            currentHp = healAmount;
            gameObject.SetActive(true);
        }

        public void RefreshView()
        {
            UpdateUI();
        }

        protected override void UpdateUI()
        {
        }

        void Die()
        {
            Debug.Log($"{sourceData.name} 사망");
            gameObject.SetActive(false);
        }
    }
}
