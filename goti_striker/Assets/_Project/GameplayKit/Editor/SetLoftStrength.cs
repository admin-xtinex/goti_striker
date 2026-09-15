#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Sets the loft arc strength on the kit prefabs.
    /// GetLoftPitch = Lerp(LoftMinAngle, LoftMaxAngle, power) * LoftVerticalForce, so
    /// LoftVerticalForce scales the whole launch angle. At the old 0.45 the arc peaked at
    /// 2.14 m versus 1.55 m for a ground shot — too close to read as a parabola.
    /// </summary>
    public static class SetLoftStrength
    {
        public const float LoftVerticalForce = 1.20f;   // ~33 deg at full power, ~6.0 m peak

        static readonly string[] Prefabs = {
            "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit_MapReady.prefab",
            "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab",
        };
        const string Result = "Library/GameplayKit_Loft_Set.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Set Loft Strength")]
        public static void ApplyMenu() => Apply(false);

        public static void ApplyBatch()
        {
            try { Apply(true); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        static void Apply(bool batch)
        {
            var sb = new StringBuilder();
            foreach (string path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { sb.AppendLine($"{path}: MISSING"); continue; }
                try
                {
                    var cfg = root.GetComponentInChildren<ShotModeConfig>(true);
                    if (cfg == null) { sb.AppendLine($"{Path.GetFileName(path)}: no ShotModeConfig"); continue; }
                    sb.AppendLine($"{Path.GetFileName(path)}: LoftVerticalForce {cfg.LoftVerticalForce} -> {LoftVerticalForce}");
                    cfg.LoftVerticalForce = LoftVerticalForce;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("LOFT_SET_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[LOFT SET]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Loft Strength", sb.ToString(), "OK");
        }
    }
}
#endif
