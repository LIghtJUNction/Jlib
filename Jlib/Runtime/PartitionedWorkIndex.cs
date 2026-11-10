#nullable disable
using System;
using System.Collections.Generic;

namespace Jlib.Runtime {
    /// <summary>Stable source positions partitioned by scope; metadata remains in its authoritative list.</summary>
    public sealed class PartitionedWorkIndex<T> {
        private readonly Dictionary<int, List<int>> partitions = new();
        private bool dirty = true;
        private int count = -1;
        public long Examined { get; private set; }
        public void Invalidate() { dirty = true; }
        public IReadOnlyList<int> Get(IReadOnlyList<T> source, int scope, Func<T, int> scopeOf) {
            if (dirty || count != source.Count) {
                foreach (var partition in partitions.Values) partition.Clear();
                for (int i = 0; i < source.Count; i++) {
                    int key = scopeOf(source[i]);
                    if (!partitions.TryGetValue(key, out var list)) partitions.Add(key, list = new List<int>());
                    list.Add(i);
                }
                Examined += source.Count;
                dirty = false; count = source.Count;
            }
            return partitions.TryGetValue(scope, out var result) ? result : Array.Empty<int>();
        }
    }
}
