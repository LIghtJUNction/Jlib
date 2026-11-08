using System.Reflection;
using Jlib.Runtime;
using PeterHan.PLib.Core;
using UnityEngine;

internal static class ModAssetsCoverage {
    internal static void Run() {
        string root = Path.Combine(Path.GetTempPath(), "Jlib.Assets." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var owner = Program.NewAssembly();
        PUtil.RegisterModAssembly(owner, Program.NewMod(root));
        Texture2D.Created.Clear();
        Texture2D.Decode = data => data[0] switch {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException("Image decoder failed.")
        };
        try {
            File.WriteAllBytes(Path.Combine(root, "valid.png"), [1]);
            File.WriteAllBytes(Path.Combine(root, "invalid.png"), [0]);
            File.WriteAllBytes(Path.Combine(root, "throws.png"), [2]);
            File.WriteAllBytes(Path.Combine(root, "anim.bytes"), [1]);
            File.WriteAllBytes(Path.Combine(root, "build.bytes"), [1]);
            SpriteLifetime(owner);
            KAnimLifetime(owner);
        } finally {
            Sprite.CreateFailure = null;
            Texture2D.Decode = _ => true;
            ModAssets.Release(owner);
            foreach (var animation in Assets.ModLoadedKAnims)
                foreach (var texture in animation.mod!.textures)
                    UnityEngine.Object.Destroy(texture);
            Assets.ModLoadedKAnims.Clear();
            Directory.Delete(root, true);
        }
    }

    private static void SpriteLifetime(Assembly owner) {
        int created = Texture2D.Created.Count;
        Program.Throws<FileNotFoundException>(() => ModAssets.LoadSprite(owner, "missing.png"),
            "missing sprite file propagates");
        Program.Equal(created, Texture2D.Created.Count, "missing sprite allocates no texture");

        Program.Throws<InvalidDataException>(() => ModAssets.LoadSprite(owner, "invalid.png"),
            "invalid image is rejected");
        Program.Equal(1, Texture2D.Created[^1].DestroyCount, "rejected image texture is destroyed exactly once");
        Program.Throws<InvalidDataException>(() => ModAssets.LoadSprite(owner, "throws.png"),
            "image decoder exception propagates");
        Program.Equal(1, Texture2D.Created[^1].DestroyCount, "decoder exception destroys its allocated texture");

        Sprite.CreateFailure = new InvalidOperationException("Sprite creation failed.");
        Program.Throws<InvalidOperationException>(() => ModAssets.LoadSprite(owner, "valid.png"),
            "sprite creation exception propagates");
        Program.Equal(1, Texture2D.Created[^1].DestroyCount, "sprite creation failure destroys its texture");
        Sprite.CreateFailure = null;

        var sprite = ModAssets.LoadSprite(owner, "valid.png");
        created = Texture2D.Created.Count;
        Program.Equal(sprite, ModAssets.LoadSprite(owner, "./valid.png"), "normalized sprite path shares the cache");
        Program.Equal(created, Texture2D.Created.Count, "cached sprite allocates no extra texture");
        Program.Equal(0, sprite.texture.DestroyCount, "successful sprite retains its texture");
        ModAssets.Release(owner);
        Program.Equal(1, sprite.DestroyCount, "release destroys the cached sprite");
        Program.Equal(1, sprite.texture.DestroyCount, "release destroys the cached texture");
        ModAssets.Release(owner);
        Program.Equal(1, sprite.texture.DestroyCount, "repeated release does not destroy a texture twice");
        Program.Check(!ReferenceEquals(sprite, ModAssets.LoadSprite(owner, "valid.png")),
            "released sprite is loaded anew");
        ModAssets.Release(owner);
    }

    private static void KAnimLifetime(Assembly owner) {
        ModAssets.RegisterKAnim(owner, "invalid_kanim", "anim.bytes", "build.bytes", "valid.png", "invalid.png");
        ModAssets.RegisterKAnim(owner, "throws_kanim", "anim.bytes", "build.bytes", "valid.png", "throws.png");
        ModAssets.RegisterKAnim(owner, "valid_kanim", "anim.bytes", "build.bytes", "valid.png");
        int first = Texture2D.Created.Count;
        int errors = Debug.Errors.Count;
        Patch("AnimationPhase", "Prefix");
        try {
            Patch("AnimationRegistration", "Postfix", KMod.Content.Animation);
        } finally {
            Patch("AnimationPhase", "Postfix");
        }
        Program.Equal(first + 5, Texture2D.Created.Count, "three KAnim requests reach their texture boundaries");
        for (int i = first; i < first + 4; i++)
            Program.Equal(1, Texture2D.Created[i].DestroyCount,
                "failed KAnim destroys each partial texture exactly once: " + (i - first));
        Program.Equal(errors + 2, Debug.Errors.Count, "each failed KAnim is reported");
        Program.Equal(1, Assets.ModLoadedKAnims.Count, "failed KAnims do not prevent the valid request");
        Program.Equal("valid_kanim", Assets.ModLoadedKAnims[0].name, "only the valid KAnim is registered");
        var acceptedTexture = Assets.ModLoadedKAnims[0].mod!.textures[0];
        ModAssets.Release(owner);
        Program.Equal(0, acceptedTexture.DestroyCount, "game-owned KAnim survives ModAssets.Release");
    }

    private static void Patch(string type, string method, params object[] arguments) =>
        typeof(ModAssets).GetNestedType(type, BindingFlags.NonPublic)!
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments);
}
