#nullable disable
using System;
using System.Collections.Generic;

namespace Jlib.Runtime {
    /// <summary>
    /// Filters an ordered snapshot once per query key. Returned lists are immutable
    /// until discarded, so concurrent task evaluators can use them without a lease.
    /// Eligibility that depends on reservations or the consumer is evaluated later.
    /// </summary>
    public sealed class OrderedCandidateIndex<T, TKey> {
        private readonly Dictionary<TKey, List<T>> matches = new Dictionary<TKey, List<T>>();
        private readonly Func<T, TKey, bool> match;
        private IReadOnlyList<T> source = Array.Empty<T>();
        public long Examined { get; private set; }

        public OrderedCandidateIndex(Func<T, TKey, bool> match) {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
        }

        // Called after the producer joins its workers, before consumers start.
        public void Reset(IReadOnlyList<T> orderedSource) {
            lock (matches) {
                source = orderedSource ?? throw new ArgumentNullException(nameof(orderedSource));
                matches.Clear();
                Examined = 0;
            }
        }

        public IReadOnlyList<T> Get(TKey key) {
            lock (matches) {
                if (matches.TryGetValue(key, out var result)) return result;
                result = new List<T>();
                for (int i = 0; i < source.Count; i++) {
                    var candidate = source[i];
                    Examined++;
                    if (match(candidate, key)) result.Add(candidate);
                }
                matches.Add(key, result);
                return result;
            }
        }
    }
}
