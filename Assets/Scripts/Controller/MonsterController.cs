using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Manager;
using UI.DungeonMapScene;

namespace Controller
{
    public class MonsterController : BattleEntity
    {
        public MonsterDatabase.MonsterEntry sourceData;

        [Header("UI Reference")]
        public Image monsterImage; 

        // Monster 전용 필드
        public int columnIndex;
        private Color backRowColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
        private Color frontRowColor = Color.white;
        private Button button;

        // [BattleEntity 구현] 스탯 반환
        public override int GetTotalAgi() => sourceData.stats.agi;
        public override int GetTotalLuc() => sourceData.stats.luc;
        public override int GetTotalStr() => sourceData.stats.str;
        public override int GetTotalMag() => sourceData.stats.mag;

        public override int GetAttack()
        {
            int statAtk = sourceData.stats.str;
            int levelBonus = sourceData.stats.level;

            return statAtk + levelBonus;
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

        public void Initialize(MonsterDatabase.MonsterEntry data)
        {
            sourceData = data;
            entityName = $"{data.name}_{data.id}"; // 부모 필드 사용
            
            // HP/MP 설정
            maxHp = data.stats.vit * 5; 
            currentHp = maxHp;
            maxMp = data.stats.mag * 3;
            currentMp = maxMp;

            // 이미지 설정
            if (monsterImage != null && data.image != null)
            {
                monsterImage.sprite = data.image[0];
                monsterImage.SetNativeSize();
                originalColor = monsterImage.color; // 부모 필드 사용
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

        // [BattleEntity 구현] 선택 강조
        public override void SetSelectionState(bool isSelected)
        {
            if (monsterImage == null) return;

            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);

            if (isSelected)
            {
                // 몬스터 전용 깜빡임 코루틴 시작
                highlightCoroutine = StartCoroutine(AnimateHighlight());
            }
            else
            {
                monsterImage.color = originalColor;
            }
        }

        // 노란색 <-> 원래색 반복해서 부드럽게 깜빡이는 연출
        IEnumerator AnimateHighlight()
        {
            while (true)
            {
                float time = Mathf.PingPong(Time.time * 5f, 1f); 
                monsterImage.color = Color.Lerp(originalColor, Color.cyan, time);
                yield return null;
            }
        }

        // 몬스터가 클릭되었을 때 실행됨
        void OnClicked()
        {
            CombatManager.Instance.OnMonsterSelected(this);
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

        // [BattleEntity 구현] 데미지 처리
        public override IEnumerator OnDamageTaken(int damage)
        {
            currentHp -= damage;
            Debug.Log($"<color=red>{sourceData.name}에게 {damage} 데미지!</color> (남은 HP: {currentHp})");

            // 몬스터 전용 피격 연출 (빨간색 깜빡임 + 진동)
            if (monsterImage != null)
            {
                Color cachedColor = monsterImage.color;
                monsterImage.color = Color.red; 
                
                Vector3 originalPos = transform.localPosition;
                // ... (기존의 좌우 진동 로직 유지) ...
                yield return new WaitForSeconds(0.1f); 
                
                transform.localPosition = originalPos;
                monsterImage.color = cachedColor;
            }

            if (currentHp <= 0) Die();
        }

        void Die()
        {
            Debug.Log($"{sourceData.name} 사망");
            gameObject.SetActive(false);
            CombatManager.Instance.activeMonsters.Remove(this);
        }
    }
}
