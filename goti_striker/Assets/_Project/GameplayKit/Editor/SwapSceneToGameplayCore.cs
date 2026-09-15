#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitStriker.Physics;
using PitStriker.Gameplay;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Replaces the scene's baked gameplay (Arena_Sandbox + Foundation_Gameplay) with the
    /// portable gameplay core prefab. TurnManager discovers marbles/pits by scene search,
    /// so the match picks up the kit with no code change.
    /// </summary>
    public static class SwapSceneToGameplayCore
    {
        const string Entry = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string Core = "Assets/_Project/GameplayKit/Prefabs/GotiStriker_GameplayCore.prefab";
        const string Result = "Library/SceneSwap.result";

        static readonly string[] Replace = { "Arena_Sandbox", "Foundation_Gameplay" };

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Swap Scene To Gameplay Core")]
        public static void SwapMenu() => Swap(false);

        public static void SwapBatch()
        {
            try { Swap(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Swap(bool batch)
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(Entry, OpenSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Core);
            if (prefab == null) throw new System.Exception("missing " + Core);

            sb.AppendLine("before: marbles=" + Object.FindObjectsByType<MarbleController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
                        + " pits=" + Object.FindObjectsByType<PitZone>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var name in Replace)
                {
                    if (root.name != name) continue;
                    int m = root.GetComponentsInChildren<MarbleController>(true).Length;
                    int p = root.GetComponentsInChildren<PitZone>(true).Length;
                    sb.AppendLine($"removed '{root.name}' (marbles={m} pits={p})");
                    Object.DestroyImmediate(root);
                    break;
                }
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "GotiStriker_GameplayCore";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sb.AppendLine("added prefab instance 'GotiStriker_GameplayCore' at origin");

            int marbles = Object.FindObjectsByType<MarbleController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int pits = Object.FindObjectsByType<PitZone>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int turnMgrs = Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            sb.AppendLine($"\nafter: marbles={marbles} pits={pits} turnManagers={turnMgrs} cameras={cams} audioListeners={listeners}");
            sb.AppendLine("(expect marbles=4 pits=3 turnManagers=1 cameras=1 audioListeners=1)");

            sb.AppendLine("\nscene roots:");
            foreach (var root in scene.GetRootGameObjects())
                sb.AppendLine($"  {root.name,-38} active={root.activeSelf}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sb.AppendLine("SCENE_SWAP_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[SWAP]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Scene Swap", sb.ToString(), "OK");
        }
    }
}
#endif
