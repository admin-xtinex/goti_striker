#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Builds the embeddable gameplay prefab: the full kit minus everything the host scene
    /// already owns (TurnManager, AudioManager, VFXManager, EventSystem, camera).
    /// TurnManager finds marbles and pits by scene-wide search, so dropping this into a scene
    /// is enough to make a match use the kit — no code change required.
    /// </summary>
    public static class BuildGameplayCore
    {
        const string Source = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit_MapReady.prefab";
        const string Output = "Assets/_Project/GameplayKit/Prefabs/GotiStriker_GameplayCore.prefab";
        const string Result = "Library/GameplayCore.result";

        // host scene already provides these — keeping them would duplicate singletons
        static readonly string[] DropChildren = { "TurnSystem", "Audio", "VFX", "Cameras", "Input" };

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Build Gameplay Core Prefab")]
        public static void BuildMenu() => Build(false);

        public static void BuildBatch()
        {
            try { Build(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Build(bool batch)
        {
            var sb = new StringBuilder();
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (src == null) throw new System.Exception("missing " + Source);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "GotiStriker_GameplayCore";
            go.transform.position = Vector3.zero;

            var gameplay = go.transform.Find("Gameplay");
            if (gameplay == null) throw new System.Exception("no Gameplay group in kit");

            foreach (var name in DropChildren)
            {
                var t = gameplay.Find(name);
                if (t != null) { sb.AppendLine($"dropped Gameplay/{name}"); Object.DestroyImmediate(t.gameObject); }
                else sb.AppendLine($"(Gameplay/{name} not present)");
            }

            // HUD holds both an EventSystem (scene has one) and UI_ShotControl (scene lacks it):
            // keep the shot UI, drop the duplicate EventSystem.
            var hud = gameplay.Find("HUD");
            if (hud != null)
            {
                var es = hud.Find("EventSystem");
                if (es != null) { sb.AppendLine("dropped Gameplay/HUD/EventSystem (scene already has one)"); Object.DestroyImmediate(es.gameObject); }
                var shotUI = hud.Find("UI_ShotControl");
                sb.AppendLine($"kept Gameplay/HUD/UI_ShotControl = {shotUI != null}");
            }

            sb.AppendLine("\nremaining structure:");
            Walk(go.transform, 0, 2, sb);

            int marbles = go.GetComponentsInChildren<PitStriker.Physics.MarbleController>(true).Length;
            int pits = go.GetComponentsInChildren<PitStriker.Gameplay.PitZone>(true).Length;
            int turnMgrs = go.GetComponentsInChildren<PitStriker.Gameplay.TurnManager>(true).Length;
            int cams = go.GetComponentsInChildren<Camera>(true).Length;
            int listeners = go.GetComponentsInChildren<AudioListener>(true).Length;
            sb.AppendLine($"\nmarbles={marbles} pits={pits} turnManagers={turnMgrs} cameras={cams} audioListeners={listeners}");
            sb.AppendLine("(turnManagers/cameras/audioListeners must all be 0)");

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            PrefabUtility.SaveAsPrefabAsset(go, Output);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine("saved " + Output);
            sb.AppendLine("CORE_BUILD_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[CORE]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Gameplay Core", sb.ToString(), "OK");
        }

        static void Walk(Transform t, int d, int max, StringBuilder sb)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                sb.AppendLine($"{new string(' ', (d + 1) * 2)}{c.name}");
                if (d < max) Walk(c, d + 1, max, sb);
            }
        }
    }
}
#endif
