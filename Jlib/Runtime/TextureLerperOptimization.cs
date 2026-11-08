using HarmonyLib;
using UnityEngine;

namespace Jlib.Runtime
{
    /// <summary>Optional optimizations for the game's full-grid texture lerpers.</summary>
    public static class TextureLerperOptimization
    {
        public static void Apply(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(VisibleRangePatch)).Patch();
        }

        // TextureLerper.Update immediately replaces this private method's result
        // with the full grid bounds before using it. Avoid the unused camera rays.
        [HarmonyPatch(typeof(TextureLerper), "GetVisibleCellRange")]
        private static class VisibleRangePatch
        {
            private static bool logged;
            [HarmonyPrefix]
            private static bool Prefix(ref Vector4 __result)
            {
                if (!logged)
                {
                    logged = true;
                    Debug.Log("[Jlib] Texture lerper skips unused visible-range calculation");
                }
                __result = default(Vector4);
                return false;
            }
        }
    }
}
