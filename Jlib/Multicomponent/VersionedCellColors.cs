#nullable disable
using System;

namespace Jlib.Multicomponent {
    // One shared grid cache, sharded by cell block. Parallel texture workers do
    // not duplicate a world-sized cache or race multi-field entry publication.
    internal sealed class VersionedCellColors {
        private struct Entry { internal ulong Revision; internal uint Context, Color; }
        private sealed class Block { internal readonly Entry[] Cells = new Entry[256]; }
        private readonly Block[] blocks;
        internal VersionedCellColors(int cells) { blocks = new Block[(cells + 255) / 256]; }

        internal bool TryGet(int cell, ulong revision, uint context, out uint color) {
            color = 0;
            if (revision == 0 || cell < 0 || cell / 256 >= blocks.Length) return false;
            var block = System.Threading.Volatile.Read(ref blocks[cell / 256]);
            if (block == null) return false;
            lock (block) {
                var entry = block.Cells[cell % 256];
                if (entry.Revision != revision || entry.Context != context) return false;
                color = entry.Color;
                return true;
            }
        }

        internal void Store(int cell, ulong revision, uint context, uint color) {
            if (revision == 0 || cell < 0 || cell / 256 >= blocks.Length) return;
            var block = System.Threading.Volatile.Read(ref blocks[cell / 256]);
            if (block == null) {
                var created = new Block();
                block = System.Threading.Interlocked.CompareExchange(ref blocks[cell / 256], created, null) ?? created;
            }
            lock (block) block.Cells[cell % 256] = new Entry { Revision = revision, Context = context, Color = color };
        }
    }
}
