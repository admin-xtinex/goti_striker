#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Exports the gameplay kit as a .unitypackage.
    /// A gameplay prefab is not a model: exporting it as FBX would keep only meshes and
    /// drop every MonoBehaviour, reference and collider. Use this, or copy the folder.
    /// </summary>
    public static class ExportGameplayKitPackage
    {
        const string KitFolder = "Assets/_Project/GameplayKit";
        const string ScriptsFolder = "Assets/_Project/Scripts";
        const string OutFile = "PitStriker_GameplayKit.unitypackage";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Export Kit .unitypackage")]
        public static void ExportMenu() => Export(false);

        public static void ExportBatch()
        {
            try { Export(true); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        static void Export(bool batch)
        {
            // Scripts are included because the prefab's components live there; without them
            // the imported prefab arrives with "Missing (Mono Script)" on every object.
            string[] roots = { KitFolder, ScriptsFolder };
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", OutFile));

            AssetDatabase.ExportPackage(
                roots,
                outPath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

            var fi = new FileInfo(outPath);
            string msg = $"Exported {outPath}\nsize={(fi.Exists ? fi.Length / 1024 + " KB" : "MISSING")}";
            File.WriteAllText("Library/GameplayKit_Export.result", msg);
            Debug.Log("[EXPORT] " + msg);
            if (!batch) EditorUtility.RevealInFinder(outPath);
        }
    }
}
#endif
