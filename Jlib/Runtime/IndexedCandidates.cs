using System.Collections.Generic;

namespace Jlib.Runtime {
    /// <summary>Allocation-free indexed enumeration retaining List mutation checks.</summary>
    public readonly struct IndexedCandidates<T> {
        private readonly List<T> source;
        private readonly IReadOnlyList<int> indices;
        public IndexedCandidates(List<T> source, IReadOnlyList<int> indices) {
            this.source = source; this.indices = indices;
        }
        public Enumerator GetEnumerator() => new Enumerator(source, indices);
        public struct Enumerator {
            private List<T>.Enumerator guard;
            private readonly List<T> source;
            private readonly IReadOnlyList<int> indices;
            private int index;
            internal Enumerator(List<T> source, IReadOnlyList<int> indices) {
                this.source = source; this.indices = indices;
                guard = source.GetEnumerator(); index = -1;
            }
            public T Current => indices == null ? guard.Current : source[indices[index]];
            public bool MoveNext() {
                bool available = guard.MoveNext();
                return available && (indices == null || ++index < indices.Count);
            }
            public void Dispose() { guard.Dispose(); }
        }
    }
}
