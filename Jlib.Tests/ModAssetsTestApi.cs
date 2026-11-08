using System.Collections;

// In-memory Unity/game boundaries for asset ownership tests. Image decoding and
// Sprite.Create failures are injected; no engine or installed game code executes.
namespace UnityEngine {
    public class Object {
        internal int DestroyCount { get; private set; }
        public static void Destroy(Object value) => value.DestroyCount++;
    }

    public sealed class Texture2D : Object {
        internal static readonly List<Texture2D> Created = new();
        internal static Func<byte[], bool> Decode = _ => true;
        public int width { get; }
        public int height { get; }

        public Texture2D(int width, int height) {
            this.width = width;
            this.height = height;
            Created.Add(this);
        }

        public bool LoadImage(byte[] data) => Decode(data);
    }

    public sealed class Sprite : Object {
        internal static Exception? CreateFailure;
        public Texture2D texture { get; }
        private Sprite(Texture2D texture) => this.texture = texture;

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) {
            if (CreateFailure != null)
                throw CreateFailure;
            return new Sprite(texture);
        }
    }

    public readonly struct Rect {
        public Rect(float x, float y, float width, float height) { }
    }

    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) {
            this.x = x;
            this.y = y;
        }
    }

    public static class Debug {
        internal static readonly List<object> Errors = new();
        internal static readonly List<object> Warnings = new();
        public static void LogError(object message) => Errors.Add(message);
        public static void LogWarning(object message) => Warnings.Add(message);
    }

    // Untested APIs deliberately fail if called so the fixture cannot stand in
    // for audio playback, Unity networking, or platform-specific bundle loading.
    public sealed class AssetBundle : Object {
        public static AssetBundle LoadFromFile(string path) => throw new NotSupportedException();
        public T LoadAsset<T>(string name) where T : Object => throw new NotSupportedException();
        public void Unload(bool unloadAllLoadedObjects) => throw new NotSupportedException();
    }

    public sealed class AudioClip : Object { }
    public enum AudioType { UNKNOWN }
    public sealed class AudioSource : Object {
        public void PlayOneShot(AudioClip clip, float volume) => throw new NotSupportedException();
    }
}

namespace UnityEngine.Networking {
    public sealed class UnityWebRequest : IDisposable {
        public enum Result { Success }
        public Result result => throw new NotSupportedException();
        public string error => throw new NotSupportedException();
        public object SendWebRequest() => throw new NotSupportedException();
        public void Dispose() { }
    }

    public static class UnityWebRequestMultimedia {
        public static UnityWebRequest GetAudioClip(string uri, AudioType type) => throw new NotSupportedException();
    }

    public static class DownloadHandlerAudioClip {
        public static AudioClip GetContent(UnityWebRequest request) => throw new NotSupportedException();
    }
}

namespace Jlib.Runtime {
    public sealed class JlibDispatcher {
        public static JlibDispatcher Instance { get; } = new();
        public static void RequireMainThread(string operationName) { }
        public void StartCoroutine(IEnumerator routine) => throw new NotSupportedException();
    }
}

namespace KMod {
    [Flags]
    public enum Content { Animation = 1 }
    public sealed class Manager {
        public void Load(Content content) { }
    }
}

public sealed class KAnimFile {
    public string name = "";
    public Mod? mod;
    public sealed class Mod {
        public byte[] anim = [];
        public byte[] build = [];
        public readonly List<UnityEngine.Texture2D> textures = new();
    }
}

public static class Assets {
    public static readonly List<KAnimFile> ModLoadedKAnims = new();
    public static bool TryGetAnim(string name, out KAnimFile? value) {
        value = ModLoadedKAnims.FirstOrDefault(animation => animation.name == name);
        return value != null;
    }
    public static void LoadAnims() { }
}

public static class ModUtil {
    public static void AddKAnimMod(string name, KAnimFile.Mod mod) =>
        Assets.ModLoadedKAnims.Add(new KAnimFile { name = name, mod = mod });
}
