/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;

namespace Jlib.Multicomponent {
    public enum MulticomponentPhase : byte {
        Gas = 0,
        Liquid = 1,
        Solid = 2
    }

    /// <summary>
    /// Read-only species data for one cell. Element is the game's canonical
    /// Element instance; values use ONI's standard mass, temperature and pressure units.
    /// </summary>
    public struct MulticomponentSpecies {
        public readonly Element Element;
        public readonly MulticomponentPhase Phase;
        public readonly float Mass;
        public readonly float Temperature;
        public readonly float VolumeFraction;
        public readonly float PartialPressureKPa;

        public MulticomponentSpecies(Element element, MulticomponentPhase phase,
            float mass, float temperature, float volumeFraction, float partialPressureKPa) {
            Element = element;
            Phase = phase;
            Mass = mass;
            Temperature = temperature;
            VolumeFraction = volumeFraction;
            PartialPressureKPa = partialPressureKPa;
        }
    }

    /// <summary>
    /// Borrowed per-cell snapshot. Components is a reusable buffer;
    /// consume ComponentCount entries before the next TryGetCell on this thread.
    /// </summary>
    public struct MulticomponentCellSnapshot {
        public readonly MulticomponentSpecies[] Components;
        public readonly int ComponentCount;
        public readonly float TotalMass;
        public readonly float GasPressureKPa;
        public readonly float LiquidPressureKPa;
        public readonly float LiquidVolumeFraction;

        public MulticomponentCellSnapshot(MulticomponentSpecies[] components,
            int componentCount, float totalMass, float gasPressureKPa, float liquidPressureKPa,
            float liquidVolumeFraction) {
            Components = components;
            ComponentCount = componentCount;
            TotalMass = totalMass;
            GasPressureKPa = gasPressureKPa;
            LiquidPressureKPa = liquidPressureKPa;
            LiquidVolumeFraction = liquidVolumeFraction;
        }
    }

    /// <summary>
    /// Contract for native or managed simulation engines (e.g. OniSim GPU kernels)
    /// publishing per-cell multi-species composition data to managed consumers.
    /// </summary>
    public interface IMulticomponentCellProvider {
        bool IsReady { get; }
        bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot);
    }

    /// <summary>Optional cheap, process-monotonic content revision; zero disables caching.</summary>
    public interface IMulticomponentRevisionProvider {
        ulong ContentRevision { get; }
    }

    /// <summary>
    /// Optional bounded bulk read. Implementations copy publication data before
    /// returning; no native lease may remain held while the caller uses a batch.
    /// Existing single-cell providers do not need to implement this interface.
    /// </summary>
    public interface IMulticomponentCellBatchProvider {
        bool TryGetCellBatch(int firstCell, int count, out IMulticomponentCellBatch batch);
    }

    /// <summary>
    /// An invocation-owned, same-thread reader. Dispose after the region is
    /// consumed, including exceptional exits. A snapshot is borrowed until the
    /// next read on this batch or disposal. False permits a single-cell fallback.
    /// </summary>
    public interface IMulticomponentCellBatch : IDisposable {
        bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot);
    }
}
