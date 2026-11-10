#nullable disable
using System;

namespace Jlib.Multicomponent {
    // Batches own managed copies; no native lease is retained across calls.
    internal sealed class RenderCellCache : IDisposable {
        private IMulticomponentCellBatch batch;
        private object provider;
        private object view;
        private int frame = -1;
        private int first;
        private int count;

        internal bool TryGetCell(int cell, int cells, int renderFrame, object renderView,
            out MulticomponentCellSnapshot snapshot) {
            snapshot = default;
            var current = MulticomponentCellAccess.ProviderIdentity;
            if (!MulticomponentCellAccess.Enabled || current == null || cell < 0 || cell >= cells) {
                Dispose();
                return false;
            }
            if (frame != renderFrame || !ReferenceEquals(view, renderView)
                || !ReferenceEquals(provider, current) || cell < first || cell >= first + count) {
                Dispose();
                frame = renderFrame; view = renderView; provider = current;
                first = cell / MulticomponentCellAccess.MaximumBatchCells * MulticomponentCellAccess.MaximumBatchCells;
                count = Math.Min(MulticomponentCellAccess.MaximumBatchCells, cells - first);
                MulticomponentCellAccess.TryGetCellBatch(first, count, out batch);
            }
            if (batch != null && batch.TryGetCell(cell, out snapshot)) return true;
            return MulticomponentCellAccess.TryGetCell(cell, out snapshot);
        }

        public void Dispose() {
            batch?.Dispose();
            batch = null;
        }
    }
}
