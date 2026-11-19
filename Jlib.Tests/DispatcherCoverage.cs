using System.Reflection;
using Jlib.Runtime;

internal static class DispatcherCoverage {
    private static readonly MethodInfo Update = typeof(JlibDispatcher).GetMethod(
        "Update", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void Run() {
        var dispatcher = JlibDispatcher.EnsureInitialized();
        var observed = new List<int>();
        JlibDispatcher.Enqueue(() => observed.Add(-1));
        Program.Equal(-1, observed.Single(), "main-thread enqueue runs inline");
        observed.Clear();

        for (int i = 0; i < 512; i++) {
            int value = i;
            JlibDispatcher.EnqueueDeferred(() => observed.Add(value));
        }
        Program.Equal(512, dispatcher.PendingCount, "deferred work waits for a frame");
        Pump(dispatcher);
        Program.Check(observed.Count > 0 && observed.Count <= 128,
            "a burst yields after a bounded batch");
        Program.Equal(512 - observed.Count, dispatcher.PendingCount,
            "unfinished callbacks remain queued");
        JlibDispatcher.EnqueueDeferred(() => observed.Add(512));
        Drain(dispatcher);
        Program.Check(observed.SequenceEqual(Enumerable.Range(0, 513)),
            "partially drained batches retain FIFO order across frames");

        bool afterSlow = false;
        JlibDispatcher.EnqueueDeferred(() => Thread.Sleep(12));
        JlibDispatcher.EnqueueDeferred(() => afterSlow = true);
        Pump(dispatcher);
        Program.Check(!afterSlow, "elapsed frame budget yields after a slow callback");
        Drain(dispatcher);
        Program.Check(afterSlow, "the next frame resumes budgeted work");

        bool nested = false;
        JlibDispatcher.EnqueueDeferred(() =>
            JlibDispatcher.EnqueueDeferred(() => nested = true));
        Pump(dispatcher);
        Program.Check(!nested, "deferred callbacks enqueued while draining wait for another frame");
        Drain(dispatcher);
        Program.Check(nested, "nested deferred work is retained");

        int errors = UnityEngine.Debug.Errors.Count;
        bool afterError = false;
        JlibDispatcher.EnqueueDeferred(() => throw new InvalidOperationException("callback failure"));
        JlibDispatcher.EnqueueDeferred(() => afterError = true);
        Drain(dispatcher);
        Program.Equal(errors + 1, UnityEngine.Debug.Errors.Count,
            "uncaught callback errors are logged once");
        Program.Check(afterError, "one callback failure does not discard later work");

        var producers = Enumerable.Range(0, 4).Select(producer => Task.Run(() => {
            for (int i = 0; i < 50; i++) {
                int value = producer * 1000 + i;
                JlibDispatcher.EnqueueDeferred(() => observed.Add(value));
            }
        })).ToArray();
        observed.Clear();
        Program.Check(Task.WaitAll(producers, 3000), "concurrent producers finish");
        Drain(dispatcher);
        Program.Equal(200, observed.Count, "all concurrent callbacks execute once");
        for (int i = 0; i < 4; i++) {
            int producer = i;
            Program.Check(observed.Where(v => v / 1000 == producer)
                .SequenceEqual(Enumerable.Range(0, 50).Select(v => producer * 1000 + v)),
                "FIFO order is preserved for each producer");
        }

        bool expiredRan = false;
        var expired = Task.Run(() => {
            try {
                JlibDispatcher.Invoke(() => { expiredRan = true; return 1; }, 0);
                return false;
            } catch (TimeoutException error) {
                return error.Message.Contains("cancelled", StringComparison.Ordinal);
            }
        });
        Program.Check(expired.Wait(3000) && expired.Result,
            "a queued invocation reports that its timeout cancelled the callback");
        Drain(dispatcher);
        Program.Check(!expiredRan, "timed-out queued work never executes later");

        var result = Task.Run(() => JlibDispatcher.Invoke(() => 42));
        WaitForPending(dispatcher);
        Drain(dispatcher);
        Program.Check(result.Wait(3000), "worker invocation is awakened");
        Program.Equal(42, result.Result, "worker receives the main-thread result");

        var failure = Task.Run(() => {
            try { JlibDispatcher.Invoke<int>(ThrowFromCallback); }
            catch (InvalidOperationException error) { return error; }
            return null;
        });
        WaitForPending(dispatcher);
        Drain(dispatcher);
        Program.Check(failure.Wait(3000), "worker failure completes");
        Program.Check(failure.Result?.StackTrace?.Contains(nameof(ThrowFromCallback),
            StringComparison.Ordinal) == true, "callback failures retain their original stack");

        var destroyed = Task.Run(() => {
            try { JlibDispatcher.Invoke(() => 1, Timeout.Infinite); }
            catch (InvalidOperationException) { return true; }
            return false;
        });
        WaitForPending(dispatcher);
        UnityEngine.Object.Destroy(dispatcher);
        Program.Check(destroyed.Wait(3000) && destroyed.Result,
            "destroying the dispatcher wakes pending infinite waits");
        Program.Equal(0, dispatcher.PendingCount, "destroying the dispatcher clears queued work");
        Program.Check(JlibDispatcher.Instance == null, "destroying the active dispatcher clears the singleton");
        JlibDispatcher.EnsureInitialized();
    }

    private static int ThrowFromCallback() => throw new InvalidOperationException("original callback");

    private static void WaitForPending(JlibDispatcher dispatcher) {
        Program.Check(SpinWait.SpinUntil(() => dispatcher.PendingCount > 0, 3000),
            "worker reaches the real dispatcher queue");
    }

    private static void Pump(JlibDispatcher dispatcher) => Update.Invoke(dispatcher, null);

    private static void Drain(JlibDispatcher dispatcher) {
        for (int frame = 0; dispatcher.PendingCount > 0 && frame < 2048; frame++)
            Pump(dispatcher);
        Program.Equal(0, dispatcher.PendingCount, "queued work drains completely");
    }
}
