using Jlib.Multicomponent;
using UnityEngine;

internal static class MulticomponentCoverage {
    internal static void Run() {
        var gases = new[] {
            new MulticomponentSpecies(new Element { id = SimHashes.Oxygen }, MulticomponentPhase.Gas, .4f, 300f, 0f, 0f),
            new MulticomponentSpecies(new Element { id = SimHashes.ContaminatedOxygen }, MulticomponentPhase.Gas, .6f, 300f, 0f, 0f),
            new MulticomponentSpecies(new Element { id = SimHashes.CarbonDioxide }, MulticomponentPhase.Gas, 2f, 300f, 0f, 0f)
        };
        var gasSnapshot = new MulticomponentCellSnapshot(gases, 3, 3f, 0f, 0f, 0f);
        Program.Equal(1f, BreathableGas.Mass(gasSnapshot), "polluted oxygen counts as breathable; CO2 does not");
        Array.Reverse(gases);
        Program.Equal(1f, BreathableGas.Mass(gasSnapshot), "primary species ordering cannot change breathability");
        var style = new TextStyleSetting();
        var fallback = new HoverTextDrawer();
        fallback.BeginShadowBar();
        Program.Check(!MulticomponentHoverCardPatch.DrawCardsOrTitle(fallback, "OXYGEN", style),
            "hover without a provider uses the original title");
        Program.Equal("OXYGEN", fallback.Text.Single(), "fallback title is retained");
        Program.Equal(1, fallback.OpenShadowBars, "fallback leaves the caller's cleanup intact");
        fallback.EndShadowBar();

        RenderCellCacheCoverage.Run();
        var provider = new Provider();
        int originalFrame = Time.frameCount;
        Vector3 originalMouse = KInputManager.MousePosition;
        Program.Check(MulticomponentCellAccess.RegisterProvider(provider), "hover provider registers");
        try {
            Time.frameCount++;
            KInputManager.MousePosition = new Vector3();
            var first = new HoverTextDrawer();
            Draw(first, style);
            Program.Equal(2, first.Text.Count, "one card per positive-mass species");
            Program.Check(first.Text[0].Contains("Oxygen") && first.Text[0].Contains("1.5 kg"),
                "first card keeps species identity and mass");
            Program.Check(first.Text[1].Contains("Carbon Dioxide") && first.Text[1].Contains("0.5 kg"),
                "second card keeps species identity and mass");
            Program.Check(first.Root.x == 10f && first.Root.y == 20f && first.Current.y < 20f,
                "cards restore the drawing origin and advance the cursor");

            Draw(first, style);
            Program.Equal(2, first.Text.Count, "same drawer suppresses duplicate cards in this frame");

            var second = new HoverTextDrawer();
            Draw(second, style);
            Program.Equal(2, second.Text.Count, "another drawer renders the same cell in this frame");
            Draw(first, style);
            Program.Equal(2, first.Text.Count, "drawing another UI does not reset the first drawer's deduplication");

            Time.frameCount++;
            Draw(first, style);
            Program.Equal(4, first.Text.Count, "the next frame redraws the cards");
            KInputManager.MousePosition = new Vector3 { x = 1f };
            Draw(first, style);
            Program.Equal(6, first.Text.Count, "another cell redraws the cards in the same frame");

            KInputManager.MousePosition = new Vector3 { x = -1f };
            first.BeginShadowBar();
            Program.Check(!MulticomponentHoverCardPatch.DrawCardsOrTitle(first, "INVALID", style),
                "invalid cells retain the original rendering path");
            Program.Equal(1, first.OpenShadowBars, "invalid-cell fallback leaves cleanup to the caller");
            first.EndShadowBar();
        } finally {
            Time.frameCount = originalFrame;
            KInputManager.MousePosition = originalMouse;
            Program.Check(MulticomponentCellAccess.UnregisterProvider(provider), "hover provider unregisters");
        }
    }

    private static void Draw(HoverTextDrawer drawer, TextStyleSetting style) {
        drawer.BeginShadowBar();
        Program.Check(MulticomponentHoverCardPatch.DrawCardsOrTitle(drawer, "OXYGEN", style),
            "mixed-cell hover replaces the original card");
        Program.Equal(0, drawer.OpenShadowBars, "every replaced card closes the caller's shadow bar");
    }

    private sealed class Provider : IMulticomponentCellProvider {
        private readonly MulticomponentSpecies[] components = {
            new(new Element { name = "Oxygen" }, MulticomponentPhase.Gas, 1.5f, 300f, 0.75f, 150f),
            new(new Element { name = "Carbon Dioxide" }, MulticomponentPhase.Gas, 0.5f, 300f, 0.25f, 50f)
        };

        public bool IsReady => true;

        public bool TryGetCell(int cell, out MulticomponentCellSnapshot snapshot) {
            snapshot = new MulticomponentCellSnapshot(components, 2, 2f, 200f, 0f, 0f);
            return true;
        }
    }
}
