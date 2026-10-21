using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppVehiclePhysics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HShifter
{
    /// <summary>
    /// Shifts the local player's vehicle with the H-gate.
    ///
    /// Clutch held (default L1): the car is put in N and the right stick moves the gear lever along the H-gate rails.
    /// Clutch released: the gear the lever is in is engaged immediately and stays engaged, whatever the stick does afterwards.
    ///
    /// How gears are changed (the game ignores VehicleInput.selectedGear on a gamepad - that field is for steering-wheel shifters):
    ///  * when this client owns the car's networked state (single player / hosting) the car's SelectedManualGear is set directly,
    ///    which allows any gear incl. N at once;
    ///  * otherwise the game's own gear-up / gear-down button presses are sent (one input sample each), exactly like the bumpers do.
    /// The hi/lo transfer case is switched with the game's own hi/lo button press.
    ///
    /// The state machine runs in the mod's own frame loop (<see cref="Frame"/>), reading every connected gamepad.
    /// </summary>
    internal static class ShifterController
    {
        internal static readonly HGate Gate = new HGate();

        /// <summary>Toggled by the user (ToggleKey).</summary>
        internal static bool UserEnabled = true;
        /// <summary>The shifter owns gear selection for the local vehicle (manual gearbox + gamepad + gameplay).</summary>
        internal static bool Engaged { get; private set; }
        /// <summary>The clutch is held right now (stick moves the lever, car is in N).</summary>
        internal static bool ClutchHeld { get; private set; }
        internal static bool BlockBumpers => Engaged;

        internal static VehicleInput Bound { get; private set; }

        private static bool _usable;
        private static bool _hiLo;
        private static string _vehicleName = "?";
        private static NetworkedVehicleStatus _status;
        private static FunselektorFusionVPStandardInput _fusionInput;

        private static int _engagedGear;                 // gear we want engaged while the clutch is up
        private static bool _wantLowRange;
        private static bool _rangePending;
        private static float _rangeRequestedAt;
        private static int _rangeAttempts;

        // one-sample button presses (hi/lo, gear up, gear down)
        private static int _requestedBit = -1, _inFlightBit = -1;
        private static int _sampleCounter, _lastPulseSample = -10;

        // gear changing
        private static float _directSince;               // when direct setting of the gear started for the current target
        private static bool _directFailed;
        private static int _assumedGear;
        private static float _lastStepAt = -10f;
        private static int _stepCount;
        private static bool _buttonsGaveUp;

        private static int _lastSelected = int.MinValue;
        private static int _lastStatusGear = int.MinValue;
        private static int _lastManualGear = int.MinValue;
        private static int _lastStatusLow = -1;
        private static string _layoutSig = "";
        private static string _lastStateKey = "";
        private static float _lastStateLog;
        private static float _nextDiscover;
        private static int _faults;
        private static int _fwd = 6, _rev = 1;

        private const int ToggleHiLowBit = (int)NetworkInputData.VehicleButtons.ToggleHiLow;
        private const int GearUpBit = (int)NetworkInputData.VehicleButtons.GearUp;
        private const int GearDownBit = (int)NetworkInputData.VehicleButtons.GearDown;

        // maps in which the player is clearly not driving
        private static readonly HashSet<GameInput.Map> MenuMaps = new HashSet<GameInput.Map>
        {
            GameInput.Map.UI, GameInput.Map.Terminal, GameInput.Map.GameHasNoFocus, GameInput.Map.CutscenePlaying,
            GameInput.Map.LoadingScreen, GameInput.Map.FreeCamera, GameInput.Map.PhotoModeFreeCamera, GameInput.Map.PhotoModeUI,
            GameInput.Map.RadialMenuNavigation, GameInput.Map.MapScreen, GameInput.Map.MusicMenu,
        };

        private static float Now => Time.realtimeSinceStartup;

        // ---------------------------------------------------------------- game hooks

        /// <summary>From the game's VehicleInput.Update (local car only): the cheapest way to learn which vehicle is ours.</summary>
        internal static void OnVehicleUpdate(VehicleInput vi)
        {
            if (!SameObject(vi, Bound)) Bind(vi);
        }

        /// <summary>Called right before the game samples its input: put a requested one-sample button press into it.</summary>
        internal static void PreInput(VehicleInput vi)
        {
            if (!Engaged || !SameObject(vi, Bound)) return;
            _sampleCounter++;
            if (_requestedBit >= 0 && _sampleCounter - _lastPulseSample >= 2)      // at least one clear sample between two presses
            {
                SetBit(vi, _requestedBit, true);
                _inFlightBit = _requestedBit;
                _requestedBit = -1;
                _lastPulseSample = _sampleCounter;
            }
        }

        /// <summary>Called right after the game sampled its input: the press lasts exactly one sample.</summary>
        internal static void PostInput(VehicleInput vi)
        {
            if (_inFlightBit < 0 || !SameObject(vi, Bound)) return;
            SetBit(vi, _inFlightBit, false);
            _inFlightBit = -1;
        }

        private static void RequestBit(int bit) { if (_requestedBit < 0 && _inFlightBit < 0) _requestedBit = bit; }

        private static void SetBit(VehicleInput vi, int bit, bool on)
        {
            var b = vi.buttons;
            b.Set(bit, on);
            vi.buttons = b;
        }

        // ---------------------------------------------------------------- per-frame (mod's own loop)

        internal static void Frame()
        {
            if (_faults > 20) return;
            try { FrameInner(); }
            catch (Exception e) { Fail(e); }
        }

        private static void FrameInner()
        {
            // 1. which vehicle is ours?
            if (Bound == null) { Bound = null; Discover(); }
            if (Bound == null) { SetEngaged(false); return; }
            var vi = Bound;

            // 2. should we own the gearbox right now?
            string reason = !Settings.Enabled.Value ? "disabled"
                          : !UserEnabled ? "toggled-off"
                          : !_usable ? "gearbox-not-manual"
                          : NotGameplayReason();
            SetEngaged(reason == null);
            LogState(reason);
            Diagnose(vi);
            if (!Engaged) { Hud.Tick(false); return; }

            ReconfigureIfNeeded();

            // 3. clutch + stick (union of all connected pads)
            bool clutch = AnyPressed(Settings.ParseClutchButton());
            if (clutch && !ClutchHeld) BeginShift();
            else if (!clutch && ClutchHeld) EndShift();
            ClutchHeld = clutch;

            if (clutch)
            {
                CameraSuppressor.Maintain();
                Gate.Move(ReadRightStick(), Time.unscaledDeltaTime, Settings.LeverSpeed.Value, Settings.StickDeadzone.Value);
            }
            LogThrottleAfterRelease(vi);

            TrackRange();
            DriveGear();
            Hud.Tick(clutch && Settings.ShowHud.Value);
        }

        // ---------------------------------------------------------------- gear changing

        private static int ActualGear()
        {
            if (_fusionInput != null) return _fusionInput.SelectedManualGear;
            return _status != null ? _status.GearboxGear : 0;
        }

        private static bool HasAuthority()
        {
            try { return _fusionInput != null && _fusionInput.HasStateAuthority; }
            catch { return false; }
        }

        /// <summary>Makes the car's gear equal to what the clutch / lever say it should be (N while the clutch is held).</summary>
        private static void DriveGear()
        {
            int target = ClutchHeld ? 0 : _engagedGear;
            int actual = ActualGear();
            if (actual == target) { _directSince = 0f; _stepCount = 0; return; }

            if (HasAuthority() && !_directFailed)
            {
                if (_directSince == 0f) _directSince = Now;
                _fusionInput.SelectedManualGear = target;
                if (Settings.VerboseLog.Value) Mod.Log.Msg($"[gear] set directly {actual} -> {target}");

                if (Now - _directSince > 1.0f)
                {
                    _directFailed = true;       // writing the networked value does not stick: use button presses instead
                    Mod.Log.Warning("[gear] direct gear change did not take effect - falling back to gear up/down presses");
                }
                return;
            }

            StepWithButtons(actual, target);
        }

        private static void StepWithButtons(int actual, int target)
        {
            if (_buttonsGaveUp || _requestedBit >= 0 || _inFlightBit >= 0) return;

            if (Now - _lastStepAt < 0.30f) actual = _assumedGear;       // presses just sent are not reflected in the car yet
            else _assumedGear = actual;
            if (actual == target) return;

            if (_stepCount >= 10)
            {
                _buttonsGaveUp = true;
                Mod.Log.Warning($"[gear] could not reach gear {target} with gear up/down presses (stuck at {actual}) - giving up for this vehicle");
                if (!ClutchHeld) _engagedGear = actual;
                return;
            }

            int dir = target > actual ? 1 : -1;
            RequestBit(dir > 0 ? GearUpBit : GearDownBit);
            _assumedGear = actual + dir;
            _lastStepAt = Now;
            _stepCount++;
            if (Settings.VerboseLog.Value) Mod.Log.Msg($"[gear] press gear {(dir > 0 ? "up" : "down")} ({actual} -> {target})");
        }

        // ---------------------------------------------------------------- input devices

        private static bool AnyPressed(GamepadButton button)
        {
            var all = Gamepad.all;
            for (int i = 0; i < all.Count; i++)
            {
                var pad = all[i];
                if (pad != null && pad[button].isPressed) return true;
            }
            var cur = Gamepad.current;
            return cur != null && cur[button].isPressed;
        }

        private static Vector2 ReadRightStick()
        {
            Vector2 best = Vector2.zero;
            var all = Gamepad.all;
            for (int i = 0; i < all.Count; i++)
            {
                var pad = all[i];
                if (pad == null) continue;
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > best.sqrMagnitude) best = s;
            }
            return best;
        }

        // ---------------------------------------------------------------- shifting

        private static void BeginShift()
        {
            var range = !_hiLo ? GearRange.None : (CurrentLow() ? GearRange.Low : GearRange.High);
            Gate.Begin(_engagedGear, range);
            CameraSuppressor.Set(Settings.SuppressCameraStick.Value);
            if (Settings.VerboseLog.Value) Mod.Log.Msg($"[clutch] pressed - gear {_engagedGear} range {range} -> N (lever at {Gate.LeverX:0.0},{Gate.LeverY:0.0})");
        }

        private static void EndShift()
        {
            var d = Gate.Desired;
            _engagedGear = d.Gear;
            if (_hiLo && d.Range != GearRange.None)
            {
                bool wantLow = d.Range == GearRange.Low;
                if (wantLow != CurrentLow())
                {
                    _wantLowRange = wantLow;
                    _rangePending = true;
                    _rangeAttempts = 0;
                    _rangeRequestedAt = -10f;
                }
            }
            CameraSuppressor.Set(false);
            _releasedAt = Now;
            _throttleLogStage = 0;
            if (Settings.VerboseLog.Value) Mod.Log.Msg($"[clutch] released - engaging {d.Label} (gear {d.Gear}, range {d.Range})");
        }

        // Diagnostics for "throttle held through a shift is ignored": compares the trigger with what the game's input took.
        private static float _releasedAt = -10f;
        private static int _throttleLogStage = 2;

        private static void LogThrottleAfterRelease(VehicleInput vi)
        {
            if (!Settings.VerboseLog.Value || ClutchHeld || _throttleLogStage >= 2) return;
            float since = Now - _releasedAt;
            if (since < (_throttleLogStage == 0 ? 0.15f : 0.8f)) return;
            _throttleLogStage++;
            float trigger = 0f;
            var pad = Gamepad.current;
            if (pad != null) trigger = pad.rightTrigger.ReadValue();
            Mod.Log.Msg($"[throttle] {since:0.00}s after release: right trigger={trigger:0.00}, game prevFrameThrottle={vi.prevFrameThrottle:0.00}");
        }

        /// <summary>Keeps pulsing the hi/lo toggle until the vehicle reports the wanted range (or gives up).</summary>
        private static void TrackRange()
        {
            if (!_rangePending) return;
            if (CurrentLow() == _wantLowRange)
            {
                _rangePending = false;
                if (Settings.VerboseLog.Value) Mod.Log.Msg($"[range] transfer case now {(CurrentLow() ? "LOW" : "HIGH")}");
                return;
            }
            if (Now - _rangeRequestedAt < 0.4f) return;
            if (_rangeAttempts >= 4)
            {
                _rangePending = false;
                Mod.Log.Warning($"[range] transfer case did not switch to {(_wantLowRange ? "LOW" : "HIGH")} after 4 tries (is it blocked while moving / in gear?)");
                return;
            }
            if (_requestedBit >= 0 || _inFlightBit >= 0) return;      // a gear press is on its way: wait for a free sample
            _rangeAttempts++;
            _rangeRequestedAt = Now;
            RequestBit(ToggleHiLowBit);
            if (Settings.VerboseLog.Value) Mod.Log.Msg($"[range] toggling hi/lo (try {_rangeAttempts})");
        }

        private static bool CurrentLow() => _status != null && _status.GearboxIsLowTransferCase;

        // ---------------------------------------------------------------- state

        /// <summary>Null when the player is driving with a gamepad; otherwise why the shifter must stay out of the way.</summary>
        private static string NotGameplayReason()
        {
            try
            {
                if (Gamepad.all.Count == 0 && Gamepad.current == null) return "no-gamepad";
                if (GameInput.ControlScheme != GameInput.ControlSchemeType.Gamepad && GameInput.LastInputSource != GameInput.InputSource.Gamepad)
                    return "scheme:" + GameInput.ControlScheme;
                var map = GameInput.CurrentMap;
                if (map != null && map.HasValue && MenuMaps.Contains(map.Value)) return "map:" + map.Value;
                if (Time.timeScale < 0.001f) return "paused";
                return null;
            }
            catch (Exception e) { return "error:" + e.Message; }
        }

        private static void LogState(string reason)
        {
            if (!Settings.VerboseLog.Value) return;
            string map = "?", scheme = "?", src = "?", inputEnabled = "?";
            try
            {
                var m = GameInput.CurrentMap; map = m != null && m.HasValue ? m.Value.ToString() : "none";
                scheme = GameInput.ControlScheme.ToString();
                src = GameInput.LastInputSource.ToString();
                if (_fusionInput != null) inputEnabled = ((bool)_fusionInput.inputIsEnabled).ToString();
            }
            catch { /* diagnostics only */ }
            string key = $"{reason ?? "ok"}|{map}|{scheme}|{src}|{inputEnabled}|{Time.timeScale > 0.001f}";
            if (key == _lastStateKey || Now - _lastStateLog < 0.25f) return;
            _lastStateKey = key;
            _lastStateLog = Now;
            Mod.Log.Msg($"[state] {(reason == null ? "ENGAGED" : "off: " + reason)}  map={map} scheme={scheme} lastInput={src} vehicleInputEnabled={inputEnabled} timeScale={Time.timeScale:0.##}");
        }

        private static void SetEngaged(bool on)
        {
            if (on == Engaged) return;
            Engaged = on;
            if (on)
            {
                _engagedGear = ActualGear();
                _rangePending = false;
                _directSince = 0f;
                _stepCount = 0;
                Mod.Log.Msg($"H-shifter ON for {_vehicleName} (gear {_engagedGear}, authority={HasAuthority()})");
            }
            else
            {
                ClutchHeld = false;
                _requestedBit = -1;
                _rangePending = false;
                CameraSuppressor.Set(false);
                Hud.Tick(false);
                Mod.Log.Msg("H-shifter off");
            }
        }

        internal static void ForceOff() { SetEngaged(false); CameraSuppressor.Set(false); Hud.Tick(false); }

        internal static void Fail(Exception e)
        {
            _faults++;
            Mod.Log.Error($"[fault {_faults}/20] {e}");
            if (_faults > 20)
            {
                Mod.Log.Error("Too many faults - H-shifter disabled for this session.");
                ForceOff();
            }
        }

        private static bool SameObject(VehicleInput a, VehicleInput b) =>
            a != null && b != null && a.Pointer == b.Pointer;

        // ---------------------------------------------------------------- binding / diagnostics

        /// <summary>Fallback discovery of the local car if the game's Update hook has not told us about it.</summary>
        private static void Discover()
        {
            if (Now < _nextDiscover) return;
            _nextDiscover = Now + 1f;
            var all = UnityEngine.Object.FindObjectsOfType<VehicleInput>();
            foreach (var vi in all)
                if (vi != null && vi.HasInputAuthority) { Bind(vi); return; }
        }

        private static void Bind(VehicleInput vi)
        {
            ForceOff();
            Bound = vi;
            _usable = false;
            _status = null;
            _fusionInput = null;
            _directFailed = false;
            _buttonsGaveUp = false;
            _requestedBit = _inFlightBit = -1;
            _lastSelected = _lastStatusGear = _lastManualGear = int.MinValue;
            _lastStatusLow = -1;
            _layoutSig = "";
            _vehicleName = vi.gameObject != null ? vi.gameObject.name : "?";

            var vpc = vi.GetComponentInParent<VPVehicleController>();
            if (vpc == null) vpc = vi.transform.root.GetComponentInChildren<VPVehicleController>(true);
            _status = vi.GetComponentInParent<NetworkedVehicleStatus>();
            if (_status == null) _status = vi.transform.root.GetComponentInChildren<NetworkedVehicleStatus>(true);
            _fusionInput = vi.GetComponentInParent<FunselektorFusionVPStandardInput>();
            if (_fusionInput == null) _fusionInput = vi.transform.root.GetComponentInChildren<FunselektorFusionVPStandardInput>(true);

            _fwd = 6; _rev = 1;
            string type = "unknown";
            if (vpc != null && vpc.gearbox != null)
            {
                var gs = vpc.gearbox;
                type = gs.type.ToString();
                _fwd = gs.forwardGearRatios != null ? gs.forwardGearRatios.Length : _fwd;
                _rev = gs.reverseGearRatios != null ? gs.reverseGearRatios.Length : _rev;
                _usable = gs.type == Gearbox.Type.Manual;
            }
            if (Settings.ForceActive.Value) _usable = true;

            ReconfigureIfNeeded();

            Mod.Log.Msg($"[bind] vehicle='{_vehicleName}' vpc={(vpc != null)} status={(_status != null)} fusionInput={(_fusionInput != null)} gearboxType={type} " +
                        $"forward={_fwd} reverse={_rev} gear={ActualGear()} stateAuthority={HasAuthority()} usable={_usable} hiLo={_hiLo} columns={Gate.ColumnCount}");
            if (!_usable)
                Mod.Log.Msg("[bind] This vehicle's gearbox is not reported as Manual, so the H-shifter stays off. Set ForceActive=true in UserData\\HShifter.cfg to override.");
        }

        /// <summary>Rebuild the gate when a layout-relevant setting changed (e.g. edited in the configurator).</summary>
        private static void ReconfigureIfNeeded()
        {
            _hiLo = Settings.UseTransferCase.Value && _status != null;
            string sig = $"{_fwd}/{_rev}/{_hiLo}/{Settings.ReverseOnRight.Value}";
            if (sig == _layoutSig) return;
            _layoutSig = sig;
            Gate.Configure(_fwd, _rev, _hiLo, Settings.ReverseOnRight.Value);
        }

        private static void Diagnose(VehicleInput vi)
        {
            if (!Settings.VerboseLog.Value) return;

            int sel = vi.selectedGear;
            if (sel != _lastSelected) { Mod.Log.Msg($"[diag] vehicleInput.selectedGear {_lastSelected} -> {sel}"); _lastSelected = sel; }
            if (_fusionInput != null)
            {
                int m = _fusionInput.SelectedManualGear;
                if (m != _lastManualGear) { Mod.Log.Msg($"[diag] car SelectedManualGear {_lastManualGear} -> {m} (engaged: {Engaged}, clutch: {ClutchHeld})"); _lastManualGear = m; }
            }
            if (_status != null)
            {
                int g = _status.GearboxGear;
                if (g != _lastStatusGear) { Mod.Log.Msg($"[diag] vehicle status gearboxGear {_lastStatusGear} -> {g}"); _lastStatusGear = g; }
                int low = _status.GearboxIsLowTransferCase ? 1 : 0;
                if (low != _lastStatusLow) { Mod.Log.Msg($"[diag] vehicle status lowRange {_lastStatusLow} -> {low}"); _lastStatusLow = low; }
            }
        }

        internal static void DumpDiagnostics()
        {
            Mod.Log.Msg("---- HShifter diagnostics ----");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Gamepad.all.Count; i++) sb.Append(Gamepad.all[i]?.displayName).Append(i == Gamepad.all.Count - 1 ? "" : ", ");
            Mod.Log.Msg($"enabled={Settings.Enabled.Value} userEnabled={UserEnabled} engaged={Engaged} clutch={ClutchHeld} usable={_usable} hiLo={_hiLo} " +
                        $"vehicle='{_vehicleName}' pads=[{sb}] clutchButton={Settings.ParseClutchButton()} notGameplayReason={NotGameplayReason() ?? "none"}");
            Mod.Log.Msg($"gear: actual={ActualGear()} engagedTarget={_engagedGear} stateAuthority={HasAuthority()} directFailed={_directFailed} buttonsGaveUp={_buttonsGaveUp}");
            Mod.Log.Msg($"gate: forward={Gate.Forward} reverse={Gate.Reverse} columns={Gate.ColumnCount} lever=({Gate.LeverX:0.00},{Gate.LeverY:0.00}) desired={Gate.Desired.Label}");
            for (int i = 0; i < Gate.Columns.Count; i++)
                Mod.Log.Msg($"  col {i}: up={Gate.Columns[i].Up.Label} down={Gate.Columns[i].Down.Label}");
            Mod.Log.Msg("input bindings:\n" + CameraSuppressor.Describe());
            Mod.Log.Msg("ui look:\n" + OthShared.GameUi.DumpLook());
            Mod.Log.Msg(OthShared.BlurDiag.Status());
            Mod.Log.Msg("------------------------------");
        }
    }
}
