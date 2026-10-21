using MelonLoader;
using UnityEngine.InputSystem;

namespace HShifter
{
    public class Mod : MelonMod
    {
        internal static Mod Instance { get; private set; }
        internal static MelonLogger.Instance Log => Instance.LoggerInstance;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Settings.Init();
            ShifterController.UserEnabled = Settings.StartActive.Value;
            Log.Msg($"{ModInfo.Name} {ModInfo.Version} loaded - toggle: {Settings.ToggleKey.Value}, clutch: {Settings.ClutchButton.Value}, diagnostics: F10");
        }

        public override void OnUpdate()
        {
            ControlsMenu.Pump();
            ShifterController.Frame();

            var kb = Keyboard.current;
            if (kb == null) return;

            var toggle = Settings.ParseToggleKey();
            if (toggle.HasValue && kb[toggle.Value].wasPressedThisFrame)
            {
                ShifterController.UserEnabled = !ShifterController.UserEnabled;
                Log.Msg($"H-shifter {(ShifterController.UserEnabled ? "ON" : "OFF")}");
            }
            if (kb.f10Key.wasPressedThisFrame) ShifterController.DumpDiagnostics();
        }

        public override void OnApplicationQuit()
        {
            CameraSuppressor.Set(false);
            Hud.Destroy();
        }
    }
}
