using UnityEngine;

namespace UI.DungeonMapScene
{
    /// <summary>Cached sprite raster and alpha compositing for the software-rendered dungeon viewport.</summary>
    public sealed class DungeonTalkIcon
    {
        private Sprite source;
        private int requestedWidth;
        private bool configured;
        private Color32[] pixels;
        private int width, height;

        public void Configure(Sprite sprite, int pixelWidth)
        {
            pixelWidth = Mathf.Clamp(pixelWidth, 12, 256);
            if (configured && source == sprite && requestedWidth == pixelWidth) return;
            configured = true;
            source = sprite;
            requestedWidth = pixelWidth;
            if (sprite != null)
            {
                try { Bake(sprite, pixelWidth); return; }
                catch (System.Exception error) { Debug.LogWarning("[TALK] 아이콘을 읽지 못해 기본 아이콘을 사용합니다: " + error.Message); }
            }
            BuildDefault(pixelWidth);
        }

        private void Bake(Sprite sprite, int pixelWidth)
        {
            Texture2D texture = sprite.texture;
            Color32[] atlas = ReadTexture(texture);
            Texture2D alphaTexture = sprite.associatedAlphaSplitTexture;
            Color32[] splitAlpha = alphaTexture != null ? ReadTexture(alphaTexture) : null;
            Vector2[] vertices = sprite.vertices;
            Vector2[] uv = sprite.uv;
            ushort[] triangles = sprite.triangles;
            width = pixelWidth;
            height = Mathf.Clamp(Mathf.RoundToInt(pixelWidth * sprite.rect.height / sprite.rect.width), 1, 256);
            pixels = new Color32[width * height];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = vertices[i] * sprite.pixelsPerUnit + sprite.pivot;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) * sprite.rect.width / width, (y + 0.5f) * sprite.rect.height / height);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
                    Vector2 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                    float det = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(det) < 0.00001f) continue;
                    float u = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / det;
                    float v = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / det;
                    float w = 1 - u - v;
                    if (u < -0.0001f || v < -0.0001f || w < -0.0001f) continue;
                    Vector2 sample = uv[ia] * u + uv[ib] * v + uv[ic] * w;
                    int sx = Mathf.Clamp(Mathf.FloorToInt(sample.x * texture.width), 0, texture.width - 1);
                    int sy = Mathf.Clamp(Mathf.FloorToInt(sample.y * texture.height), 0, texture.height - 1);
                    Color32 color = atlas[sy * texture.width + sx];
                    if (splitAlpha != null)
                    {
                        int ax = Mathf.Clamp(Mathf.FloorToInt(sample.x * alphaTexture.width), 0, alphaTexture.width - 1);
                        int ay = Mathf.Clamp(Mathf.FloorToInt(sample.y * alphaTexture.height), 0, alphaTexture.height - 1);
                        color.a = splitAlpha[ay * alphaTexture.width + ax].r;
                    }
                    pixels[y * width + x] = color;
                    break;
                }
            }
        }

        private static Color32[] ReadTexture(Texture2D texture)
        {
            if (texture.isReadable) return texture.GetPixels32();
            // Read back once per sprite change, never once per frame. Source importer stays untouched.
            RenderTexture previous = RenderTexture.active;
            bool previousSRGB = GL.sRGBWrite;
            RenderTexture temporary = null;
            Texture2D copy = null;
            try
            {
                temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSRGB;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (copy != null)
                {
                    if (Application.isPlaying) Object.Destroy(copy);
                    else Object.DestroyImmediate(copy);
                }
            }
        }

        private void BuildDefault(int pixelWidth)
        {
            const int originalWidth = 33, originalHeight = 17;
            var original = new Color32[originalWidth * originalHeight];
            Color32 border = new Color32(115, 230, 255, 255);
            for (int y = 3; y < 17; y++)
            for (int x = 0; x < originalWidth; x++)
                original[y * originalWidth + x] = x == 0 || x == originalWidth - 1 || y == 3 || y == 16
                    ? border : new Color32(15, 25, 40, 225);
            for (int y = 0; y < 3; y++)
                for (int x = 7; x <= 7 + y; x++) original[y * originalWidth + x] = border;
            string[] letters = {
                "11111001000010000100001000010000100", // T
                "01110100011000111111100011000110001", // A
                "10000100001000010000100001000011111", // L
                "10001100101010011000101001001010001"  // K
            };
            for (int letter = 0; letter < letters.Length; letter++)
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 5; x++)
                if (letters[letter][y * 5 + x] == '1') original[(13 - y) * originalWidth + 4 + letter * 6 + x] = new Color32(255, 255, 255, 255);
            width = pixelWidth;
            height = Mathf.Max(1, Mathf.RoundToInt(width * (float)originalHeight / originalWidth));
            pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) pixels[y * width + x] = original[(y * originalHeight / height) * originalWidth + x * originalWidth / width];
        }

        public void DrawCentered(Color32[] destination, int screenWidth, int screenHeight)
        {
            if (pixels == null || destination == null || destination.Length != screenWidth * screenHeight) return;
            int left = (screenWidth - width) / 2, bottom = (screenHeight - height) / 2;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int dx = left + x, dy = bottom + y;
                if (dx < 0 || dy < 0 || dx >= screenWidth || dy >= screenHeight) continue;
                int index = dy * screenWidth + dx;
                destination[index] = Over(pixels[y * width + x], destination[index]);
            }
        }

        public static Color32 Over(Color32 foreground, Color32 background)
        {
            if (foreground.a == 0) return background;
            if (foreground.a == 255 || background.a == 0) return foreground;
            float a = foreground.a / 255f, b = background.a / 255f * (1 - a), alpha = a + b;
            return new Color32((byte)Mathf.RoundToInt((foreground.r * a + background.r * b) / alpha),
                (byte)Mathf.RoundToInt((foreground.g * a + background.g * b) / alpha),
                (byte)Mathf.RoundToInt((foreground.b * a + background.b * b) / alpha), (byte)Mathf.RoundToInt(alpha * 255));
        }
    }
}
