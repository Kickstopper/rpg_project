using System.Linq;
using System.Text;
using RPGProject.Feature.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGProject.Feature.StatusEffects
{
    public static class StatusEffectText
    {
        public static string Name(ActiveEffect effect) => string.IsNullOrEmpty(effect.data.effectName)
            ? effect.data.id.ToString() : effect.data.effectName;
        public static string Duration(ActiveEffect effect) => effect.data.cureType == EffectCureType.TurnBased
            ? $"{effect.turnsRemaining}회" : effect.data.cureType == EffectCureType.ChancePerTurn ? "확률 해제" : "치료 필요";
        public static string Summary(StatusEffectSet set, bool all = false)
        {
            var sorted = set.Effects.OrderByDescending(e => e.data.displayPriority).ThenBy(e => e.data.id).ToList();
            var shown = all ? sorted : sorted.Take(2);
            string text = string.Join(all ? "\n" : " · ", shown.Select(e => $"{Name(e)} [{Duration(e)}]"));
            return !all && sorted.Count > 2 ? text + $" +{sorted.Count - 2}" : text;
        }
        public static string Details(StatusEffectSet set)
        {
            var text = new StringBuilder();
            foreach (var effect in set.Effects.OrderByDescending(e => e.data.displayPriority).ThenBy(e => e.data.id))
            {
                var d = effect.data;
                text.AppendLine($"<b>{Name(effect)}</b> — {Duration(effect)}");
                if (!string.IsNullOrWhiteSpace(d.description)) text.AppendLine(d.description);
                string restriction = d.restrictionType == RestrictionType.SkipTurn ? "행동 실패" :
                    d.restrictionType == RestrictionType.Silence ? "스킬 사용 실패" :
                    d.restrictionType == RestrictionType.Charm ? "같은 진영 공격 (대상이 없으면 대기)" :
                    d.restrictionType == RestrictionType.Panic ? "혼란: 무작위 공격 또는 대기" : null;
                if (restriction != null) text.AppendLine($"{restriction}: {d.restrictionChance:P0}");
                if (d.dotDamage > 0 || d.battleDotMaxHpRatio > 0)
                    text.AppendLine($"행동 기회 종료 시 피해: {d.dotDamage} + 최대 HP의 {d.battleDotMaxHpRatio:P0}");
                if (d.durationType == EffectDurationType.Persistent && (d.explorationDamage > 0 || d.explorationMaxHpRatio > 0))
                    text.AppendLine($"탐색 {d.explorationStepInterval}칸마다: {d.explorationDamage} + 최대 HP의 {d.explorationMaxHpRatio:P0}" +
                        " (최소 HP 1)");
                if (d.atkMultiplier != 1) text.AppendLine($"공격력 ×{d.atkMultiplier:0.##}");
                if (d.defMultiplier != 1) text.AppendLine($"방어력 ×{d.defMultiplier:0.##}");
                if (d.accMultiplier != 1) text.AppendLine($"명중 능력치 ×{d.accMultiplier:0.##}");
                if (d.evaMultiplier != 1) text.AppendLine($"회피 능력치 ×{d.evaMultiplier:0.##}");
                if (d.healingReceivedMultiplier != 1) text.AppendLine($"받는 HP 회복 ×{d.healingReceivedMultiplier:0.##}");
                if (d.cureType == EffectCureType.ChancePerTurn)
                    text.AppendLine($"행동 기회 종료 시 {d.cureChancePerTurn:P0} 확률로 해제 (최대 턴 제한 없음)");
                if (d.cureOnDirectDamage) text.AppendLine("직접 공격으로 피해를 받으면 해제");
                text.AppendLine(d.durationType == EffectDurationType.Persistent ? "전투 후 유지 · 치료로 해제" : "전투 종료 시 해제 · 치료 가능");
                text.AppendLine();
            }
            return text.ToString().TrimEnd();
        }
    }

    /// <summary>Automatically attached to player and enemy cards. Optional anchor and icon support.</summary>
    public sealed class StatusEffectHUD : MonoBehaviour
    {
        BattleEntity owner;
        StatusEffectSet bound;
        RectTransform badge;
        TextMeshProUGUI label;
        Image icon;
        Image background;
        GameObject panel;
        TextMeshProUGUI details;
        bool pinned;

        public static void Attach(BattleEntity entity)
        {
            if (!Application.isPlaying || entity.GetComponentInParent<Canvas>() == null) return;
            var hud = entity.GetComponent<StatusEffectHUD>() ?? entity.gameObject.AddComponent<StatusEffectHUD>();
            hud.owner = entity;
            hud.Build();
            hud.Bind();
        }
        void OnEnable() { if (owner != null) Bind(); }
        void OnDisable() { Unbind(); Close(); }
        void OnDestroy() { Unbind(); if (panel != null) Destroy(panel); }
        void Unbind() { if (bound != null) bound.Changed -= Refresh; bound = null; }
        void Bind()
        {
            Unbind(); bound = owner.StatusEffects; bound.Changed += Refresh; Refresh();
        }
        void Build()
        {
            if (badge != null) return;
            badge = Rect("Status Effects", owner.statusEffectAnchor != null ? owner.statusEffectAnchor : transform);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 1f);
            badge.pivot = new Vector2(0.5f, 1f);
            badge.sizeDelta = new Vector2(180, 30);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            background = badge.gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.09f, 0.94f);
            var button = badge.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { if (pinned) Close(); else { pinned = true; Show(); } });
            var iconRect = Rect("Icon", badge); iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
            iconRect.anchoredPosition = new Vector2(15, 0); iconRect.sizeDelta = new Vector2(22, 22);
            icon = iconRect.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
            var labelRect = Rect("Label", badge); Stretch(labelRect, new Vector2(30, 2), new Vector2(-4, -2));
            label = Text(labelRect, 13); label.enableAutoSizing = true; label.fontSizeMin = 10;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }
        void Refresh()
        {
            if (badge == null || bound == null) return;
            bool visible = bound.Effects.Count > 0;
            badge.gameObject.SetActive(visible);
            if (!visible) { Close(); return; }
            label.text = StatusEffectText.Summary(bound);
            var first = bound.Effects.OrderByDescending(e => e.data.displayPriority).ThenBy(e => e.data.id).First();
            label.color = first.data.displayColor;
            icon.sprite = first.data.icon; icon.enabled = icon.sprite != null;
            if (panel != null && panel.activeSelf) details.text = StatusEffectText.Details(bound);
        }
        void Show()
        {
            if (bound == null || bound.Effects.Count == 0) return;
            if (panel == null)
            {
                var canvas = GetComponentInParent<Canvas>().rootCanvas;
                var root = Rect("Status Effect Details", canvas.transform);
                panel = root.gameObject;
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
                var canvasRect = canvas.transform as RectTransform;
                root.sizeDelta = new Vector2(Mathf.Min(380, canvasRect.rect.width - 20), Mathf.Min(360, canvasRect.rect.height - 20));
                root.gameObject.AddComponent<Image>().color = new Color(.04f, .04f, .07f, .98f);
                root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var close = Rect("Close", root); close.anchorMin = close.anchorMax = new Vector2(1, 1);
                close.pivot = Vector2.one; close.sizeDelta = new Vector2(65, 30);
                close.gameObject.AddComponent<Image>().color = new Color(.2f, .2f, .25f);
                close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
                var closeText = Text(Rect("Text", close), 15); Stretch(closeText.rectTransform, Vector2.zero, Vector2.zero);
                closeText.text = "닫기"; closeText.alignment = TextAlignmentOptions.Center;
                var viewport = Rect("Viewport", root); Stretch(viewport, new Vector2(12, 12), new Vector2(-12, -35));
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = Rect("Content", viewport);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
                content.sizeDelta = Vector2.zero;
                details = Text(content, 16);
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 25;
            }
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            details.text = StatusEffectText.Details(bound);
        }
        void Close() { pinned = false; if (panel != null) panel.SetActive(false); }
        TextMeshProUGUI Text(RectTransform rect, float size)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var source = owner.GetComponentInChildren<TMP_Text>(true);
            text.font = source != null && source.font != null ? source.font : TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.raycastTarget = false; text.color = Color.white;
            return text;
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false); return result;
        }
        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}
