#nullable disable
using System;
using System.Collections.Generic;

namespace Jlib.Runtime {
    /// <summary>Orders only the eligible prefix consumed by a bounded scheduler.</summary>
    public static class BoundedWorkSelection {
        public static void SelectFirst<T>(List<T> values, Comparison<T> comparison,
                int limit, Predicate<T> eligible) {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            int retained = 0;
            int length = values.Count;
            for (int i = 0; i < length; i++) {
                T value = values[i];
                if (!eligible(value)) continue;
                int at = retained;
                while (at > 0 && comparison(value, values[at - 1]) < 0) at--;
                if (at >= limit) continue;
                int end = Math.Min(retained, limit - 1);
                for (int j = end; j > at; j--) values[j] = values[j - 1];
                values[at] = value;
                retained = Math.Min(retained + 1, limit);
            }
            if (retained < length) values.RemoveRange(retained, length - retained);
        }
    }
}
