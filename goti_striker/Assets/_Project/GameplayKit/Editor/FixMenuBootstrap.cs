#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// MenuManager is a component ON 'Canvas_GameScreens', but that object was saved
    /// inactive, so Awake() never ran, MenuManager.Instance stayed null and the whole
    /// menu system (Home / PlayMode / Online / Settings / Pause) never appeared.
    /// Nothing in the codebase re-enables it. This switches it on and saves the scene.
    /// </summary>
    public static class FixMenuBootstrap
    {
        const string Entry = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string Result = "Library/MenuBootstrap.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Fix Menu Bootstrap")]
        public static void FixMenu() => Fix(false);

        public static void FixBatch()
        {
            try { Fix(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Fix(bool batch)
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(Entry, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Canvas_GameScreens") continue;
                sb.AppendLine($"{root.name}: activeSelf {root.activeSelf} -> true");
                root.SetActive(true);
                EditorUtility.SetDirty(root);
            }

            // report every root so the saved state is visible in the log
            sb.AppendLine("\nscene roots after fix:");
            foreach (var root in scene.GetRootGameObjects())
                sb.AppendLine($"  {root.name,-34} active={root.activeSelf}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sb.AppendLine("MENU_BOOTSTRAP_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[MENUBOOT]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Menu Bootstrap", sb.ToString(), "OK");
        }
    }
}
#endif
