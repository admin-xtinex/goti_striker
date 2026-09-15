#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Removes the GROUND/LOFT toggle from the shot-control UI.
    /// Shot type is now chosen by swipe direction (backward = Ground, forward = Loft),
    /// so the toggle is redundant.
    /// </summary>
    public static class RemoveShotModeToggle
    {
        const string UIPrefab = "Assets/_Project/GameplayKit/UI/Prefabs/UI_ShotControl.prefab";
        const string Result = "Library/GameplayKit_ToggleRemoved.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Remove Ground-Loft Toggle")]
        public static void RemoveMenu() => Remove(false);

        public static void RemoveBatch()
        {
            try { Remove(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Remove(bool batch)
        {
            var sb = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(UIPrefab);
            try
            {
                var toggle = root.transform.Find("ShotModeToggle");
                if (toggle == null) sb.AppendLine("ShotModeToggle: already absent");
                else
                {
                    sb.AppendLine($"ShotModeToggle: removing ({toggle.childCount} children)");
                    Object.DestroyImmediate(toggle.gameObject);
                }

                // clear the binder's now-dangling references
                var binder = root.GetComponent<PitStriker.GameplayKit.UI.ShotControlBinder>();
                if (binder != null)
                {
                    binder.GroundButton = null; binder.LoftButton = null;
                    binder.GroundPill = null; binder.LoftPill = null;
                    sb.AppendLine("ShotControlBinder: toggle references cleared");
                }

                sb.AppendLine($"remaining children: {root.transform.childCount}");
                for (int i = 0; i < root.transform.childCount; i++)
                    sb.AppendLine($"  - {root.transform.GetChild(i).name}");

                PrefabUtility.SaveAsPrefabAsset(root, UIPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("TOGGLE_REMOVED_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[TOGGLE]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Remove Toggle", sb.ToString(), "OK");
        }
    }
}
#endif
