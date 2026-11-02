using System.Reflection;

// Only the data boundary used by the ownership helper is reproduced here.
// This host executable never references or executes an installed game assembly.
namespace KMod {
    public sealed class Mod {
        public string? ContentPath { get; set; }
        public string? staticID { get; set; }
        public LoadedModData? loaded_mod_data;
    }

    public sealed class LoadedModData {
        public List<Assembly>? dlls;
    }
}

public sealed class Global {
    public static Global? Instance;
    public ModManager? modManager;
}

public sealed class ModManager {
    public List<KMod.Mod?>? mods;
}
