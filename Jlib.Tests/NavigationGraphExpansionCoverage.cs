#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Optimization = Jlib.Runtime.NavigationGraphExpansion;

internal static class NavigationGraphExpansionCoverage
{
    private static int calls;
    private static int assertions;
    private const string Owner = "Jlib.Tests.NavigationGraphExpansion";

    internal static void Run(bool benchmark)
    {
        var harmony = new Harmony(Owner);
        var update = AccessTools.Method(typeof(Graph), nameof(Graph.UpdateGraph), Type.EmptyTypes);
        var add = AccessTools.Method(typeof(Graph), nameof(Graph.AddDirtyCell));
        harmony.Patch(add, prefix: new HarmonyMethod(typeof(NavigationGraphExpansionCoverage), nameof(ObserveAdd)));
        var random = new Random(1271);
        for (int trial = 0; trial < 80; trial++)
        {
            int width = random.Next(1, 50), height = random.Next(1, 45);
            var cells = Enumerable.Range(0, random.Next(0, width * height + 1))
                .Select(_ => random.Next(width * height)).ToArray();
            int rx = random.Next(0, 6), ry = random.Next(0, 7);
            var baseline = new Graph(width, height, rx, ry);
            var candidate = new Graph(width, height, rx, ry);
            baseline.Seed(cells);
            candidate.Seed(cells);
            harmony.Unpatch(update, HarmonyPatchType.Transpiler, Owner);
            baseline.UpdateGraph();
            Check(Optimization.Install(harmony), Optimization.LastFailure);
            Check(Optimization.Install(new Harmony(Owner + ".consumer")), "shared installation");
            candidate.UpdateGraph();
            Check(baseline.Processed.SequenceEqual(candidate.Processed), "dirty order and membership");
            Check(baseline.Flags.SequenceEqual(candidate.Flags), "dirty flags after clearing");
            Check(baseline.Updates == candidate.Updates, "callback cadence");
            candidate.UpdateGraph();
            Check(candidate.Processed.Count == 0 && candidate.Updates == 2, "empty update retains callback");
            candidate.Seed(cells);
            candidate.UpdateGraph();
            Check(baseline.Processed.SequenceEqual(candidate.Processed), "reuse after clear");
        }
        // Direct callers continue through AddDirtyCell and its existing hooks.
        var direct = new Graph(4, 4, 2, 3);
        calls = 0;
        direct.AddDirtyCell(1);
        direct.AddDirtyCell(1);
        Check(calls == 2, "direct caller hooks preserved");
        if (benchmark) Benchmark(harmony, update);
        harmony.UnpatchAll(Owner);
        Console.WriteLine("PASS navigation expansion " + assertions + " assertions");
    }

    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(label);
    }

    private static void ObserveAdd() { calls++; }

    private static void Benchmark(Harmony harmony, MethodInfo update)
    {
        var graph = new Graph(738, 306, 2, 3) { Capture = false };
        int[] sources = Enumerable.Range(0, 2000).Select(i => (100 + i / 50) * 738 + 300 + i % 50).ToArray();
        for (int round = 0; round < 4; round++)
        {
            bool optimized = round == 1 || round == 2;
            harmony.Unpatch(update, HarmonyPatchType.Transpiler, Owner);
            if (optimized) Check(Optimization.Install(harmony), Optimization.LastFailure);
            var samples = new List<double>();
            long totalCalls = 0;
            for (int iteration = 0; iteration < 120; iteration++)
            {
                graph.Seed(sources);
                calls = 0;
                long start = Stopwatch.GetTimestamp();
                graph.UpdateGraph();
                double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (iteration >= 20) { samples.Add(ms); totalCalls += calls; }
            }
            Console.WriteLine($"NAV_EXPANSION_SAMPLE optimized={optimized} mean_ms={samples.Average():F6} calls={totalCalls / samples.Count}");
        }
    }

    internal static class Cells
    {
        internal static int Width, Height;
        internal static void CellToXY(int cell, out int x, out int y) { x = cell % Width; y = cell / Width; }
        internal static int ClampX(int x) => Math.Max(0, Math.Min(Width - 1, x));
        internal static int ClampY(int y) => Math.Max(0, Math.Min(Height - 1, y));
        internal static int XYToCell(int x, int y) => y * Width + x;
        internal static bool IsValidCell(int cell) => cell >= 0 && cell < Width * Height;
    }

    internal sealed class Graph
    {
        public struct Link { public int Value; }
        public struct Transition { }

        private byte[] DirtyBitFlags;
        private List<int> DirtyCells = new List<int>();
        private List<int> dirtyCellsSwapBuffer = new List<int>();
        private readonly int width, height;
        public readonly int updateRangeX, updateRangeY;
        internal List<int> Processed = new List<int>();
        internal bool Capture = true;
        internal int Updates;
        internal byte[] Flags => DirtyBitFlags;

        internal Graph(int width, int height, int rangeX, int rangeY)
        {
            this.width = width; this.height = height;
            this.updateRangeX = rangeX; this.updateRangeY = rangeY;
            Cells.Width = width; Cells.Height = height;
            DirtyBitFlags = new byte[(width * height + 7) / 8];
        }

        internal void Seed(IEnumerable<int> cells) { foreach (int cell in cells) AddDirtyCell(cell); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddDirtyCell(int cell)
        {
            if (cell >= 0 && cell < width * height && (DirtyBitFlags[cell / 8] & (1 << cell % 8)) == 0)
            {
                DirtyCells.Add(cell);
                DirtyBitFlags[cell / 8] |= (byte)(1 << cell % 8);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateGraph()
        {
            int count = DirtyCells.Count;
            for (int i = 0; i < count; i++)
            {
                int x = DirtyCells[i] % width, y = DirtyCells[i] / width;
                int x0 = Math.Max(0, x - updateRangeX), x1 = Math.Min(width - 1, x + updateRangeX);
                int y0 = Math.Max(0, y - updateRangeY), y1 = Math.Min(height - 1, y + updateRangeY);
                for (int row = y0; row <= y1; row++)
                    for (int col = x0; col <= x1; col++) AddDirtyCell(row * width + col);
            }
            var empty = dirtyCellsSwapBuffer;
            dirtyCellsSwapBuffer = DirtyCells;
            DirtyCells = empty;
            UpdateGraph(dirtyCellsSwapBuffer);
            foreach (int cell in dirtyCellsSwapBuffer) DirtyBitFlags[cell / 8] = 0;
            dirtyCellsSwapBuffer.Clear();
        }

        public void UpdateGraph(List<int> cells)
        {
            Updates++;
            if (Capture) Processed = new List<int>(cells);
        }
    }
}
