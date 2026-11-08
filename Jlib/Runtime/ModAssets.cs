/* Copyright 2026 Jlib contributors. SPDX-License-Identifier: MIT */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace Jlib.Runtime {
    /// <summary>Mod-owned Unity assets. All operations and callbacks run on the Unity main thread.</summary>
    public static class ModAssets {
        private sealed class Scope {
            internal readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
            internal readonly Dictionary<string, AssetBundle> Bundles = new Dictionary<string, AssetBundle>();
            internal readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
            internal int Generation;
        }
        private sealed class AnimRequest {
            internal readonly Assembly Owner;
            internal readonly string Name, Anim, Build;
            internal readonly string[] Textures;

            internal AnimRequest(Assembly owner, string name, string anim, string build, string[] textures) {
                Owner = owner;
                Name = name;
                Anim = anim;
                Build = build;
                Textures = (string[])textures.Clone();
            }
        }
        private static readonly Dictionary<string, Scope> scopes = new Dictionary<string, Scope>();
        private static readonly List<AnimRequest> animations = new List<AnimRequest>();
        private static bool animationPhaseStarted;
        private static bool animationLoading;
        private static string PathFor(Assembly owner, string path) => ModResources.ResolvePath(owner, path);
        private static Scope GetScope(Assembly owner) {
            string root = System.IO.Path.GetDirectoryName(PathFor(owner, "__jlib_owner__"));
            if (!scopes.TryGetValue(root, out var scope)) scopes.Add(root, scope = new Scope());
            return scope;
        }
        private static void MainThread() => JlibDispatcher.RequireMainThread("ModAssets");

        /// <summary>Loads and caches a PNG/JPEG as a centered sprite.</summary>
        public static Sprite LoadSprite(Assembly owner, string relativePath) {
            MainThread();
            string path = PathFor(owner, relativePath);
            var scope = GetScope(owner);
            if (scope.Sprites.TryGetValue(path, out var sprite) && sprite != null) return sprite;
            var texture = LoadTexture(path);
            try {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            } catch {
                UnityEngine.Object.Destroy(texture);
                throw;
            }
            scope.Sprites[path] = sprite;
            return sprite;
        }

        private static Texture2D LoadTexture(string path) {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            try {
                if (!texture.LoadImage(bytes))
                    throw new InvalidDataException("Invalid image: " + path);
                return texture;
            } catch {
                UnityEngine.Object.Destroy(texture);
                throw;
            }
        }
        /// <summary>Loads and caches a platform-compatible Unity AssetBundle.</summary>
        public static AssetBundle LoadBundle(Assembly owner, string relativePath) {
            MainThread();
            string path = PathFor(owner, relativePath);
            var scope = GetScope(owner);
            if (scope.Bundles.TryGetValue(path, out var bundle) && bundle != null) return bundle;
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidDataException("Unable to load AssetBundle: " + path);
            scope.Bundles[path] = bundle;
            return bundle;
        }
        /// <summary>Loads a named object from a cached bundle; throws if absent.</summary>
        public static T LoadBundleAsset<T>(Assembly owner, string relativePath, string assetName) where T : UnityEngine.Object {
            if (string.IsNullOrEmpty(assetName)) throw new ArgumentException("Asset name is required.", nameof(assetName));
            var asset = LoadBundle(owner, relativePath).LoadAsset<T>(assetName);
            if (asset == null) throw new FileNotFoundException("Asset not found in bundle: " + assetName, relativePath);
            return asset;
        }
        /// <summary>Asynchronously loads audio; callback receives either a clip or an exception.</summary>
        public static void LoadAudio(Assembly owner, string relativePath, AudioType type, Action<AudioClip, Exception> completed) {
            MainThread();
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            string path = PathFor(owner, relativePath);
            var scope = GetScope(owner);
            string key = path + "|" + type;
            if (scope.Clips.TryGetValue(key, out var cached) && cached != null) { completed(cached, null); return; }
            JlibDispatcher.Instance.StartCoroutine(ReadAudio(scope, path, key, type, scope.Generation, completed));
        }
        private static IEnumerator ReadAudio(Scope scope, string path, string key, AudioType type, int generation, Action<AudioClip, Exception> completed) {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type)) {
                yield return request.SendWebRequest();
                if (generation != scope.Generation) { completed(null, new OperationCanceledException("Mod assets were released.")); yield break; }
                if (request.result != UnityWebRequest.Result.Success) { completed(null, new IOException("Audio load failed: " + path + ": " + request.error)); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) { completed(null, new InvalidDataException("Invalid audio: " + path)); yield break; }
                if (scope.Clips.TryGetValue(key, out var existing) && existing != null) { UnityEngine.Object.Destroy(clip); clip = existing; }
                else scope.Clips[key] = clip;
                completed(clip, null);
            }
        }
        /// <summary>Plays a loaded clip using Unity audio, independent of FMOD event banks.</summary>
        public static void PlayOneShot(AudioSource source, AudioClip clip, float volume = 1f) {
            MainThread();
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            source.PlayOneShot(clip, volume);
        }
        /// <summary>Releases this Mod's cached resources. Call only after consumers stop using them.</summary>
        public static void Release(Assembly owner) {
            MainThread();
            var scope = GetScope(owner);
            scope.Generation++;
            foreach (var sprite in scope.Sprites.Values) if (sprite != null) { var texture = sprite.texture; UnityEngine.Object.Destroy(sprite); if (texture != null) UnityEngine.Object.Destroy(texture); }
            foreach (var clip in scope.Clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
            foreach (var bundle in scope.Bundles.Values) if (bundle != null) bundle.Unload(true);
            scope.Sprites.Clear(); scope.Clips.Clear(); scope.Bundles.Clear();
        }
        /// <summary>Queues KAnim files from arbitrary Mod-relative paths for the next startup animation load.</summary>
        public static void RegisterKAnim(Assembly owner, string name, string anim, string build, params string[] textures) {
            MainThread();
            if (animationPhaseStarted) throw new InvalidOperationException("KAnim registration must occur before animation loading begins.");
            if (string.IsNullOrWhiteSpace(name) || !name.EndsWith("_kanim", StringComparison.Ordinal)) throw new ArgumentException("Name must end in _kanim.", nameof(name));
            if (textures == null || textures.Length == 0) throw new ArgumentException("At least one texture is required.", nameof(textures));
            foreach (var request in animations) if (request.Name == name) throw new InvalidOperationException("KAnim already queued: " + name);
            PathFor(owner, anim); PathFor(owner, build);
            foreach (var texture in textures) PathFor(owner, texture);
            animations.Add(new AnimRequest(owner, name, anim, build, textures));
        }
        [HarmonyPatch(typeof(Assets), "LoadAnims")]
        private static class AnimationPhase {
            private static void Prefix() { animationPhaseStarted = true; animationLoading = true; }
            private static void Postfix() { animationLoading = false; }
            private static Exception Finalizer(Exception __exception) { animationLoading = false; return __exception; }
        }
        [HarmonyPatch(typeof(KMod.Manager), "Load")]
        private static class AnimationRegistration {
            private static void Postfix(KMod.Content content) {
                if (!animationLoading || (content & KMod.Content.Animation) == 0) return;
                foreach (var item in animations) {
                    KAnimFile.Mod mod = null;
                    bool registered = false;
                    try {
                        mod = new KAnimFile.Mod { anim = File.ReadAllBytes(PathFor(item.Owner, item.Anim)), build = File.ReadAllBytes(PathFor(item.Owner, item.Build)) };
                        foreach (var path in item.Textures)
                            mod.textures.Add(LoadTexture(PathFor(item.Owner, path)));
                        foreach (var existing in Assets.ModLoadedKAnims)
                            if (existing != null && existing.name == item.Name) throw new InvalidOperationException("KAnim name already exists: " + item.Name);
                        if (Assets.TryGetAnim(item.Name, out _)) throw new InvalidOperationException("KAnim name already exists: " + item.Name);
                        int count = Assets.ModLoadedKAnims.Count;
                        ModUtil.AddKAnimMod(item.Name, mod);
                        if (Assets.ModLoadedKAnims.Count == count) throw new InvalidOperationException("Game rejected KAnim: " + item.Name);
                        registered = true;
                    } catch (Exception e) { Debug.LogError("[Jlib] Failed to register KAnim " + item.Name + ": " + e); }
                    finally { if (!registered && mod != null) foreach (var image in mod.textures) if (image != null) UnityEngine.Object.Destroy(image); }
                }
            }
        }
    }
}
