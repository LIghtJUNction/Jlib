using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace Jlib.Runtime {
    /// <summary>The single installed provider of the PLib-compatible shared assembly.</summary>
    public sealed class JlibMod : KMod.UserMod2 {
        public override void OnLoad(Harmony harmony) {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            JlibDispatcher.EnsureInitialized();
            new POptions().RegisterOptions(this, typeof(JlibOptions));
            PUtil.LogDebug("Jlib provider " + JlibApi.StaticId + " " + JlibApi.Version +
                " ABI " + JlibApi.AbiVersion + " protocol " + JlibApi.PLibVersion);
        }
    }
}
