using System.Text;
using Il2CppLeTai.Asset.TranslucentImage;
using UnityEngine;

namespace OthShared
{
    /// <summary>
    /// One-line status of the game's blur system (LeTai TranslucentImage: the frosted-glass behind HUD panels and menus).
    /// Logged at points where our mods touch the UI so a regression ("everything is opaque") can be pinned to a moment.
    /// </summary>
    internal static class BlurDiag
    {
        internal static string Status()
        {
            try
            {
                var sb = new StringBuilder("blur: ");
                var sources = Resources.FindObjectsOfTypeAll<TranslucentImageSource>();
                int src = 0;
                foreach (var s in sources)
                {
                    if (s == null || !s.gameObject.scene.IsValid()) continue;
                    src++;
                    sb.Append($"[source '{s.name}' enabled={s.enabled} cam={(s.camera != null ? s.camera.name : "null")} overlay={s.isForOverlayCanvas} " +
                              $"blurredScreen={(s.blurredScreen != null)} providers={(s.activeRegionProviders != null ? s.activeRegionProviders.Count : -1)} " +
                              $"downsample={s.downsample} maxUpdateRate={s.maxUpdateRate:0.#}] ");
                }
                if (src == 0) sb.Append("NO SOURCE ");

                int imgs = 0, active = 0, noSource = 0;
                foreach (var t in Resources.FindObjectsOfTypeAll<TranslucentImage>())
                {
                    if (t == null || !t.gameObject.scene.IsValid()) continue;
                    imgs++;
                    if (t.isActiveAndEnabled) { active++; if (t.source == null) noSource++; }
                }
                sb.Append($"images={imgs} active={active} activeWithoutSource={noSource}");
                return sb.ToString();
            }
            catch (System.Exception e) { return "blur: status failed: " + e.Message; }
        }
    }
}
