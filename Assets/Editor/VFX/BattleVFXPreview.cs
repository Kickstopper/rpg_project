using System;
using global::UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RPGProject.Editor.VFX
{
    /// <summary>Renders a private Canvas/Image, never instantiates gameplay behaviours.</summary>
    public sealed class BattleVFXPreview : IDisposable
    {
        private PreviewRenderUtility utility;
        private Canvas canvas;
        private Image image;
        private Vector2 originalSize;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Vector2 originalPivot;
        private Vector2 fitSize = new Vector2(100, 100);

        public void Bind(Image source)
        {
            Dispose();
            try
            {
                utility = new PreviewRenderUtility();
                utility.camera.orthographic = true;
                utility.camera.nearClipPlane = 0.01f;
                utility.camera.farClipPlane = 10000;
                utility.camera.transform.position = new Vector3(0, 0, -5000);
                utility.camera.transform.rotation = Quaternion.identity;
                utility.camera.clearFlags = CameraClearFlags.SolidColor;
                utility.camera.cullingMask = 1 << 5;
                utility.camera.allowHDR = false;
                utility.camera.allowMSAA = false;
                var canvasObject = new GameObject("VFX Preview Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.layer = 5;
                utility.AddSingleGO(canvasObject);
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = utility.camera;
                canvas.referencePixelsPerUnit = 100;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(100, 100);
                var imageObject = new GameObject("VFX Preview Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.hideFlags = HideFlags.HideAndDontSave;
                imageObject.layer = 5;
                imageObject.transform.SetParent(canvas.transform, false);
                image = imageObject.GetComponent<Image>();
                image.color = source.color;
                image.material = source.material;
                image.type = source.type;
                image.preserveAspect = source.preserveAspect;
                image.fillCenter = source.fillCenter;
                image.fillMethod = source.fillMethod;
                image.fillAmount = source.fillAmount;
                image.fillClockwise = source.fillClockwise;
                image.fillOrigin = source.fillOrigin;
                image.useSpriteMesh = source.useSpriteMesh;
                image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                image.raycastTarget = false;
                image.maskable = false;
                var rect = source.rectTransform;
                originalSize = rect.rect.size;
                originalScale = rect.localScale;
                originalRotation = rect.localRotation;
                originalPivot = rect.pivot;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition3D = Vector3.zero;
                image.rectTransform.pivot = originalPivot;
                image.rectTransform.localScale = originalScale;
                image.rectTransform.localRotation = originalRotation;
            }
            catch { Dispose(); throw; }
        }

        public void Configure(BattleVFXAnimator.FrameData[] frames, bool native, float referencePixelsPerUnit)
        {
            if (image == null) return;
            canvas.referencePixelsPerUnit = referencePixelsPerUnit;
            Vector2 maximum = Vector2.one;
            if (frames != null)
            {
                foreach (var frame in frames)
                {
                    Vector2 size = originalSize;
                    if (native && frame.frameSprite != null)
                        size = frame.frameSprite.rect.size * referencePixelsPerUnit / frame.frameSprite.pixelsPerUnit;
                    // Use a fixed canvas over the whole animation, including pivot/rotation/scale.
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector3 offset = new Vector3(((corner & 1) - originalPivot.x) * size.x,
                            (((corner >> 1) & 1) - originalPivot.y) * size.y, 0);
                        offset = originalRotation * Vector3.Scale(offset, originalScale);
                        maximum = Vector2.Max(maximum, new Vector2(Mathf.Abs(offset.x) * 2, Mathf.Abs(offset.y) * 2));
                    }
                }
            }
            fitSize = maximum;
        }

        public void Draw(Rect area, Sprite sprite, bool native, bool visible, float zoom, Color background)
        {
            if (utility == null || image == null || Event.current.type != EventType.Repaint || area.width < 2 || area.height < 2) return;
            image.enabled = visible && sprite != null;
            image.sprite = sprite;
            image.rectTransform.sizeDelta = originalSize;
            if (native && sprite != null) image.SetNativeSize();
            utility.camera.backgroundColor = background;
            utility.BeginPreview(area, GUIStyle.none);
            Texture result;
            try
            {
                utility.camera.aspect = area.width / area.height;
                utility.camera.orthographicSize = Mathf.Max(fitSize.y, fitSize.x / utility.camera.aspect) * 0.55f / Mathf.Max(0.05f, zoom);
                Canvas.ForceUpdateCanvases();
                // Explicitly support the project's URP renderer.
                utility.Render(true, false);
            }
            finally { result = utility.EndPreview(); }
            if (result != null) GUI.DrawTexture(area, result, ScaleMode.StretchToFill, false);
        }

        public void Dispose()
        {
            if (utility != null) utility.Cleanup();
            utility = null;
            canvas = null;
            image = null;
            // Sprite textures and source materials are borrowed assets, not owned by this preview.
        }
    }
}
