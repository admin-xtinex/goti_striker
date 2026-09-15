#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Removes the village map from the project: first deletes the (already inactive)
    /// environment roots from the entry scene, then deletes map art that nothing references
    /// any more. Assets the gameplay kit needs — the pit saucer mesh, marble materials,
    /// shaders — live inside the same folders, so they are kept by dependency, not by path.
    /// </summary>
    public static class StripMapArt
    {
        const string Entry = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string Core = "Assets/_Project/GameplayKit/Prefabs/GotiStriker_GameplayCore.prefab";
        const string Result = "Library/StripMapArt.result";

        static readonly string[] EnvRoots = {
            "Environment_Blender_Village_Graphics",
            "Village_Reference_Upgrade",
            "Phase2_P0_Dressing",
            "Phase2_P1_Dressing",
        };

        // only prune inside these; everything else is left alone
        static readonly string[] PruneUnder = {
            "Assets/_Project/Art/Environments/",
            "Assets/_Project/Art/Models/Phase2_P0/",
            "Assets/_Project/Art/Models/Phase2_P1/",
            "Assets/_Project/Art/Models/VillageKit/",
            "Assets/_Project/Art/Models/Foundation/",
            "Assets/_Project/Art/Meshes/",
            "Assets/_Project/Art/Textures/Foundation/",
        };

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Strip Village Map Art")]
        public static void StripMenu() => Strip(false);

        public static void StripBatch()
        {
            try { Strip(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Strip(bool batch)
        {
            var sb = new StringBuilder();

            // 1. drop the inactive environment roots from the scene
            var scene = EditorSceneManager.OpenScene(Entry, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects().ToList())
            {
                if (!EnvRoots.Contains(root.name)) continue;
                int r = root.GetComponentsInChildren<MeshRenderer>(true).Length;
                sb.AppendLine($"scene: removed '{root.name}' ({r} renderers, was active={root.activeSelf})");
                Object.DestroyImmediate(root);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sb.AppendLine("\nscene roots now:");
            foreach (var root in scene.GetRootGameObjects())
                sb.AppendLine($"  {root.name,-34} active={root.activeSelf}");

            // 2. recompute what is still needed
            var keepRoots = new[] { Entry, Core }.Where(File.Exists).ToArray();
            var keep = new HashSet<string>(AssetDatabase.GetDependencies(keepRoots, true));
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/GameplayKit" }))
                foreach (var d in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(g), true))
                    keep.Add(d);

            sb.AppendLine($"\nstill-required assets: {keep.Count}");

            // 3. delete unreferenced map art
            var doomed = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Where(p => PruneUnder.Any(pre => p.StartsWith(pre)))
                .Where(p => !keep.Contains(p))
                .ToList();

            long freed = doomed.Sum(p => { var fi = new FileInfo(p); return fi.Exists ? fi.Length : 0; });
            sb.AppendLine($"deleting {doomed.Count} unreferenced map files ({freed / 1048576.0:F1} MB)");

            var kept = AssetDatabase.GetAllAssetPaths()
                .Where(p => PruneUnder.Any(pre => p.StartsWith(pre)) && keep.Contains(p)).ToList();
            sb.AppendLine($"\nKEPT inside map folders because the kit needs them ({kept.Count}):");
            foreach (var k in kept.OrderBy(x => x)) sb.AppendLine("  " + k);

            AssetDatabase.StartAssetEditing();
            try { foreach (var p in doomed) AssetDatabase.DeleteAsset(p); }
            finally { AssetDatabase.StopAssetEditing(); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine("\nSTRIP_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[STRIP]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Strip Map Art", $"Deleted {doomed.Count} files ({freed / 1048576.0:F1} MB)", "OK");
        }
    }
}
#endif
