using UnityEditor;
using UnityEngine;

namespace MonsterEditing
{
    public static class MonsterSpritePreview
    {
        /// <returns>True when a packed sprite's asynchronous preview is still loading.</returns>
        public static bool Draw(Rect area, Sprite sprite, Vector2 canvasPixels, float zoom)
        {
            if (sprite == null)
            {
                GUI.Label(area, "이미지 없음", EditorStyles.centeredGreyMiniLabel);
                return false;
            }
            if (Event.current.type != EventType.Repaint) return false;
            float scale = Mathf.Min(area.width / Mathf.Max(1, canvasPixels.x), area.height / Mathf.Max(1, canvasPixels.y)) * zoom;
            float width = sprite.rect.width * scale, height = sprite.rect.height * scale;
            Rect draw = new Rect((area.width - width) * 0.5f, (area.height - height) * 0.5f, width, height);
            GUI.BeginGroup(area);
            try
            {
                if (!sprite.packed)
                {
                    // Use the selected Sprite's region, never the entire source sprite sheet.
                    Texture2D texture = sprite.texture;
                    Rect source = sprite.rect;
                    GUI.DrawTextureWithTexCoords(draw, texture, new Rect(source.x / texture.width,
                        source.y / texture.height, source.width / texture.width, source.height / texture.height), true);
                    return false;
                }
                // Packed/rotated/tight atlas sprites may not have a rectangular textureRect.
                Texture2D preview = AssetPreview.GetAssetPreview(sprite);
                if (preview != null) GUI.DrawTexture(draw, preview, ScaleMode.ScaleToFit, true);
                else GUI.Label(new Rect(0, 0, area.width, area.height), "아틀라스 미리보기 준비 중…", EditorStyles.centeredGreyMiniLabel);
                return preview == null;
            }
            finally { GUI.EndGroup(); }
        }
    }
}
