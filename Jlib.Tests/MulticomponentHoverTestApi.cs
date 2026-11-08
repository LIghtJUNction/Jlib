using System.Globalization;
using UnityEngine;

// Drawing-state boundaries for the production hover helper. No Unity renderer
// or game assembly is loaded; shadow-bar pairing is checked independently.
public sealed class Element {
    public string name = "";
    public float maxMass;
    public string nameUpperCase => name.ToUpperInvariant();
    public string Description() => string.Empty;
}

public static class Grid {
    public static int PosToCell(Vector3 position) => (int)position.x;
    public static bool IsValidCell(int cell) => cell >= 0;
}

public sealed class SelectToolHoverTextCard {
    public void UpdateHoverElements() { }
}

public sealed class TextStyleSetting { }

public sealed class HoverTextDrawer {
    private Vector2 currentPos = new(10f, 20f);
    private Vector2 rootPos = new(10f, 20f);
    private bool firstShadowBar = true;
    private float maxShadowX;

    internal readonly List<string> Text = new();
    internal int OpenShadowBars { get; private set; }
    internal Vector2 Root => rootPos;
    internal Vector2 Current => currentPos;

    public void DrawText(string text, TextStyleSetting style) {
        Text.Add(text);
        currentPos.y -= text.Split('\n').Length;
        maxShadowX = Math.Max(maxShadowX, currentPos.x + text.Length);
    }

    public void BeginShadowBar() {
        OpenShadowBars++;
        if (firstShadowBar)
            maxShadowX = currentPos.x;
        firstShadowBar = false;
    }

    public void EndShadowBar() {
        if (OpenShadowBars == 0)
            throw new InvalidOperationException("EndShadowBar has no matching BeginShadowBar");
        OpenShadowBars--;
    }
}

public static class KInputManager {
    internal static Vector3 MousePosition;
    public static Vector3 GetMousePos() => MousePosition;
}

public static class GameUtil {
    public static string GetFormattedMass(float mass) =>
        mass.ToString("0.###", CultureInfo.InvariantCulture) + " kg";

    public static string GetFormattedTemperature(float temperature) =>
        temperature.ToString("0.###", CultureInfo.InvariantCulture) + " K";
}

namespace UnityEngine {
    public struct Vector3 {
        public float x, y, z;
    }

    public sealed class Camera {
        public static readonly Camera main = new();
        public Vector3 ScreenToWorldPoint(Vector3 position) => position;
    }

    public static class Time {
        public static int frameCount;
    }
}
