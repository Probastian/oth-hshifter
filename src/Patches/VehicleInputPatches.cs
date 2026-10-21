using System;
using HarmonyLib;
using Il2Cpp;

namespace HShifter.Patches
{
    // The game's per-vehicle input component. Only the instance with input authority (the local player's car) matters;
    // this hook just tells the controller which vehicle is ours (the controller itself runs in the mod's own frame loop).
    [HarmonyPatch(typeof(VehicleInput), nameof(VehicleInput.Update))]
    internal static class VehicleInput_Update_Patch
    {
        private static void Postfix(VehicleInput __instance)
        {
            try
            {
                if (!__instance.HasInputAuthority) return;
                ShifterController.OnVehicleUpdate(__instance);
            }
            catch (Exception e) { ShifterController.Fail(e); }
        }
    }

    // Fusion polls this once per network tick: make sure our gear (and a pending hi/lo press) is in place right before
    // the game samples its input, and take the hi/lo press away again right after, so it lasts exactly one sample.
    [HarmonyPatch(typeof(VehicleInput), nameof(VehicleInput.GetInputFromKeyboardOrController))]
    internal static class VehicleInput_GetInput_Patch
    {
        private static void Prefix(VehicleInput __instance)
        {
            try { ShifterController.PreInput(__instance); }
            catch (Exception e) { ShifterController.Fail(e); }
        }

        private static void Postfix(VehicleInput __instance)
        {
            try { ShifterController.PostInput(__instance); }
            catch (Exception e) { ShifterController.Fail(e); }
        }
    }

    // Settings > Controls: the Automatic/Manual selector gets a third option, "H-Shifter" (see ControlsMenu).
    [HarmonyPatch(typeof(StateControlsSettingsMenu), nameof(StateControlsSettingsMenu.Open))]
    internal static class ControlsMenu_Open_Patch
    {
        private static void Postfix(StateControlsSettingsMenu __instance)
        {
            try { ControlsMenu.OnPageOpen(__instance); }
            catch (Exception e) { Mod.Log.Error(e.ToString()); }
        }
    }

    [HarmonyPatch(typeof(StateControlsSettingsMenu), nameof(StateControlsSettingsMenu.ClosingCallback))]
    internal static class ControlsMenu_Closing_Patch
    {
        private static void Prefix()
        {
            try { ControlsMenu.OnPageClosing(); }
            catch (Exception e) { Mod.Log.Error(e.ToString()); }
        }
    }

    [HarmonyPatch(typeof(StateControlsSettingsMenu), nameof(StateControlsSettingsMenu.OnThrottleAndBrakeModeChanged))]
    internal static class ControlsMenu_ModeChanged_Patch
    {
        private static void Prefix(ref int option)
        {
            try { ControlsMenu.OnModeSelected(ref option); }
            catch (Exception e) { Mod.Log.Error(e.ToString()); }
        }
    }

    // While the H-shifter is on, the game's own bumper shifting would fight the H-gate (L1 is the clutch); swallow it.
    [HarmonyPatch(typeof(VehicleInput), nameof(VehicleInput.GearShiftUpPressed))]
    internal static class VehicleInput_GearUp_Patch
    {
        private static bool Prefix() => !ShifterController.BlockBumpers;
    }

    [HarmonyPatch(typeof(VehicleInput), nameof(VehicleInput.GearShiftDownPressed))]
    internal static class VehicleInput_GearDown_Patch
    {
        private static bool Prefix() => !ShifterController.BlockBumpers;
    }
}
