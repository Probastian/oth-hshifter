using MelonLoader;

[assembly: MelonInfo(typeof(HShifter.Mod), HShifter.ModInfo.Name, HShifter.ModInfo.Version, HShifter.ModInfo.Author)]
[assembly: MelonGame("Funselektor", "over the hill")]

namespace HShifter
{
    internal static class ModInfo
    {
        public const string Name = "HShifter";
        public const string Version = "0.7.2";
        public const string Author = "Probastian";
    }
}
