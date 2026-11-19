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
    /// Element instance. Mass and temperature are always available; consult the
    /// availability fields before interpreting volume or pressure.
    /// </summary>
    public struct MulticomponentSpecies {
        public readonly Element Element;
        public readonly MulticomponentPhase Phase;
        public readonly float Mass;
        public readonly float Temperature;
        public readonly float VolumeFraction;
        public readonly float PartialPressureKPa;
        /// <summary>Whether VolumeFraction is a computed occupied-cell fraction.</summary>
        public readonly bool VolumeFractionAvailable;
        /// <summary>Whether PartialPressureKPa is computed in kPa, rather than a placeholder.</summary>
        public readonly bool PartialPressureAvailable;

        // Existing providers already assert that all six supplied values are known.
        public MulticomponentSpecies(Element element, MulticomponentPhase phase,
            float mass, float temperature, float volumeFraction, float partialPressureKPa)
            : this(element, phase, mass, temperature, volumeFraction, partialPressureKPa, true, true) { }

        public MulticomponentSpecies(Element element, MulticomponentPhase phase,
            float mass, float temperature, float volumeFraction, float partialPressureKPa,
            bool volumeFractionAvailable, bool partialPressureAvailable) {
            Element = element;
            Phase = phase;
            Mass = mass;
            Temperature = temperature;
            VolumeFraction = volumeFraction;
            PartialPressureKPa = partialPressureKPa;
            VolumeFractionAvailable = volumeFractionAvailable;
            PartialPressureAvailable = partialPressureAvailable;
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
        /// <summary>Whether LiquidVolumeFraction is a computed occupied-cell fraction.</summary>
        public readonly bool LiquidVolumeFractionAvailable;
        /// <summary>Whether both GasPressureKPa and LiquidPressureKPa are computed in kPa.</summary>
        public readonly bool PressureAvailable;

        // Preserve the contract of providers using the original constructor.
        public MulticomponentCellSnapshot(MulticomponentSpecies[] components,
            int componentCount, float totalMass, float gasPressureKPa, float liquidPressureKPa,
            float liquidVolumeFraction)
            : this(components, componentCount, totalMass, gasPressureKPa, liquidPressureKPa,
                liquidVolumeFraction, true, true) { }

        public MulticomponentCellSnapshot(MulticomponentSpecies[] components,
            int componentCount, float totalMass, float gasPressureKPa, float liquidPressureKPa,
            float liquidVolumeFraction, bool liquidVolumeFractionAvailable, bool pressureAvailable) {
            Components = components;
            ComponentCount = componentCount;
            TotalMass = totalMass;
            GasPressureKPa = gasPressureKPa;
            LiquidPressureKPa = liquidPressureKPa;
            LiquidVolumeFraction = liquidVolumeFraction;
            LiquidVolumeFractionAvailable = liquidVolumeFractionAvailable;
            PressureAvailable = pressureAvailable;
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
    /// Opt-in revision for the normal Game.StepTheSim publication path. Equal
    /// nonzero revisions guarantee identical published Grid.Mass and Grid.Element
    /// values, including the cell above each liquid. Zero disables reuse. This
    /// excludes palette, lighting, fog, and external flow textures. The provider
    /// must invalidate on every grid-changing command and simulation step, and
    /// reset to zero on shutdown. Consumers also key the game/texture lifetime.
    /// </summary>
    public interface IMulticomponentGridRevisionProvider {
        ulong GridRevision { get; }
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
