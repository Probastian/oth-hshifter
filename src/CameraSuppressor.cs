using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine.InputSystem;

namespace HShifter
{
    /// <summary>
    /// Stops the camera from orbiting while the H-shifter owns the right stick, by temporarily *disabling* the four camera
    /// actions (MoveCameraLeft/Right/Up/Down).
    ///
    /// Do NOT use binding overrides for this: changing a binding makes Unity's Input System re-resolve the whole Driving map,
    /// which resets every action in it that is currently held - the throttle trigger then reads 0 until it is released and
    /// pressed again. Disabling single actions leaves the rest of the map untouched.
    /// Note: the mouse camera uses the same actions, so it is also paused while the clutch is held.
    /// </summary>
    internal static class CameraSuppressor
    {
        private static readonly List<InputAction> Disabled = new List<InputAction>();
        private static bool _on;

        internal static bool IsOn => _on;

        private static InputAction[] CameraActions(PlayerInputActions p) => new[]
        {
            p.m_CameraOrbitAndZoom_MoveCameraLeft,
            p.m_CameraOrbitAndZoom_MoveCameraRight,
            p.m_CameraOrbitAndZoom_MoveCameraUp,
            p.m_CameraOrbitAndZoom_MoveCameraDown,
        };

        internal static void Set(bool on)
        {
            if (on == _on) return;
            try
            {
                if (on) Apply(); else Restore();
                _on = on;
            }
            catch (Exception e)
            {
                Mod.Log.Error($"CameraSuppressor.Set({on}) failed: {e}");
                _on = false;
                try { Restore(); } catch { /* best effort */ }
            }
        }

        /// <summary>Called every frame while suppressing: the game may re-enable its actions (e.g. on a map switch).</summary>
        internal static void Maintain()
        {
            if (!_on) return;
            try
            {
                foreach (var a in Disabled)
                    if (a != null && a.enabled) a.Disable();
            }
            catch (Exception e) { Mod.Log.Warning($"[camera] maintain failed: {e.Message}"); }
        }

        private static void Apply()
        {
            var pia = GameInput.PlayerInputActions;
            if (pia == null) { Mod.Log.Warning("PlayerInputActions not available; camera not suppressed."); return; }

            foreach (var action in CameraActions(pia))
            {
                if (action == null || !action.enabled) continue;
                action.Disable();
                Disabled.Add(action);
                if (Settings.VerboseLog.Value) Mod.Log.Msg($"[camera] paused {action.name}");
            }
            if (Disabled.Count == 0) Mod.Log.Warning("No enabled camera actions found - camera may still move with the stick. Press F10 for a binding dump.");
        }

        private static void Restore()
        {
            foreach (var action in Disabled)
            {
                try { if (action != null) action.Enable(); }
                catch (Exception e) { Mod.Log.Warning($"[camera] could not re-enable {action?.name}: {e.Message}"); }
            }
            Disabled.Clear();
        }

        /// <summary>Human-readable dump of the camera + driving actions' bindings (diagnostics).</summary>
        internal static string Describe()
        {
            var sb = new StringBuilder();
            try
            {
                var pia = GameInput.PlayerInputActions;
                if (pia == null) return "PlayerInputActions == null";
                var actions = new List<InputAction>(CameraActions(pia))
                {
                    pia.m_Driving_Clutch, pia.m_Driving_GearShiftUp, pia.m_Driving_GearShiftDown,
                };
                foreach (var a in actions)
                {
                    if (a == null) continue;
                    sb.Append("  ").Append(a.name).Append(a.enabled ? " (enabled): " : " (disabled): ");
                    var b = a.bindings;
                    for (int i = 0; i < b.Count; i++) sb.Append('[').Append(b[i].effectivePath).Append("] ");
                    sb.AppendLine();
                }
            }
            catch (Exception e) { sb.Append("Describe failed: ").Append(e.Message); }
            return sb.ToString();
        }
    }
}
