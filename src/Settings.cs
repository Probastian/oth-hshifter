using System;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine.InputSystem.LowLevel;

namespace HShifter
{
    /// <summary>
    /// Persistent settings, saved to UserData\HShifter.cfg (MelonPreferences / TOML).
    /// Every entry can also be edited in-game with the ModConfigurator mod.
    /// </summary>
    internal static class Settings
    {
        private static MelonPreferences_Category _cat;

        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<string> ClutchButton;
        internal static MelonPreferences_Entry<bool> UseTransferCase;
        internal static MelonPreferences_Entry<float> StickDeadzone;
        internal static MelonPreferences_Entry<float> LeverSpeed;
        internal static MelonPreferences_Entry<bool> ReverseOnRight;
        internal static MelonPreferences_Entry<bool> SuppressCameraStick;
        internal static MelonPreferences_Entry<bool> ForceActive;
        internal static MelonPreferences_Entry<bool> ShowHud;
        internal static MelonPreferences_Entry<float> HudScale;
        internal static MelonPreferences_Entry<float> HudBottomOffset;
        internal static MelonPreferences_Entry<bool> VerboseLog;

        internal static void Init()
        {
            _cat = MelonPreferences.CreateCategory(ModInfo.Name, "H-Shifter");
            _cat.SetFilePath($"UserData/{ModInfo.Name}.cfg");

            Enabled = _cat.CreateEntry("Enabled", true, "Use H-Shifter", "Same as choosing H-Shifter in Settings > Controls (Automatic / Manual / H-Shifter). On by default: the first time the mod runs it switches the game to its Manual gearbox mode and selects H-Shifter.");
            ClutchButton = _cat.CreateEntry("ClutchButton", "LeftShoulder", "Clutch button", "Gamepad button that acts as the clutch: hold it to shift (gear drops to N, right stick moves the lever), release to engage. LeftShoulder = L1.");
            UseTransferCase = _cat.CreateEntry("UseTransferCase", true, "Hi/Lo range columns", "Add High/Low range columns (1L 2L 1H 2H) and switch the transfer case when shifting into them.");
            StickDeadzone = _cat.CreateEntry("StickDeadzone", 0.25f, "Stick deadzone", "Stick magnitude below this counts as 'released'.",
                validator: new ValueRange<float>(0.05f, 0.6f));
            LeverSpeed = _cat.CreateEntry("LeverSpeed", 15f, "Lever speed", "How fast the gear lever moves when the right stick is pushed fully, equally fast sideways and up/down (higher = faster).",
                validator: new ValueRange<float>(1f, 40f));
            ReverseOnRight = _cat.CreateEntry("ReverseOnRight", true, "Reverse on the right", "Put the reverse column at the right end of the gate (off = left end).");
            SuppressCameraStick = _cat.CreateEntry("SuppressCameraStick", true, "Right stick doesn't move camera", "While the clutch is held the camera is paused so the right stick can't move it (the mouse camera is paused too).");
            ForceActive = _cat.CreateEntry("ForceActive", false, "Force on every vehicle", "Also run on vehicles whose gearbox is not reported as Manual.");
            ShowHud = _cat.CreateEntry("ShowHud", true, "Show gate overlay", "Draw the H-gate overlay while the clutch is held.");
            HudScale = _cat.CreateEntry("HudScale", 0.9f, "Overlay scale", "Overlay size multiplier.", validator: new ValueRange<float>(0.4f, 2.5f));
            HudBottomOffset = _cat.CreateEntry("HudBottomOffset", 36f, "Overlay bottom offset", "Distance of the overlay from the bottom screen edge, in HUD pixels (1080p reference).",
                validator: new ValueRange<float>(0f, 600f));
            VerboseLog = _cat.CreateEntry("VerboseLog", false, "Verbose log", "Log clutch, gear and transfer-case changes to the MelonLoader console.");
        }

        internal static GamepadButton ParseClutchButton()
        {
            return Enum.TryParse<GamepadButton>(ClutchButton.Value, true, out var b) ? b : GamepadButton.LeftShoulder;
        }
    }
}
