/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Threading;
using UnityEngine;

namespace Jlib.Multicomponent {
    /// <summary>
    /// Fail-closed global access point shared by all multicomponent Harmony shims.
    /// When no provider is registered or ready, shims automatically fall back to
    /// original single-element game logic without overhead.
    /// </summary>
    public static class MulticomponentCellAccess {
        private static IMulticomponentCellProvider provider = null;
        private static int providerFailureReported;
        private static bool enabled = true;

        public static bool Enabled {
            get => enabled;
            set => enabled = value;
        }

        public static bool RegisterProvider(IMulticomponentCellProvider value) {
            if (value == null)
                return false;
            return Interlocked.CompareExchange(ref provider, value, null) == null;
        }

        public static bool UnregisterProvider(IMulticomponentCellProvider value) {
            if (value == null)
                return false;
            return ReferenceEquals(Interlocked.CompareExchange(ref provider, null, value), value);
        }

        public static bool IsReady {
            get {
                if (!enabled)
                    return false;
                IMulticomponentCellProvider current = Volatile.Read(ref provider);
                try {
                    return current != null && current.IsReady;
                } catch (Exception error) {
                    ReportProviderFailure(error);
                    return false;
                }
            }
        }

        public static bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot) {
            snapshot = default;
            if (!enabled)
                return false;

            IMulticomponentCellProvider current = Volatile.Read(ref provider);
            if (current == null)
                return false;

            try {
                if (!current.TryGetCell(cell, out snapshot) || !IsValid(snapshot)) {
                    snapshot = default;
                    return false;
                }
                return true;
            } catch (Exception error) {
                snapshot = default;
                ReportProviderFailure(error);
                return false;
            }
        }

        private static bool IsValid(MulticomponentCellSnapshot snapshot) {
            if (snapshot.Components == null || snapshot.ComponentCount < 0
                || snapshot.ComponentCount > snapshot.Components.Length || !FiniteNonNegative(snapshot.TotalMass)
                || !FiniteNonNegative(snapshot.GasPressureKPa)
                || !FiniteNonNegative(snapshot.LiquidPressureKPa)
                || !FiniteNonNegative(snapshot.LiquidVolumeFraction)
                || snapshot.LiquidVolumeFraction > 1f)
                return false;

            float mass = 0f;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                MulticomponentSpecies item = snapshot.Components[i];
                if (item.Element == null || !FiniteNonNegative(item.Mass)
                    || !Finite(item.Temperature) || !FiniteNonNegative(item.VolumeFraction)
                    || item.VolumeFraction > 1f || !FiniteNonNegative(item.PartialPressureKPa))
                    return false;
                mass += item.Mass;
                if (!FiniteNonNegative(mass))
                    return false;
            }

            // The provider must publish a self-consistent mass aggregate.
            float tolerance = Math.Max(0.001f, snapshot.TotalMass * 0.0001f);
            return Math.Abs(mass - snapshot.TotalMass) <= tolerance;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteNonNegative(float value) => Finite(value) && value >= 0f;

        private static void ReportProviderFailure(Exception error) {
            if (Interlocked.Exchange(ref providerFailureReported, 1) == 0)
                Debug.LogWarning("[Jlib] Multicomponent provider failed; retaining original single-element game behavior: " + error);
        }
    }
}
