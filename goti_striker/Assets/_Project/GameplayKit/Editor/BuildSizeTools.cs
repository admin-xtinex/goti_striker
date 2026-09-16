#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Build size helpers. Settings and scene content only - no gameplay code.
    ///   ReportBatch      lists what the build scenes and Resources pull in, with an Android size
    ///                    estimate and which scene object uses each asset.
    ///   ApplySoilLimit   caps the soil textures at 1024 px for Android (other platforms unchanged).
    /// </summary>
    public static class BuildSizeTools
    {
        static readonly string[] SoilTextures =
        {
            "Assets/PlanExample_Soil_Better_Unity6_6/Soil_BaseColor_2K.png",
            "Assets/PlanExample_Soil_Better_Unity6_6/Soil_Normal_2K.png",
            "Assets/PlanExample_Soil_Better_Unity6_6/Soil_AO_2K.png",
        };

        [MenuItem("Pit Striker/Build Size/Limit Soil Textures To 1024 On Android")]
        public static void ApplySoilLimit()
        {
            foreach (var path in SoilTextures)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogWarning("[SIZE] missing " + path); continue; }
                var android = ti.GetPlatformTextureSettings("Android");
                if (android.overridden && android.maxTextureSize == 1024) continue;
                android.overridden = true;
                android.maxTextureSize = 1024;
                if (android.format == TextureImporterFormat.Automatic) android.format = TextureImporterFormat.Automatic;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
                Debug.Log($"[SIZE] {Path.GetFileName(path)}: Android max size 1024");
            }
        }

        public static void ApplySoilLimitBatch()
        {
            ApplySoilLimit();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string OldSoilMaterial = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Soil.mat";
        static readonly string[] OldSwipeSprites =
        {
            "Assets/_Project/GameplayKit/UI/Sprites/SwipeShootPanel.png",
            "Assets/_Project/GameplayKit/UI/Sprites/SwipeShootThumb.png",
        };
        static readonly string[] SwipePrefabs =
        {
            "Assets/_Project/GameplayKit/UI/Prefabs/UI_ShotControl.prefab",
            "Assets/_Project/GameplayKit/Prefabs/GotiStriker_GameplayCore.prefab",
        };

        /// <summary>
        /// Drops art the game no longer shows from the build, and the duplicate music:
        ///   - the inactive TemporaryGround_PlaceholderMap (replaced by the soil ground);
        ///   - scene overrides pointing hidden kit objects at the old soil material;
        ///   - AudioManager's scene links to the music in _Project/Audio, which duplicate the
        ///     byte-identical files in Resources/Music that AudioManager loads when unlinked;
        ///   - the old swipe bar sprites on the now-transparent images (their rects, and so their
        ///     touch areas, are unchanged).
        /// </summary>
        public static void CleanupLeftoversBatch()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "TemporaryGround_PlaceholderMap").ToList())
            {
                sb.AppendLine($"removed inactive {root.name} (active={root.activeSelf})");
                UnityEngine.Object.DestroyImmediate(root);
            }

            var oldSoil = AssetDatabase.LoadAssetAtPath<Material>(OldSoilMaterial);
            foreach (var kit in UnityEngine.Object.FindObjectsByType<GameplayKitRoot>(FindObjectsInactive.Include))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(kit)) continue;
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(kit);
                var mods = PrefabUtility.GetPropertyModifications(root);
                if (mods == null || oldSoil == null) continue;
                var kept = mods.Where(m => m.objectReference != oldSoil).ToArray();
                if (kept.Length != mods.Length)
                {
                    PrefabUtility.SetPropertyModifications(root, kept);
                    sb.AppendLine($"reverted {mods.Length - kept.Length} old soil material overrides on {root.name}");
                }
            }

            foreach (var am in UnityEngine.Object.FindObjectsByType<PitStriker.Audio.AudioManager>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(am);
                foreach (var (field, resource) in new[] { ("_startMusicClip", "Music/Music_Start"), ("_tossMusicClip", "Music/Music_Toss"), ("_gameplayMusicClip", "Music/Music_Gameplay") })
                {
                    var prop = so.FindProperty(field);
                    if (prop == null || prop.objectReferenceValue == null) continue;
                    string linked = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                    string resPath = "Assets/Resources/" + resource + ".mp3";
                    if (!File.Exists(resPath) || !SameBytes(linked, resPath)) { sb.AppendLine($"kept {field}: no identical Resources copy"); continue; }
                    prop.objectReferenceValue = null;
                    sb.AppendLine($"AudioManager.{field}: unlinked {linked} (plays identical {resPath})");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var oldSprites = OldSwipeSprites.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToList();
            foreach (var prefabPath in SwipePrefabs)
            {
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                int cleared = 0;
                foreach (var img in contents.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (img.sprite == null || !oldSprites.Contains(img.sprite)) continue;
                    img.sprite = null;
                    cleared++;
                }
                if (cleared > 0) { PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); sb.AppendLine($"{Path.GetFileName(prefabPath)}: cleared {cleared} old swipe sprites"); }
                PrefabUtility.UnloadPrefabContents(contents);
            }

            ApplySoilLimit();
            sb.AppendLine("soil textures: Android max 1024");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/BuildSize.cleanup", sb.ToString());
            EditorApplication.Exit(0);
        }

        static bool SameBytes(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b)) return false;
            var fa = new FileInfo(a); var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
        }

        public static void ReportBatch()
        {
            File.WriteAllText("Library/BuildSize.report", Report());
            EditorApplication.Exit(0);
        }

        static string Report()
        {
            var sb = new StringBuilder();
            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && !p.Contains("/Editor/") && !AssetDatabase.IsValidFolder(p)).ToList();

            var deps = new HashSet<string>(AssetDatabase.GetDependencies(roots.Concat(resources).ToArray(), true));
            var rows = new List<(string path, long bytes, string type)>();
            foreach (var p in deps)
            {
                if (p.EndsWith(".cs") || p.EndsWith(".unity") || p.EndsWith(".dll") || p.EndsWith(".asmdef")) continue;
                rows.Add((p, Estimate(p, out string type), type));
            }
            long total = rows.Sum(r => r.bytes);
            sb.AppendLine($"Build scenes: {string.Join(", ", roots)}; Resources assets: {resources.Count}");
            sb.AppendLine($"Estimated asset payload (uncompressed, Android): {total / 1048576f:F1} MB over {rows.Count} assets");
            sb.AppendLine();

            sb.AppendLine("By top-level folder:");
            foreach (var g in rows.GroupBy(r => Folder(r.path)).OrderByDescending(g => g.Sum(r => r.bytes)).Take(25))
                sb.AppendLine($"  {g.Sum(r => r.bytes) / 1048576f,7:F2} MB  {g.Count(),4} assets  {g.Key}");
            sb.AppendLine();

            // Who references each large asset in the gameplay scene.
            var users = SceneUsers(roots.LastOrDefault());
            sb.AppendLine("Largest assets:");
            foreach (var r in rows.OrderByDescending(r => r.bytes).Take(60))
            {
                users.TryGetValue(r.path, out var who);
                string whoText = who == null ? "" : "  <- " + string.Join(", ", who.Take(3)) + (who.Count > 3 ? $" (+{who.Count - 3})" : "");
                sb.AppendLine($"  {r.bytes / 1048576f,7:F2} MB  {r.type,-10} {r.path}{whoText}");
            }
            return sb.ToString();
        }

        static string Folder(string path)
        {
            var parts = path.Split('/');
            return parts.Length > 3 ? string.Join("/", parts.Take(3)) : string.Join("/", parts.Take(parts.Length - 1));
        }

        static long Estimate(string path, out string type)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            type = obj != null ? obj.GetType().Name : "?";
            if (obj is Texture2D tex)
            {
                // Storage size for the active build target (Android), via the editor's internal utility.
                var util = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
                var m = util?.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null) return (long)m.Invoke(null, new object[] { tex });
            }
            if (AssetImporter.GetAtPath(path) is AudioImporter ai)
            {
                var s = ai.GetOverrideSampleSettings("Android");
                var size = typeof(AudioImporter).GetProperty("compSize", BindingFlags.Instance | BindingFlags.NonPublic);
                if (size != null) return Convert.ToInt64(size.GetValue(ai));
            }
            if (obj != null)
            {
                long runtime = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(obj);
                if (obj is GameObject || obj is Material || obj is Shader) return Math.Min(runtime, new FileInfo(path).Length);
                if (runtime > 0) return runtime;
            }
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        /// <summary>Asset path -> names of scene objects referencing it (directly or via their prefab/material).</summary>
        static Dictionary<string, List<string>> SceneUsers(string scenePath)
        {
            var map = new Dictionary<string, List<string>>();
            if (string.IsNullOrEmpty(scenePath)) return map;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var objs = EditorUtility.CollectDependencies(t.GetComponents<Component>().Cast<UnityEngine.Object>().ToArray());
                    string label = $"{root.name}{(root.activeSelf ? "" : "[inactive]")}/{t.name}{(t.gameObject.activeInHierarchy ? "" : "[inactive]")}";
                    foreach (var o in objs)
                    {
                        string p = AssetDatabase.GetAssetPath(o);
                        if (string.IsNullOrEmpty(p) || p.EndsWith(".cs")) continue;
                        if (!map.TryGetValue(p, out var list)) map[p] = list = new List<string>();
                        if (!list.Contains(label) && list.Count < 12) list.Add(label);
                    }
                }
            }
            return map;
        }
    }
}
#endif
