using Jlib.Runtime;

internal static class WorkSelectionCoverage {
    internal static void Run() {
        var random = new Random(71204);
        for (int n = 0; n < 500; n++) {
            var source = Enumerable.Range(0, n).Select(i => random.Next(-1000, 1000)).ToList();
            var expected = source.Where(x => x >= 0).OrderBy(x => x).Take(4).ToArray();
            BoundedWorkSelection.SelectFirst(source, (a, b) => a.CompareTo(b), 4, x => x >= 0);
            if (!source.SequenceEqual(expected)) throw new Exception("bounded selection differs from full ordering");
        }
        var many = Enumerable.Range(0, 100000).Reverse().ToList();
        long comparisons = 0;
        BoundedWorkSelection.SelectFirst(many, (a, b) => { comparisons++; return a.CompareTo(b); }, 4, _ => true);
        if (!many.SequenceEqual(new[] { 0, 1, 2, 3 }) || comparisons > 400000)
            throw new Exception("selection must remain linear in navigator count");
        var index = new OrderedCandidateIndex<int, int>((item, key) => item % 7 == key);
        index.Reset(Enumerable.Range(0, 1000).ToArray());
        Parallel.For(0, 100, _ => {
            if (!index.Get(3).SequenceEqual(Enumerable.Range(0, 1000).Where(i => i % 7 == 3)))
                throw new Exception("candidate ordering changed");
        });
        if (index.Examined != 1000) throw new Exception("repeated chores rescanned the snapshot");
        index.Reset(new[] { 10, 17, 24 });
        if (!index.Get(3).SequenceEqual(new[] { 10, 17, 24 }))
            throw new Exception("refresh retained stale candidates");
        var items = new List<int> { 0, 1, 2, 3, 4, 5 };
        var partitions = new PartitionedWorkIndex<int>();
        if (!partitions.Get(items, 1, x => x % 2).SequenceEqual(new[] { 1, 3, 5 }))
            throw new Exception("partition changed authoritative order");
        partitions.Get(items, 0, x => x % 2);
        if (partitions.Examined != 6) throw new Exception("shared world index rescans per consumer");
        items[1] = 2; partitions.Invalidate();
        if (!partitions.Get(items, 1, x => x % 2).SequenceEqual(new[] { 3, 5 }))
            throw new Exception("migration kept stale membership");
        items.RemoveAt(0); partitions.Invalidate();
        if (!partitions.Get(items, 1, x => x % 2).SequenceEqual(new[] { 2, 4 }))
            throw new Exception("compacted source positions were not rebuilt");
        var colors = new Jlib.Multicomponent.VersionedCellColors(1025);
        colors.Store(1024, 7, 8, 9);
        if (!colors.TryGet(1024, 7, 8, out uint color) || color != 9
            || colors.TryGet(1024, 8, 8, out _) || colors.TryGet(1024, 7, 9, out _)
            || colors.TryGet(1024, 0, 8, out _)) throw new Exception("stale color revision or context reused");
        Parallel.For(0, 1000, i => { colors.Store(i, 9, (uint)i, (uint)i * 2); });
        for (int i = 0; i < 1000; i++)
            if (!colors.TryGet(i, 9, (uint)i, out color) || color != i * 2)
                throw new Exception("parallel texture workers raced publication");
        Console.WriteLine("PASS bounded navigation selection and ordered candidate index (100 queries, one scan)");
    }
}
