using System;
using System.Collections.Generic;
using UnityEngine;

namespace PitStriker.UI
{
    /// <summary>
    /// Procedural HUD artwork: rounded panels, circles, glows and icons drawn once into small
    /// textures from signed-distance shapes and cached. Everything is white so a single sprite
    /// can be tinted per use, and everything renders with the built-in UI shader, so nothing
    /// extra has to survive shader stripping in a player build.
    /// </summary>
    public static class HudArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Font _font;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // ------------------------------------------------------------------ sprites

        /// <summary>Rounded rectangle, 9-sliced. With <paramref name="outline"/> only the border ring is drawn.</summary>
        public static Sprite RoundedRect(int radius, float outline = 0f)
        {
            int size = radius * 2 + 4;
            return Get($"rr{radius}_{outline}", size, size, (x, y) =>
            {
                float d = SdRoundBox(x - size * 0.5f, y - size * 0.5f, size * 0.5f - 1f, size * 0.5f - 1f, radius);
                return outline > 0f ? Fill(Mathf.Abs(d + outline * 0.5f) - outline * 0.5f) : Fill(d);
            }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        public static Sprite Circle() => Get("circle", 128, 128, (x, y) => Fill(Len(x - 64, y - 64) - 62f));

        public static Sprite Ring(float thickness) => Get($"ring{thickness}", 128, 128,
            (x, y) => Fill(Mathf.Abs(Len(x - 64, y - 64) - (62f - thickness * 0.5f)) - thickness * 0.5f));

        /// <summary>Soft radial glow, brightest in the centre.</summary>
        public static Sprite Glow() => Get("glow", 128, 128, (x, y) =>
        {
            float r = Len(x - 64, y - 64) / 64f;
            return Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
        });

        /// <summary>Horizontal fade: opaque on the left, clear on the right. For the roster backing.</summary>
        public static Sprite FadeRight() => Get("fadeR", 64, 8, (x, y) =>
        {
            float t = x / 63f;
            return t < 0.55f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.55f) / 0.45f);
        });

        /// <summary>Vertical beam: bright at the bottom, gone at the top, soft at the sides.</summary>
        public static Sprite Beam() => Get("beam", 64, 128, (x, y) =>
        {
            float side = 1f - Mathf.Abs(x - 31.5f) / 32f;
            return Mathf.Pow(Mathf.Clamp01(side), 1.6f) * Mathf.Pow(1f - y / 127f, 1.3f);
        });

        public static Sprite PauseIcon() => Get("pause", 64, 64, (x, y) =>
            Fill(Mathf.Min(SdRoundBox(x - 22, y - 32, 6, 17, 2), SdRoundBox(x - 42, y - 32, 6, 17, 2))));

        /// <summary>Solid triangle pointing right (turn marker).</summary>
        public static Sprite TriangleRight() => Get("triR", 64, 64, (x, y) =>
            Fill(SdTriangle(x, y, new Vector2(14, 8), new Vector2(54, 32), new Vector2(14, 56))));

        /// <summary>Outlined triangle pointing down (target beacon).</summary>
        public static Sprite TriangleDownOutline() => Get("triDO", 128, 128, (x, y) =>
        {
            float d = SdTriangle(x, y, new Vector2(10, 112), new Vector2(118, 112), new Vector2(64, 18));
            return Fill(Mathf.Abs(d + 5f) - 5f);
        });

        public static Sprite Gear() => Get("gear", 128, 128, (x, y) =>
        {
            float dx = x - 64, dy = y - 64;
            float r = Len(dx, dy);
            float a = Mathf.Atan2(dy, dx);
            // Eight teeth: a smoothed square wave on the outer radius.
            float wave = Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f) * 0.5f + 0.5f;
            float outer = 44f + wave * 12f;
            float body = r - outer;
            float hole = 18f - r;
            return Fill(Mathf.Max(body, hole));
        });

        public static Sprite White() => Get("white", 4, 4, (x, y) => 1f);

        // ------------------------------------------------------------------ UI construction helpers

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) { img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            return img;
        }

        public static UnityEngine.UI.Text Text(string name, Transform parent, string text, int size, Color color,
                                              FontStyle style = FontStyle.Bold, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<UnityEngine.UI.Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Places a rect by its centre, in parent units, from a fixed anchor.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 centre, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centre;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Shadow(Component graphic, float alpha = 0.45f, float distance = 2f)
        {
            var s = graphic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(0f, -distance);
        }

        // ------------------------------------------------------------------ rasterising

        static Sprite Get(string key, int w, int h, Func<float, float, float> alphaAt, Vector4 border = default)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt(i + 0.5f, j + 0.5f)) * 255f);
                px[j * w + i] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);   // no CPU copy kept
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        /// <summary>Signed distance to anti-aliased coverage (1 px soft edge).</summary>
        static float Fill(float d) => Mathf.Clamp01(0.5f - d);

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float SdRoundBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - hx + r, qy = Mathf.Abs(py) - hy + r;
            return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float SdSegment(float x, float y, Vector2 a, Vector2 b)
        {
            var p = new Vector2(x, y);
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float SdTriangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            float d = Mathf.Min(SdSegment(x, y, a, b), Mathf.Min(SdSegment(x, y, b, c), SdSegment(x, y, c, a)));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }
}
