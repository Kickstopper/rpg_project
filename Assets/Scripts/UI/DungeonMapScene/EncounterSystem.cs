using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Data;
using Manager;
using System.Collections.Generic;

namespace UI.DungeonMapScene
{
    [System.Serializable]
    public class EncounterSystem
    {
        
        [Header("Settings")]
        public EncounterMode currentMode = EncounterMode.Random;
        public int minSteps = 15;
        public int maxSteps = 30;

        [Header("UI References")]
        public Slider dangerSlider;
        public TextMeshProUGUI dangerText;
        public Image fillImage;
        
        [Header("Colors")]
        public Color32 safeColor = Color.green;
        public Color32 warningColor = Color.gold;
        public Color32 dangerColor = Color.red;

        private int _stepsUntilNextBattle;
        private int _initialSteps;
        
        private Tween _pulseTween;

        private int _lastDangerLevel = -1;
        public List<string> MonsterCandidate => monsters;
        private List<string> monsters;
        private int maxEnemyCount = 6;

        public void Initialize(List<string> monsterCandidate, int maxEnemyCount = 6, EncounterMode mode = EncounterMode.Random)
        {
            monsters = monsterCandidate;
            this.maxEnemyCount = maxEnemyCount;
            currentMode = mode;

            if (currentMode == EncounterMode.Random)
            {
                ResetSteps();
            }
            else
            {
                // 심볼 모드일 때는 초기 위험도 0
                _lastDangerLevel = -1; 
                UpdateDangerUI(0f);
            }
        }

        public void SetVisible(bool visible)
        {
            dangerSlider.gameObject.SetActive(visible);
            dangerText.gameObject.SetActive(visible);
        }

        public void ResetSteps()
        {
            _stepsUntilNextBattle = Random.Range(minSteps, maxSteps);
            _initialSteps = _stepsUntilNextBattle;
            UpdateUI();
        }

        public void OnStepTaken()
        {
            _stepsUntilNextBattle--;
            UpdateUI();

            if (_stepsUntilNextBattle <= 0)
            {
                TriggerEncounter();
            }
        }

        // 심볼 인카운터용 위험도 직접 주입 메서드
        public void UpdateSymbolDanger(float ratio)
        {
            if (currentMode != EncounterMode.Symbol) return;
            UpdateDangerUI(Mathf.Clamp01(ratio));
        }

        private void UpdateUI()
        {
            float ratio = 1.0f - ((float)_stepsUntilNextBattle / _initialSteps);
            UpdateDangerUI(Mathf.Clamp01(ratio));
        }

        private void UpdateDangerUI(float ratio)
        {
            if (ManagerRoot.Module && !ManagerRoot.Module.IsMounted(ModuleFeature.MobSensor)) return;
            if (dangerSlider == null || fillImage == null) return;

            // 위험도를 5% 단위(0~20)로 나누어 단계가 변했을 때만 트윈과 색상을 갱신
            int currentDangerLevel = Mathf.FloorToInt(ratio * 20f); 
            if (_lastDangerLevel == currentDangerLevel) return;
            _lastDangerLevel = currentDangerLevel;

            if (dangerText != null) dangerText.text = $"DANGER: {ratio * 100f:F0}%";

            // 색상 보간
            Color baseColor = (ratio < 0.5f) 
                ? Color.Lerp(safeColor, warningColor, ratio * 2f) 
                : Color.Lerp(warningColor, dangerColor, (ratio - 0.5f) * 2f);
            
            fillImage.color = baseColor;

            // 심장박동 효과
            _pulseTween?.Kill();
            
            if (ratio > 0.01f)
            {
                // 위험할수록 더 빠르게 뜀
                float duration = Mathf.Lerp(1f, 0.1f, ratio);
                _pulseTween = dangerSlider.DOValue(ratio, duration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                dangerSlider.value = 0f;
            }
        }

        private void TriggerEncounter()
        {
            _pulseTween?.Kill();
            if (ManagerRoot.GameState && ManagerRoot.GameState.CurrentState != GameState.Battle &&
                monsters != null && monsters.Count > 0)
            {
                // 전투 시스템에 후보군을 넘기지 않고, 여기서 정확한 출현 파티를 확정
                int spawnCount = Helper.BattleCalculator.DetermineSpawnCount(maxEnemyCount);
                List<string> monsterGroup = new List<string>();
                
                for (int i = 0; i < spawnCount; i++)
                {
                    monsterGroup.Add(monsters[Random.Range(0, monsters.Count)]);
                }

                // 확정된 몬스터 그룹을 전달
                ManagerRoot.GameState.StartEncounter(monsterGroup, Color.white);
            }
            ResetSteps();
        }
    }
}