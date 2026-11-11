#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
#if JLIB_HOST_TESTS
using NavGrid = NavigationGraphExpansionCoverage.Graph;
using NavigationGridCells = NavigationGraphExpansionCoverage.Cells;
#else
using NavigationGridCells = global::Grid;
#endif

namespace Jlib.Runtime
{
    /// <summary>Suppresses already-dirty neighbor insertions during graph expansion.</summary>
    public static class NavigationGraphExpansion
    {
        private static readonly object InstallLock = new object();
        private static AccessTools.FieldRef<NavGrid, byte[]> dirtyFlags;
        private static AccessTools.FieldRef<NavGrid, List<int>> dirtyCells;
        private static readonly byte[] FirstBit = BuildFirstBit();
        public static string LastFailure { get; private set; }

        public static bool Install(Harmony harmony)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            lock (InstallLock)
            {
                MethodInfo target = null;
                MethodInfo transpiler = AccessTools.Method(typeof(NavigationGraphExpansion), nameof(Transpile));
                try
                {
                    target = AccessTools.Method(typeof(NavGrid), "UpdateGraph", Type.EmptyTypes)
                        ?? throw new MissingMethodException(typeof(NavGrid).FullName, "UpdateGraph()");
                    if (Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.PatchMethod == transpiler) == true)
                        return true;
                    dirtyCells = AccessTools.FieldRefAccess<NavGrid, List<int>>("DirtyCells");
                    dirtyFlags = AccessTools.FieldRefAccess<NavGrid, byte[]>("DirtyBitFlags");
                    harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
                    LastFailure = null;
                    return true;
                }
                catch (Exception error)
                {
                    if (target != null) harmony.Unpatch(target, transpiler);
                    LastFailure = error.ToString();
                    return false;
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var body = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var swap = AccessTools.Field(typeof(NavGrid), "dirtyCellsSwapBuffer");
            int tail = body.FindIndex(instruction => instruction.opcode == OpCodes.Ldfld
                && Equals(instruction.operand, swap)) - 1;
            var add = AccessTools.Method(typeof(NavGrid), "AddDirtyCell", new[] { typeof(int) });
            bool expectedLoop = tail >= 0 && body.Take(tail).Count(instruction => Equals(instruction.operand, add)) == 1
                && !body.Skip(tail).Any(instruction => Equals(instruction.operand, add));
            if (!expectedLoop || tail < 0 || body[tail].opcode != OpCodes.Ldarg_0
                || body.Take(tail).Any(instruction => instruction.blocks.Count != 0))
                throw new InvalidOperationException("Unsupported navigation expansion/buffer-swap layout");
            var entry = new CodeInstruction(OpCodes.Ldarg_0);
            foreach (var instruction in body.Take(tail)) entry.labels.AddRange(instruction.labels);
            var result = new List<CodeInstruction> { entry,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NavigationGraphExpansion), nameof(Expand))) };
            result.AddRange(body.Skip(tail));
            return result;
        }

        private static byte[] BuildFirstBit()
        {
            var bits = new byte[256];
            for (int value = 1; value < bits.Length; value++)
            {
                int mask = value;
                while ((mask & 1) == 0) { bits[value]++; mask >>= 1; }
            }
            return bits;
        }

        private static void Expand(NavGrid graph)
        {
            byte[] flags = dirtyFlags(graph);
            List<int> cells = dirtyCells(graph);
            int count = cells.Count;
            for (int i = 0; i < count; i++)
            {
                NavigationGridCells.CellToXY(cells[i], out int x, out int y);
                int minX = NavigationGridCells.ClampX(x - graph.updateRangeX);
                int maxX = NavigationGridCells.ClampX(x + graph.updateRangeX);
                int minY = NavigationGridCells.ClampY(y - graph.updateRangeY);
                int maxY = NavigationGridCells.ClampY(y + graph.updateRangeY);
                for (int row = minY; row <= maxY; row++)
                {
                    int first = NavigationGridCells.XYToCell(minX, row);
                    int last = NavigationGridCells.XYToCell(maxX, row);
                    for (int index = first >> 3; index <= last >> 3; index++)
                    {
                        int low = index == (first >> 3) ? first & 7 : 0;
                        int high = index == (last >> 3) ? last & 7 : 7;
                        int remaining = (255 << low) & (255 >> (7 - high));
                        int clean;
                        while ((clean = remaining & ~flags[index]) != 0)
                        {
                            int bit = FirstBit[clean];
                            graph.AddDirtyCell((index << 3) + bit);
                            // Re-read flags after hooks and preserve row-major order.
                            remaining &= 255 << (bit + 1);
                        }
                    }
                }
            }
        }
    }
}
