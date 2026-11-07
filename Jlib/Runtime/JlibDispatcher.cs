/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using UnityEngine;

namespace Jlib.Runtime {
    /// <summary>
    /// Thread-safe double-buffered dispatcher bridging background worker threads to Unity's main thread.
    /// Provides zero-allocation lock-free queue swapping during render frames.
    /// </summary>
    public sealed class JlibDispatcher : MonoBehaviour {
        private static JlibDispatcher _instance;
        private static int _mainThreadId;

        public static JlibDispatcher Instance => Volatile.Read(ref _instance);
        public static bool IsMainThread => _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private readonly Queue<System.Action> _queueA = new Queue<System.Action>();
        private readonly Queue<System.Action> _queueB = new Queue<System.Action>();
        private readonly object _queueLock = new object();
        private readonly HashSet<System.Action> _pendingCancellations = new HashSet<System.Action>();
        private Queue<System.Action> _enqueueQueue;
        private Queue<System.Action> _dequeueQueue;
        private bool _destroyed;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            _enqueueQueue = _queueA;
            _dequeueQueue = _queueB;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            Volatile.Write(ref _instance, this);
        }

        private void Update() {
            lock (_queueLock) {
                if (_enqueueQueue.Count == 0)
                    return;

                var next = _enqueueQueue;
                _enqueueQueue = _dequeueQueue;
                _dequeueQueue = next;
            }

            while (_dequeueQueue.Count > 0) {
                var action = _dequeueQueue.Dequeue();
                try {
                    action();
                } catch (Exception ex) {
                    Debug.LogError("[Jlib] Dispatcher uncaught action error: " + ex);
                }
            }
        }

        public static JlibDispatcher EnsureInitialized() {
            var instance = Instance;
            if (instance != null)
                return instance;

            if (IsMainThread || _mainThreadId == 0) {
                var go = new GameObject("Jlib_Dispatcher");
                return go.AddComponent<JlibDispatcher>();
            }
            throw new InvalidOperationException("JlibDispatcher must be initialized on the Unity main thread first.");
        }

        public static void RequireMainThread(string operationName = null) {
            if (!IsMainThread)
                throw new InvalidOperationException(
                    (operationName ?? "This operation") + " must be executed on the Unity main thread.");
        }

        public static void Enqueue(System.Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            EnsureInstance().EnqueueInternal(action, allowInline: true, cancel: null);
        }

        public static void EnqueueDeferred(System.Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            EnsureInstance().EnqueueInternal(action, allowInline: false, cancel: null);
        }

        public static T Invoke<T>(Func<T> func, int timeoutMs = 10000) {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            if (timeoutMs < Timeout.Infinite)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));

            var instance = EnsureInstance();
            if (instance.IsOnMainThread())
                return func();

            var invocation = new DispatcherInvocation<T>(func);
            instance.EnqueueInternal(invocation.Execute, allowInline: false, cancel: invocation.Cancel);
            return invocation.Wait(timeoutMs);
        }

        private bool IsOnMainThread() => _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private void EnqueueInternal(System.Action action, bool allowInline, System.Action cancel) {
            if (allowInline && IsOnMainThread()) {
                action();
                return;
            }

            lock (_queueLock) {
                if (_destroyed)
                    throw new InvalidOperationException("JlibDispatcher has been destroyed.");
                if (cancel != null) {
                    var pendingAction = action;
                    action = () => {
                        try {
                            pendingAction();
                        } finally {
                            lock (_queueLock)
                                _pendingCancellations.Remove(cancel);
                        }
                    };
                    _pendingCancellations.Add(cancel);
                }
                _enqueueQueue.Enqueue(action);
            }
        }

        private static JlibDispatcher EnsureInstance() {
            var instance = Instance;
            if (ReferenceEquals(instance, null))
                throw new InvalidOperationException("JlibDispatcher is not initialized. Ensure Jlib is loaded.");
            return instance;
        }

        private void OnDestroy() {
            Interlocked.CompareExchange(ref _instance, null, this);
            System.Action[] cancellations;
            lock (_queueLock) {
                _destroyed = true;
                cancellations = new System.Action[_pendingCancellations.Count];
                _pendingCancellations.CopyTo(cancellations);
                _pendingCancellations.Clear();
                _queueA.Clear();
                _queueB.Clear();
            }
            foreach (var cancel in cancellations)
                cancel();
        }

        private sealed class DispatcherInvocation<T> {
            private readonly object _gate = new object();
            private readonly Func<T> _action;
            private bool _started;
            private bool _completed;
            private bool _cancelled;
            private T _result;
            private ExceptionDispatchInfo _error;

            internal DispatcherInvocation(Func<T> action) {
                _action = action ?? throw new ArgumentNullException(nameof(action));
            }

            internal void Execute() {
                lock (_gate) {
                    if (_cancelled || _started)
                        return;
                    _started = true;
                }

                T result = default;
                ExceptionDispatchInfo error = null;
                try {
                    result = _action();
                } catch (Exception ex) {
                    error = ExceptionDispatchInfo.Capture(ex);
                }

                lock (_gate) {
                    if (_completed)
                        return;
                    _result = result;
                    _error = error;
                    _completed = true;
                    Monitor.PulseAll(_gate);
                }
            }

            internal void Cancel() {
                lock (_gate) {
                    if (_completed)
                        return;
                    _cancelled = true;
                    _error = ExceptionDispatchInfo.Capture(
                        new InvalidOperationException("Unity main thread dispatcher was destroyed."));
                    _completed = true;
                    Monitor.PulseAll(_gate);
                }
            }

            internal T Wait(int timeoutMs) {
                if (timeoutMs < Timeout.Infinite)
                    throw new ArgumentOutOfRangeException(nameof(timeoutMs));

                var elapsed = Stopwatch.StartNew();
                lock (_gate) {
                    while (!_completed) {
                        int remaining = timeoutMs == Timeout.Infinite
                            ? Timeout.Infinite
                            : (int)Math.Max(0L, timeoutMs - elapsed.ElapsedMilliseconds);
                        if (remaining == 0 || !Monitor.Wait(_gate, remaining)) {
                            _cancelled = !_started;
                            throw new TimeoutException("Timed out waiting for Unity main thread dispatch.");
                        }
                    }
                    _error?.Throw();
                    return _result;
                }
            }
        }
    }
}
