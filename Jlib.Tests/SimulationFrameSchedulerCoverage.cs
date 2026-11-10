using System.Runtime.CompilerServices;
using HarmonyLib;
using Jlib.Runtime;

internal static class SimulationFrameSchedulerCoverage {
    internal static void Run() {
        bool ready = false;
        bool fault = false;
        Func<bool> observe = () => fault ? throw new InvalidOperationException("native worker fault") : ready;
        var harmony = new Harmony("Jlib.Tests.SimulationFrameScheduler");
        SimulationFrameScheduler.Install(harmony, observe);
        SimulationFrameScheduler.Install(harmony, observe);
        try {
            var game = new Game();
            for (int i = 0; i < 100; i++) game.Tick(0.01f);
            Program.Equal(11, game.ManagedTicks, "independent managed work overlaps native computation");
            Program.Equal(0, game.Handoffs, "busy worker never blocks on a positive or zero-time handoff");
            ready = true;
            game.Tick(0.01f);
            Program.Equal(23, game.ManagedTicks, "stalled budget is bounded by vanilla's 0.2 second limit");
            Program.Equal(1, game.Handoffs, "ready signal allows exactly one handoff");

            ready = false;
            var paused = new Game();
            for (int i = 0; i < 100; i++) paused.Tick(0.02f);
            paused.Tick(0f);
            ready = true;
            paused.Tick(0f);
            Program.Equal(11, paused.ManagedTicks, "pause cannot replay deferred simulation time");
            paused.Tick(1f / 60f);
            Program.Equal(12, paused.ManagedTicks, "resume starts without a long catch-up");
            var nextWorld = new Game();
            nextWorld.Tick(1f / 60f);
            Program.Equal(1, nextWorld.ManagedTicks, "a new game has no inherited time debt");

            var random = new Random(29);
            for (int tick = 0; tick < 12; tick++) {
                var varied = new Game(tick);
                ready = false;
                for (int i = 0; i < 200; i++) varied.Tick((float)random.NextDouble() * 0.35f);
                Program.Equal(0, varied.Handoffs, "no busy handoff from sub-tick " + tick);
                Program.Equal(11 - tick, varied.ManagedTicks, "independent work progresses from sub-tick " + tick);
            }
            fault = true;
            Program.Throws<InvalidOperationException>(() => nextWorld.Tick(0.01f), "worker faults are visible");
            Program.Equal(1, nextWorld.ManagedTicks, "worker faults do not advance simulation");
            fault = false;
            var changing = new Game();
            changing.OnHandoff = () => {
                Program.Check(ready, "each native handoff owns a ready completion");
                ready = false;
            };
            for (int i = 0; i < 5000; i++) {
                ready = random.Next(3) == 0;
                changing.Tick((float)random.NextDouble() * 0.4f);
            }
        } finally {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}

// Run the real prefix against vanilla's private timing loop shape.
public sealed class Game {
    private float simDt;
    private int simSubTick;
    public int ManagedTicks { get; private set; }
    public int Handoffs { get; private set; }
    public Action? OnHandoff;
    public Game(int subTick = 0) => simSubTick = subTick;
    public void Tick(float dt) => SimEveryTick(dt);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SimEveryTick(float dt) {
        simDt += Math.Min(dt, 0.2f);
        if (simDt >= 1f / 60f) {
            do {
                simSubTick = (simSubTick + 1) % 12;
                if (simSubTick == 0) Handoff();
                ManagedTicks++;
                simDt -= 1f / 60f;
            } while (simDt >= 1f / 60f);
        } else Handoff();
    }
    private void Handoff() { Handoffs++; OnHandoff?.Invoke(); }
}
