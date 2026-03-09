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
        public int minSteps = 15;
        public int maxSteps = 30;

        [Header("UI References")]
        public Slider dangerSlider;
        public TextMeshProUGUI dangerText;
        public Image fillImage;
        
        [Header("Colors")]
        public Color32 safeColor = Color.green;
        public Color32 warningColor = Color.yellow;
        public Color32 dangerColor = Color.red;

        private int _stepsUntilNextBattle;
        private int _initialSteps;
        private Tween _pulseTween;
        private List<string> monsters;

        public void Initialize(List<string> monsterCandidate)
        {
            monsters = monsterCandidate;
            ResetSteps();
            if (AppManager.Instance)
                SetVisible(AppManager.Instance.IsInstalled(AppFeature.MobSensor));
        }

        private void SetVisible(bool visible)
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

        private void UpdateUI()
        {
            if (AppManager.Instance && !AppManager.Instance.IsInstalled(AppFeature.MobSensor)) return;
            if (dangerSlider == null || fillImage == null) return;

            float ratio = 1.0f - ((float)_stepsUntilNextBattle / _initialSteps);
            ratio = Mathf.Clamp01(ratio);

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
            if (GameStateManager.Instance && GameStateManager.Instance.CurrentState != GameState.Battle &&
                monsters != null && monsters.Count > 0)
            {
                GameStateManager.Instance.StartEncounter(monsters);
            }
            ResetSteps();
        }
    }
}