using System;
using System.Collections.Generic;
using Il2CppFunCore.Style;
using Il2CppNobi.UiRoundedCorners;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OthShared
{
    /// <summary>
    /// Shared helper for building uGUI overlays that look like the game's own HUD.
    ///
    /// The look comes straight from the game's active FunStyleSheet (the same named colours and text styles the game's
    /// own HUD uses: "Typography/White", "Typography/Yellow", "Hud/Transfer/Selected", text style "Hud_Transfer_Selected" ...),
    /// panels get the game's rounded-corner component.
    /// </summary>
    internal static class GameUi
    {
        internal sealed class Look
        {
            public TMP_FontAsset Font;
            public FontStyles FontStyle = FontStyles.Normal;
            public Color Text = new Color(1f, 0.976f, 0.933f, 1f);              // Typography/White
            public Color TextDark = new Color(0.075f, 0.071f, 0.071f, 1f);       // Hud/Transfer/Selected
            public Color Panel = new Color(0f, 0f, 0f, 0.55f);
            public Color ChipOff = new Color(0f, 0f, 0f, 0.5f);
            public Color ChipOn = new Color(1f, 0.976f, 0.933f, 1f);             // Typography/White
            public Color Accent = new Color(1f, 0.847f, 0.396f, 1f);             // Typography/Yellow
        }

        internal static Look Current { get; private set; } = new Look();
        internal static bool Resolved { get; private set; }

        private static float _nextTry;
        private static bool _roundedBroken;

        // ------------------------------------------------------------------ look discovery

        /// <summary>Read the game's style sheet. Cheap to call often; throttled to once per 2 s until it succeeds.</summary>
        internal static bool TryResolve(bool force = false)
        {
            if (Resolved && !force) return true;
            if (!force && Time.realtimeSinceStartup < _nextTry) return false;
            _nextTry = Time.realtimeSinceStartup + 2f;

            var look = new Look();
            try
            {
                bool ok = FromStyleSheet(look);
                Current = look;
                Resolved = ok;
            }
            catch (Exception)
            {
                Current = look;      // keep the built-in defaults
            }
            return Resolved;
        }

        private static bool FromStyleSheet(Look look)
        {
            var asset = FunStyleAsset.Instance;
            var sheet = asset != null ? asset.GetActiveStyleSheet() : null;
            if (sheet == null) return false;

            var named = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            var colors = sheet.GetStyleElementList<FunStyleColorData>();
            if (colors != null)
                for (int i = 0; i < colors.Count; i++)
                    named[colors[i].styleName ?? ""] = colors[i].color;

            Color Pick(string name, Color fallback) => named.TryGetValue(name, out var c) ? c : fallback;
            look.Text = Opaque(Pick("Typography/White", look.Text));
            look.TextDark = Opaque(Pick("Hud/Transfer/Selected", look.TextDark));
            look.ChipOn = Opaque(Pick("Typography/White", look.ChipOn));
            look.Accent = Opaque(Pick("Typography/Yellow", look.Accent));
            var glass = Pick("Modals/UnselectedItemBackground", new Color(0f, 0f, 0f, 0.4f));   // the game's translucent panel colour
            look.Panel = new Color(glass.r, glass.g, glass.b, Mathf.Max(glass.a, 0.55f));        // no blur behind it: a bit denser
            look.ChipOff = new Color(glass.r, glass.g, glass.b, Mathf.Max(glass.a, 0.5f));

            var texts = sheet.GetStyleElementList<FunStyleTextData>();
            FunStyleTextData chosen = null;
            if (texts != null)
                for (int i = 0; i < texts.Count; i++)
                {
                    var t = texts[i];
                    if (t.fontAsset == null) continue;
                    if (chosen == null || string.Equals(t.styleName, "Hud_Transfer_Selected", StringComparison.OrdinalIgnoreCase)) chosen = t;
                }
            if (chosen != null)
            {
                look.Font = chosen.fontAsset;
                look.FontStyle = chosen.fontStyles;
            }
            return look.Font != null;
        }

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        // ------------------------------------------------------------------ builders

        /// <summary>Screen-space overlay canvas with fixed 1080p scaling.</summary>
        internal static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        /// <summary>Empty rect. Anchor/pivot are all set to <paramref name="anchor"/>; position is relative to that anchor.</summary>
        internal static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.layer = 5;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        internal static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        /// <summary>Coloured box; with <paramref name="radius"/> &gt; 0 it gets the game's own rounded-corner component.</summary>
        internal static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, float radius = 0f)
        {
            var rt = Rect(parent, name, Center, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            if (radius > 0f && !_roundedBroken)
            {
                try
                {
                    var rc = rt.gameObject.AddComponent<ImageWithRoundedCorners>();
                    rc.radius = radius;
                }
                catch (Exception)
                {
                    _roundedBroken = true;      // shader missing or similar: plain squares from now on
                }
            }
            return img;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, string text, float fontSize, Color color,
                                              Vector2 position, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(parent, name, Center, position, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            if (Current.Font != null) t.font = Current.Font;
            t.fontStyle = Current.FontStyle;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.text = text;
            return t;
        }
    }
}
