using MelonLoader;

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
            Log.Msg($"{ModInfo.Name} {ModInfo.Version} loaded - clutch: {Settings.ClutchButton.Value}");
        }

        public override void OnUpdate()
        {
            ControlsMenu.Pump();
            ShifterController.Frame();
        }

        public override void OnApplicationQuit()
        {
            CameraSuppressor.Set(false);
            Hud.Destroy();
        }
    }
}
