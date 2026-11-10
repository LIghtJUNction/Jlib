using System;
using HarmonyLib;
using UnityEngine;

namespace Jlib.Multicomponent
{
    /// <summary>
    /// 材料视图（F4 / TileMode）与氧气视图多组分真实混合渲染补丁。
    /// 当格子中存在多组分混合物时，按照各组分质量权重动态混合颜色，
    /// 确保玩家在材料概况视图中能肉眼直观看到双组分/多组分的混合色彩过渡。
    /// </summary>
    public static class MulticomponentOverlayPatch
    {
        private static readonly AccessTools.FieldRef<SimDebugView, int> RenderFrame =
            AccessTools.FieldRefAccess<SimDebugView, int>("currentFrame");
        [ThreadStatic] private static RenderCellCache cells;

        private static bool TryGetRenderCell(SimDebugView view, int cell, out MulticomponentCellSnapshot snapshot)
        {
            if (cells == null) cells = new RenderCellCache();
            return cells.TryGetCell(cell, Grid.CellCount, RenderFrame(view), view, out snapshot);
        }
        [HarmonyPatch(typeof(SimDebugView), "GetTileColour")]
        public static class MaterialOverlayTileColourPatch
        {
            [HarmonyPostfix]
            public static void Postfix(SimDebugView instance, int cell, ref Color __result)
            {
                if (!TryGetRenderCell(instance, cell, out var snapshot))
                    return;

                if (snapshot.Components == null || snapshot.ComponentCount <= 1 || snapshot.TotalMass <= 0f)
                    return;

                float r = 0f, g = 0f, b = 0f;
                float accountedMass = 0f;

                for (int i = 0; i < snapshot.ComponentCount; i++)
                {
                    var item = snapshot.Components[i];
                    if (item.Mass <= 0f || item.Element == null || item.Element.substance == null)
                        continue;

                    Color c = item.Element.substance.uiColour;
                    r += c.r * item.Mass;
                    g += c.g * item.Mass;
                    b += c.b * item.Mass;
                    accountedMass += item.Mass;
                }

                if (accountedMass > 0f)
                {
                    __result = new Color(r / accountedMass, g / accountedMass, b / accountedMass, __result.a);
                }
            }
        }

        [HarmonyPatch(typeof(SimDebugView), "GetOxygenMapColour")]
        public static class OxygenOverlayColourPatch
        {
            [HarmonyPostfix]
            public static void Postfix(SimDebugView instance, int cell, ref Color __result)
            {
                if (Grid.IsLiquid(cell) || Grid.Solid[cell])
                    return;
                if (!TryGetRenderCell(instance, cell, out var snapshot))
                    return;

                if (snapshot.Components == null || snapshot.ComponentCount <= 1 || snapshot.TotalMass <= 0f)
                    return;

                // Recompute the native pressure scale from all breathable gas.
                // Scaling the old primary-element colour by pure-O2 fraction
                // makes polluted oxygen black and causes dominance flicker.
                float mass = BreathableGas.Mass(snapshot);
                __result = mass > SimDebugView.minimumBreathable
                    ? instance.breathableGradient.Evaluate(Mathf.Clamp01(
                        (mass - SimDebugView.minimumBreathable) / SimDebugView.optimallyBreathable))
                    : (Color)instance.unbreathableColour;
            }
        }
    }
}
