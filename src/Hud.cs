using System.Collections.Generic;
using System.Text;
using Il2CppTMPro;
using OthShared;
using UnityEngine;
using UnityEngine.UI;

namespace HShifter
{
    /// <summary>
    /// H-gate overlay, drawn with the game's own HUD look (font, panel sprite/colour, on/off chip colours - see GameUi).
    /// Bottom centre of the screen; visible only while the clutch is held. Shows the gate rails, one chip per gear slot
    /// (lit while the lever is in it) and the lever itself. There is no "N" chip: neutral is simply the lever not being in a slot.
    /// </summary>
    internal static class Hud
    {
        private sealed class Chip
        {
            public Slot Slot;
            public Image Bg;
            public TextMeshProUGUI Text;
            public bool Lit;
        }

        private const float ChipW = 64f, ChipH = 42f, ColStep = 96f, RowStep = 54f, Pad = 26f, Header = 30f, LeverSize = 20f;

        private static GameObject _canvas;
        private static RectTransform _root, _lever;
        private static readonly List<Chip> Chips = new List<Chip>();
        private static string _builtSignature;
        private static bool _builtWithHud;
        private static float _builtScale = -1f, _builtOffset = -1f;
        private static int _cols;
        private static float _gateCenterY;

        internal static void Tick(bool visible)
        {
            if (!visible)
            {
                if (_canvas != null && _canvas.activeSelf) _canvas.SetActive(false);
                return;
            }

            GameUi.TryResolve();
            var gate = ShifterController.Gate;
            string sig = Signature(gate);
            if (_canvas == null || sig != _builtSignature || (GameUi.Resolved && !_builtWithHud) ||
                _builtScale != Settings.HudScale.Value || _builtOffset != Settings.HudBottomOffset.Value)
                Build(gate, sig);

            if (!_canvas.activeSelf) _canvas.SetActive(true);
            Refresh(gate);
        }

        internal static void Destroy()
        {
            if (_canvas != null) Object.Destroy(_canvas);
            _canvas = null;
            _root = null;
            _lever = null;
            Chips.Clear();
        }

        private static string Signature(HGate g)
        {
            var sb = new StringBuilder();
            foreach (var c in g.Columns) sb.Append(c.Up.Label).Append('/').Append(c.Down.Label).Append(';');
            return sb.ToString();
        }

        private static float X(int cols, float colIndex) => (colIndex - (cols - 1) / 2f) * ColStep;

        private static void Build(HGate gate, string sig)
        {
            Destroy();
            var look = GameUi.Current;
            _builtSignature = sig;
            _builtWithHud = GameUi.Resolved;
            _builtScale = Settings.HudScale.Value;
            _builtOffset = Settings.HudBottomOffset.Value;

            int cols = gate.ColumnCount;
            _cols = cols;
            float width = (cols - 1) * ColStep + ChipW + 2f * Pad;
            float height = 2f * RowStep + ChipH + 2f * Pad + Header;
            _gateCenterY = -Header / 2f;

            _canvas = GameUi.CreateCanvas("HShifterHud", 32000);
            _root = GameUi.Rect(_canvas.transform, "Root", new Vector2(0.5f, 0f), new Vector2(0f, _builtOffset), new Vector2(width, height));
            _root.localScale = Vector3.one * _builtScale;

            GameUi.Box(_root, "Panel", Vector2.zero, new Vector2(width, height), look.Panel, 14f);
            GameUi.Label(_root, "Header", "CLUTCH", 20f, new Color(look.Text.r, look.Text.g, look.Text.b, 0.65f),
                         new Vector2(0f, height / 2f - Pad + 4f), new Vector2(200f, 26f));

            // rails: neutral channel + one rail per column (only the half that has a gear)
            var railColor = new Color(look.Text.r, look.Text.g, look.Text.b, 1f);          // opaque white, like the idle chips
            if (cols > 1)
                GameUi.Box(_root, "Channel", new Vector2(0f, _gateCenterY), new Vector2((cols - 1) * ColStep, 5f), railColor);
            for (int i = 0; i < cols; i++)
            {
                var col = gate.Columns[i];
                float min = col.RailMin, max = col.RailMax;
                if (max - min <= 0f) continue;
                GameUi.Box(_root, "Rail" + i, new Vector2(X(cols, i), _gateCenterY + (min + max) / 2f * RowStep), new Vector2(5f, (max - min) * RowStep), railColor);
            }

            // the lever is created before the chips so it sits under them: at a slot it disappears behind the lit chip
            var lever = GameUi.Box(_root, "Lever", Vector2.zero, new Vector2(LeverSize, LeverSize), look.Accent, LeverSize / 2f);    // radius = half size: a circle
            _lever = lever.rectTransform;

            for (int i = 0; i < cols; i++)
            {
                AddChip(gate.Columns[i].Up, X(cols, i), _gateCenterY + RowStep);
                AddChip(gate.Columns[i].Down, X(cols, i), _gateCenterY - RowStep);
            }
        }

        private static void AddChip(Slot slot, float x, float y)
        {
            if (!slot.Exists) return;
            var look = GameUi.Current;
            // idle chips: opaque white with dark text; the active one turns yellow (see SetLit)
            var bg = GameUi.Box(_root, "Chip" + slot.Label, new Vector2(x, y), new Vector2(ChipW, ChipH), look.ChipOn, 8f);
            var txt = GameUi.Label(bg.transform, "Text", slot.Label, 26f, look.TextDark, Vector2.zero, new Vector2(ChipW, ChipH));
            Chips.Add(new Chip { Slot = slot, Bg = bg, Text = txt });
        }

        private static void Refresh(HGate gate)
        {
            var look = GameUi.Current;
            var desired = gate.Desired;

            foreach (var c in Chips)
                SetLit(c, desired.Exists && c.Slot.Gear == desired.Gear && c.Slot.Range == desired.Range, look);

            _lever.anchoredPosition = new Vector2(X(_cols, gate.LeverX), _gateCenterY + gate.LeverY * RowStep);
        }

        private static void SetLit(Chip c, bool lit, GameUi.Look look)
        {
            if (c.Lit == lit) return;
            c.Lit = lit;
            c.Bg.color = lit ? look.Accent : look.ChipOn;       // yellow (same as the lever) when active, opaque white otherwise
            c.Text.color = look.TextDark;
        }
    }
}
