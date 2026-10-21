using System;
using Il2Cpp;
using UnityEngine;

namespace HShifter
{
    /// <summary>
    /// Integrates the H-shifter into the game's Settings > Controls page: the existing "Automatic / Manual" selector gets a third
    /// option, "H-Shifter". Choosing it keeps the game's own mode on Manual (the game knows nothing about a third value) and
    /// switches <see cref="Settings.Enabled"/> on; choosing Automatic or Manual switches it off.
    ///
    /// Settings.Enabled therefore *is* the "H-Shifter selected" state. It defaults to true, so the first time the mod runs the
    /// game's mode is switched to Manual (H-Shifter) automatically; after that the player's choice is persisted in HShifter.cfg.
    /// </summary>
    internal static class ControlsMenu
    {
        internal const string OptionText = "H-Shifter";
        private const int HShifterIndex = 2;

        private static StateControlsSettingsMenu _state;
        private static float _openedAt;
        private static bool _extended;
        private static bool _gameModeEnsured;
        private static bool _lastEnabled = true;
        private static float _nextEnsure;

        private static float Now => Time.realtimeSinceStartup;

        // ---------------------------------------------------------------- hooks

        internal static void OnPageOpen(StateControlsSettingsMenu state)
        {
            _state = state;
            _openedAt = Now;
            _extended = false;
        }

        internal static void OnPageClosing()
        {
            _state = null;
            _extended = false;
        }

        /// <summary>Called (via Harmony prefix) with the index the player picked in the Automatic/Manual selector. May rewrite it.</summary>
        internal static void OnModeSelected(ref int option)
        {
            if (option == HShifterIndex)
            {
                if (!Settings.Enabled.Value) Mod.Log.Msg("Controls: H-Shifter selected");
                Settings.Enabled.Value = true;
                option = (int)ControlsSettingsState.ThrottleAndBrakeBehaviour.Manual;      // the game itself stays on Manual
            }
            else
            {
                if (Settings.Enabled.Value) Mod.Log.Msg($"Controls: switched to the game's {(option == 0 ? "Automatic" : "Manual")} mode, H-Shifter off");
                Settings.Enabled.Value = false;
            }
        }

        // ---------------------------------------------------------------- per frame

        internal static void Pump()
        {
            try
            {
                EnsureGameMode();
                if (_state != null) PumpPage();
            }
            catch (Exception e)
            {
                Mod.Log.Error($"ControlsMenu.Pump failed: {e}");
                _state = null;
            }
        }

        /// <summary>H-Shifter selected => the game must be in Manual mode (also covers the very first run and a non-persisted mode).</summary>
        private static void EnsureGameMode()
        {
            bool enabled = Settings.Enabled.Value;
            if ((_gameModeEnsured && enabled == _lastEnabled) || Now < _nextEnsure) return;      // once per session, and again when switched on
            _nextEnsure = Now + 1f;
            var cs = ControlsSettingsState.Instance;
            if (cs == null) return;
            _gameModeEnsured = true;
            _lastEnabled = enabled;
            if (!enabled) return;
            if (cs.ThrottleAndBrakeMode != ControlsSettingsState.ThrottleAndBrakeBehaviour.Manual)
            {
                cs.ThrottleAndBrakeMode = ControlsSettingsState.ThrottleAndBrakeBehaviour.Manual;
                Mod.Log.Msg("Gearbox mode set to Manual for the H-Shifter (Settings > Controls shows it as 'H-Shifter')");
            }
        }

        private static void PumpPage()
        {
            var ctx = _state.controlsSettingsMenuUIContext;
            var sel = ctx != null ? ctx.ThrottleAndBrakeModeSelector : null;
            var carousel = sel != null ? sel.carouselOption : null;
            if (carousel == null)
            {
                if (Now - _openedAt > 10f) { Mod.Log.Warning("Controls page: the Automatic/Manual selector was not found - no H-Shifter option added."); _state = null; }
                return;
            }

            if (!_extended)
            {
                var opts = carousel.options;
                if (opts == null || opts.Count < 2) return;                   // not initialised yet
                var list = new Il2CppSystem.Collections.Generic.List<string>();
                bool has = false;
                for (int i = 0; i < opts.Count; i++) { list.Add(opts[i]); if (opts[i] == OptionText) has = true; }
                if (!has) list.Add(OptionText);
                carousel.SetOptions(list);
                _extended = true;
                Mod.Log.Msg($"Controls page: added the '{OptionText}' option to the Automatic/Manual selector ({list.Count} options)");
            }

            // keep the selector showing H-Shifter whenever it is the active choice (the game resets it to the plain mode on refresh)
            int cur = carousel.currentSelected;
            bool manual = ControlsSettingsState.Instance != null &&
                          ControlsSettingsState.Instance.ThrottleAndBrakeMode == ControlsSettingsState.ThrottleAndBrakeBehaviour.Manual;
            if (Settings.Enabled.Value && manual && cur != HShifterIndex) carousel.SetSelected(HShifterIndex);
            else if (cur == HShifterIndex && !Settings.Enabled.Value) carousel.SetSelected(manual ? 1 : 0);
        }
    }
}
