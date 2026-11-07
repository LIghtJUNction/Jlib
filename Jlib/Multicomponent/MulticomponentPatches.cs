/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Jlib.Multicomponent {
    /// <summary>
    /// Master installer for all multicomponent game shims, unlocking ONI's hardcoded
    /// single-element assumptions across visual rendering, duplicant respiration,
    /// building operation, plant vitality, and swimming navigation.
    /// </summary>
    public static class MulticomponentPatches {
        private static bool _installed;

        public static void ApplyAll(Harmony harmony) {
            if (harmony == null)
                throw new ArgumentNullException(nameof(harmony));
            if (_installed)
                return;
            _installed = true;

            var patchTypes = new[] {
                typeof(MulticomponentVisualPatch),
                typeof(MulticomponentBreathingPatch),
                typeof(MulticomponentBuildingPatch.ElementConsumerAvailablePatch),
                typeof(MulticomponentBuildingPatch.VentValidOutputCellPatch),
                typeof(MulticomponentPlantPatch),
                typeof(MulticomponentNavigationPatch),
                typeof(MulticomponentHoverPatch)
            };

            foreach (var type in patchTypes) {
                try {
                    harmony.CreateClassProcessor(type).Patch();
                } catch (Exception ex) {
                    Debug.LogWarning("[Jlib] Failed to patch multicomponent shim " + type.Name + ": " + ex);
                }
            }
        }
    }

    /// <summary>
    /// Mass-weighted gas and liquid color blending for multicomponent cells.
    /// </summary>
    [HarmonyPatch(typeof(PropertyTextures), "UpdateGasColour")]
    public static class MulticomponentVisualPatch {
        public static void ComputeBlendedGasColor(
            MulticomponentCellSnapshot snapshot,
            bool cellAboveValid,
            out byte r,
            out byte g,
            out byte b,
            out byte a) {
            r = 0;
            g = 0;
            b = 0;
            a = 0;

            if (snapshot.Components == null || snapshot.ComponentCount == 0)
                return;

            float gasMass = 0f;
            float gasR = 0f;
            float gasG = 0f;
            float gasB = 0f;

            float liquidMass = 0f;
            float liquidR = 0f;
            float liquidG = 0f;
            float liquidB = 0f;

            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Mass <= 0f || item.Element == null || item.Element.substance == null)
                    continue;

                Color32 c = item.Element.substance.colour;
                if (item.Phase == MulticomponentPhase.Gas) {
                    gasMass += item.Mass;
                    gasR += item.Mass * c.r;
                    gasG += item.Mass * c.g;
                    gasB += item.Mass * c.b;
                } else if (item.Phase == MulticomponentPhase.Liquid) {
                    liquidMass += item.Mass;
                    liquidR += item.Mass * c.r;
                    liquidG += item.Mass * c.g;
                    liquidB += item.Mass * c.b;
                }
            }

            if (gasMass > 0f) {
                r = ClampByte(gasR / gasMass);
                g = ClampByte(gasG / gasMass);
                b = ClampByte(gasB / gasMass);
                a = byte.MaxValue;
            } else if (cellAboveValid && liquidMass > 0f) {
                r = ClampByte(liquidR / liquidMass);
                g = ClampByte(liquidG / liquidMass);
                b = ClampByte(liquidB / liquidMass);
                a = byte.MaxValue;
            }
        }

        private static byte ClampByte(float value) {
            int rounded = (int)Math.Round(value);
            if (rounded <= 0)
                return 0;
            if (rounded >= 255)
                return 255;
            return (byte)rounded;
        }

        [HarmonyPrefix]
        private static bool Prefix(TextureRegion region, int x0, int y0, int x1, int y1) {
            if (!MulticomponentCellAccess.IsReady)
                return true;

            for (int i = y0; i <= y1; i++) {
                for (int j = x0; j <= x1; j++) {
                    int cell = Grid.XYToCell(j, i);
                    if (!Grid.IsActiveWorld(cell)) {
                        region.SetBytes(j, i, 0, 0, 0, 0);
                        continue;
                    }

                    if (MulticomponentCellAccess.TryGetCell(cell, out var snapshot)) {
                        byte r, g, b, a;
                        ComputeBlendedGasColor(
                            snapshot,
                            Grid.IsValidCell(Grid.CellAbove(cell)),
                            out r, out g, out b, out a);
                        region.SetBytes(j, i, r, g, b, a);
                        continue;
                    }

                    Element element = Grid.Element[cell];
                    if (element.IsGas) {
                        region.SetBytes(j, i, element.substance.colour.r, element.substance.colour.g, element.substance.colour.b, byte.MaxValue);
                    } else if (element.IsLiquid && Grid.IsValidCell(Grid.CellAbove(cell))) {
                        region.SetBytes(j, i, element.substance.colour.r, element.substance.colour.g, element.substance.colour.b, byte.MaxValue);
                    } else {
                        region.SetBytes(j, i, 0, 0, 0, 0);
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Respiration shim resolving breathable mass by sum of breathable species.
    /// </summary>
    [HarmonyPatch(typeof(GasBreatherFromWorldProvider), "GetBreathableCellMass")]
    public static class MulticomponentBreathingPatch {
        public static void EvaluateBreathableCell(
            MulticomponentCellSnapshot snapshot,
            out SimHashes elementID,
            out float breathableMass) {
            breathableMass = 0f;
            float dominantBreathableMass = 0f;
            elementID = SimHashes.Vacuum;

            if (snapshot.Components == null)
                return;

            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies component = snapshot.Components[i];
                if (component.Phase != MulticomponentPhase.Gas
                    || component.Element == null
                    || !component.Element.HasTag(GameTags.Breathable)
                    || component.Mass <= 0f)
                    continue;

                breathableMass += component.Mass;
                if (component.Mass > dominantBreathableMass) {
                    dominantBreathableMass = component.Mass;
                    elementID = component.Element.id;
                }
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(int cell, ref SimHashes elementID, ref float __result) {
            if (!MulticomponentCellAccess.TryGetCell(cell, out var snapshot))
                return true;

            EvaluateBreathableCell(snapshot, out elementID, out __result);
            return false;
        }
    }

    /// <summary>
    /// Multicomponent compatibility for ElementConsumer and Vent.
    /// </summary>
    public static class MulticomponentBuildingPatch {
        public const float GasVentFloodedLiquidFraction = 0.95f;

        public static bool EvaluateElementAvailable(
            MulticomponentCellSnapshot snapshot,
            ElementConsumer.Configuration configuration,
            SimHashes elementToConsume,
            float minimumMass) {
            if (snapshot.Components == null)
                return false;

            float availableMass = 0f;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Mass <= 0f || item.Element == null)
                    continue;

                switch (configuration) {
                    case ElementConsumer.Configuration.Element:
                        if (item.Element.id == elementToConsume)
                            availableMass += item.Mass;
                        break;
                    case ElementConsumer.Configuration.AllGas:
                        if (item.Phase == MulticomponentPhase.Gas)
                            availableMass += item.Mass;
                        break;
                    case ElementConsumer.Configuration.AllLiquid:
                        if (item.Phase == MulticomponentPhase.Liquid)
                            availableMass += item.Mass;
                        break;
                }
            }

            return availableMass >= minimumMass;
        }

        public static bool EvaluateVentOutputValid(
            MulticomponentCellSnapshot snapshot,
            ConduitType conduitType,
            float overpressureMass,
            bool isSolidOrClosed) {
            if (isSolidOrClosed)
                return false;

            if (snapshot.Components == null)
                return false;

            if (conduitType == ConduitType.Gas && snapshot.LiquidVolumeFraction >= GasVentFloodedLiquidFraction)
                return false;

            float phaseMass = 0f;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Mass <= 0f)
                    continue;

                if (conduitType == ConduitType.Gas && item.Phase == MulticomponentPhase.Gas)
                    phaseMass += item.Mass;
                else if (conduitType == ConduitType.Liquid && item.Phase == MulticomponentPhase.Liquid)
                    phaseMass += item.Mass;
            }

            return phaseMass < overpressureMass;
        }

        [HarmonyPatch(typeof(ElementConsumer), "get_IsElementAvailable")]
        public static class ElementConsumerAvailablePatch {
            private static readonly MethodInfo GetSampleCellMethod =
                AccessTools.Method(typeof(ElementConsumer), "GetSampleCell");

            [HarmonyPrefix]
            private static bool Prefix(ElementConsumer __instance, ref bool __result) {
                if (!MulticomponentCellAccess.IsReady || GetSampleCellMethod == null)
                    return true;

                int sampleCell = (int)GetSampleCellMethod.Invoke(__instance, null);
                if (!MulticomponentCellAccess.TryGetCell(sampleCell, out var snapshot))
                    return true;

                __result = EvaluateElementAvailable(
                    snapshot,
                    __instance.configuration,
                    __instance.elementToConsume,
                    __instance.minimumMass);
                return false;
            }
        }

        [HarmonyPatch(typeof(Vent), "IsValidOutputCell")]
        public static class VentValidOutputCellPatch {
            private static readonly FieldInfo StructureField =
                AccessTools.Field(typeof(Vent), "structure");
            private static readonly MethodInfo ClosedMethod =
                AccessTools.Method(typeof(Vent), "Closed");

            [HarmonyPrefix]
            private static bool Prefix(Vent __instance, int output_cell, ref bool __result) {
                if (!MulticomponentCellAccess.TryGetCell(output_cell, out var snapshot))
                    return true;

                bool closedOrEntombed = false;
                if (StructureField != null && ClosedMethod != null) {
                    Structure structure = StructureField.GetValue(__instance) as Structure;
                    if (structure != null && structure.IsEntombed() && (bool)ClosedMethod.Invoke(__instance, null))
                        closedOrEntombed = true;
                }

                bool blocked = closedOrEntombed || Grid.Solid[output_cell];
                __result = EvaluateVentOutputValid(
                    snapshot,
                    __instance.conduitType,
                    __instance.overpressureMass,
                    blocked);
                return false;
            }
        }
    }

    /// <summary>
    /// Plant vitality shim evaluating safe-element partial pressure.
    /// </summary>
    [HarmonyPatch(typeof(PressureVulnerable), "GetPressureOverArea")]
    public static class MulticomponentPlantPatch {
        public static bool EvaluateCellAtmosphere(
            MulticomponentCellSnapshot snapshot,
            Func<Element, bool> isSafeElement,
            float pressureWarningHigh,
            out float effectivePressure,
            out Element dominantSafeElement,
            out Element fallbackElement) {
            effectivePressure = 0f;
            dominantSafeElement = null;
            fallbackElement = null;

            if (snapshot.Components == null || isSafeElement == null)
                return false;

            float safeMass = 0f;
            float dominantSafeMass = 0f;
            float totalFluidMass = 0f;

            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Mass <= 0f || item.Element == null || item.Phase == MulticomponentPhase.Solid)
                    continue;

                if (fallbackElement == null)
                    fallbackElement = item.Element;

                totalFluidMass += item.Mass;
                if (isSafeElement(item.Element)) {
                    safeMass += item.Mass;
                    if (item.Mass > dominantSafeMass) {
                        dominantSafeMass = item.Mass;
                        dominantSafeElement = item.Element;
                    }
                }
            }

            if (safeMass <= 0f)
                return false;

            effectivePressure = totalFluidMass > pressureWarningHigh ? totalFluidMass : safeMass;
            return true;
        }

        [HarmonyPrefix]
        private static bool Prefix(PressureVulnerable __instance, int cell, ref float __result) {
            if (!MulticomponentCellAccess.IsReady)
                return true;

            OccupyArea occupyArea = __instance.GetComponent<OccupyArea>();
            if (occupyArea == null || occupyArea.OccupiedCellsOffsets == null)
                return true;

            bool previousSafe = __instance.testAreaElementSafe;
            float pressureSum = 0f;
            int areaCount = 0;
            int safeElementCount = 0;
            __instance.testAreaElementSafe = false;
            __instance.currentAtmoElement = null;

            CellOffset[] offsets = occupyArea.OccupiedCellsOffsets;
            for (int i = 0; i < offsets.Length; i++) {
                int testCell = Grid.OffsetCell(cell, offsets[i]);
                if (!Grid.IsValidCell(testCell) || Grid.IsSolidCell(testCell))
                    continue;

                if (MulticomponentCellAccess.TryGetCell(testCell, out var snapshot)) {
                    float cellPressure;
                    Element safeElement;
                    Element fallbackElement;
                    if (EvaluateCellAtmosphere(
                        snapshot,
                        __instance.IsSafeElement,
                        __instance.pressureWarning_High,
                        out cellPressure,
                        out safeElement,
                        out fallbackElement)) {
                        pressureSum += cellPressure;
                        areaCount++;
                        safeElementCount++;
                        __instance.currentAtmoElement = safeElement;
                    } else if (__instance.currentAtmoElement == null && fallbackElement != null) {
                        __instance.currentAtmoElement = fallbackElement;
                    }
                } else {
                    Element element = Grid.Element[testCell];
                    if (__instance.IsSafeElement(element)) {
                        pressureSum += Grid.Mass[testCell];
                        areaCount++;
                        safeElementCount++;
                        __instance.currentAtmoElement = element;
                    }
                    if (__instance.currentAtmoElement == null)
                        __instance.currentAtmoElement = element;
                }
            }

            __instance.testAreaElementSafe = __instance.allCellsMustBeSafe
                ? (safeElementCount == offsets.Length)
                : (safeElementCount > 0);
            __result = areaCount > 0 ? (pressureSum / areaCount) : 0f;
            if (__instance.testAreaElementSafe != previousSafe)
                __instance.Trigger(-2023773544);

            return false;
        }
    }

    /// <summary>
    /// Swimming hysteresis navigation shim.
    /// </summary>
    [HarmonyPatch(typeof(GameNavGrids.DupeSwimValidator), "UpdateCell")]
    public static class MulticomponentNavigationPatch {
        public const float SwimEntryVolumeFraction = 0.40f;
        public const float SwimExitVolumeFraction = 0.20f;
        private static readonly MethodInfo IsClearMethod = AccessTools.Method(typeof(NavTableValidator), "IsClear");

        public static bool EvaluateSwimState(
            bool isClear,
            bool currentlySwimming,
            float cellLiquidVolumeFraction,
            bool cellAboveValid,
            float cellAboveLiquidVolumeFraction) {
            if (!isClear)
                return false;

            float threshold = currentlySwimming ? SwimExitVolumeFraction : SwimEntryVolumeFraction;
            if (cellLiquidVolumeFraction >= threshold)
                return true;

            return cellLiquidVolumeFraction > 0f
                && cellAboveValid
                && cellAboveLiquidVolumeFraction >= threshold;
        }

        [HarmonyPrefix]
        private static bool Prefix(
            GameNavGrids.DupeSwimValidator __instance,
            int cell,
            NavTable nav_table,
            CellOffset[] bounding_offsets) {
            if (!MulticomponentCellAccess.TryGetCell(cell, out var snapshot) || IsClearMethod == null)
                return true;

            bool isClear = (bool)IsClearMethod.Invoke(__instance, new object[] { cell, bounding_offsets, true });
            bool currentlySwimming = nav_table.IsValid(cell, NavType.Swim);

            int cellAbove = Grid.CellAbove(cell);
            bool cellAboveValid = Grid.IsWorldValidCell(cellAbove);
            float aboveLiquidFraction = 0f;
            if (cellAboveValid) {
                if (MulticomponentCellAccess.TryGetCell(cellAbove, out var aboveSnapshot))
                    aboveLiquidFraction = aboveSnapshot.LiquidVolumeFraction;
                else if (Grid.IsLiquid(cellAbove))
                    aboveLiquidFraction = 1f;
            }

            bool nextSwimming = EvaluateSwimState(
                isClear,
                currentlySwimming,
                snapshot.LiquidVolumeFraction,
                cellAboveValid,
                aboveLiquidFraction);

            nav_table.SetValid(cell, NavType.Swim, nextSwimming);
            if (currentlySwimming == nextSwimming)
                return false;

            Grid.CellToXY(cell, out int x, out int y);
            int minX = Grid.ClampX(x - 1);
            int minY = Grid.ClampY(y - 2);
            int maxX = Grid.ClampX(x + 1);
            int maxY = Grid.ClampY(y + 2);
            if (__instance.onDirty != null) {
                for (int iy = minY; iy <= maxY; iy++) {
                    for (int ix = minX; ix <= maxX; ix++) {
                        __instance.onDirty(Grid.XYToCell(ix, iy));
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Multi-species hover tooltip inspection shim.
    /// </summary>
    [HarmonyPatch(typeof(HoverTextHelper), "MassStringsReadOnly")]
    public static class MulticomponentHoverPatch {
        public static string FormatCompositionSuffix(MulticomponentCellSnapshot snapshot) {
            if (snapshot.Components == null || snapshot.ComponentCount <= 1 || snapshot.TotalMass <= 0f)
                return string.Empty;

            var builder = new StringBuilder(" [");
            bool first = true;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Mass <= 0f || item.Element == null)
                    continue;

                if (!first)
                    builder.Append(", ");
                first = false;

                int pct = (int)Math.Round((item.Mass / snapshot.TotalMass) * 100f);
                string label = !string.IsNullOrEmpty(item.Element.name)
                    ? item.Element.name
                    : item.Element.id.ToString();
                builder.Append(label).Append(' ').Append(pct).Append('%');
            }
            builder.Append(']');
            return first ? string.Empty : builder.ToString();
        }

        [HarmonyPostfix]
        private static void Postfix(int cell, string[] __result) {
            if (__result == null || __result.Length < 4)
                return;

            if (!MulticomponentCellAccess.TryGetCell(cell, out var snapshot))
                return;

            string suffix = FormatCompositionSuffix(snapshot);
            if (!string.IsNullOrEmpty(suffix) && (__result[3] == null || !__result[3].EndsWith(suffix, StringComparison.Ordinal)))
                __result[3] = (__result[3] ?? string.Empty) + suffix;
        }
    }
}
