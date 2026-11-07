/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;

namespace PeterHan.PLib.Core {
    /// <summary>
    /// Zero-lock single-item per-thread object pool designed for non-reentrant hot paths.
    /// Eliminates thread contention and heap allocations without unbounded memory growth.
    /// </summary>
    /// <typeparam name="T">The reference type to pool, requiring a parameterless constructor.</typeparam>
    public static class PThreadLocalPool<T> where T : class, new() {
        [ThreadStatic]
        private static T item;

        public static T Rent() {
            var pooled = item;
            if (pooled != null) {
                item = null;
                return pooled;
            }
            return new T();
        }

        public static void Return(T value) {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // One retained object is enough for the normal non-reentrant hot path.
            // If a call is nested, let the extra object become collectible rather
            // than permanently growing per-thread retained capacity.
            if (item == null)
                item = value;
        }
    }
}
