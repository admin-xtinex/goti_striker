#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// The shot-control UI had Image/Text/CanvasRenderer components but no Canvas anywhere in
    /// its parent chain, so it never rendered and never received taps — the Ground/Loft toggle,
    /// power area and finger tutorial were invisible and dead. This adds the missing
    /// Canvas + CanvasScaler + GraphicRaycaster to the UI root.
    /// </summary>
    public static class FixShotControlCanvas
    {
        const string UIPrefab = "Assets/_Project/GameplayKit/UI/Prefabs/UI_ShotControl.prefab";
        const string Result = "Library/GameplayKit_CanvasFix.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Fix Shot Control Canvas")]
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
            var root = PrefabUtility.LoadPrefabContents(UIPrefab);
            try
            {
                sb.AppendLine("before: Canvas=" + (root.GetComponent<Canvas>() != null)
                            + " Scaler=" + (root.GetComponent<CanvasScaler>() != null)
                            + " Raycaster=" + (root.GetComponent<GraphicRaycaster>() != null));

                var canvas = root.GetComponent<Canvas>();
                if (canvas == null) canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                // landscape mobile: bias toward height so controls keep their reach on tall aspects
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();

                // The Ground/Loft pills had raycastTarget off, so the UI raycaster ignored them
                // and taps passed straight through — the toggle could never be clicked.
                foreach (string pill in new[] { "ShotModeToggle/Pill_Ground", "ShotModeToggle/Pill_Loft" })
                {
                    var t = root.transform.Find(pill);
                    if (t == null) { sb.AppendLine($"  {pill}: NOT FOUND"); continue; }
                    var img = t.GetComponent<Image>();
                    if (img == null) { sb.AppendLine($"  {pill}: no Image"); continue; }
                    sb.AppendLine($"  {pill}: raycastTarget {img.raycastTarget} -> true");
                    img.raycastTarget = true;
                }

                sb.AppendLine("after : Canvas=" + (root.GetComponent<Canvas>() != null)
                            + " Scaler=" + (root.GetComponent<CanvasScaler>() != null)
                            + " Raycaster=" + (root.GetComponent<GraphicRaycaster>() != null));
                sb.AppendLine("renderMode=" + canvas.renderMode + " refRes=" + scaler.referenceResolution);

                PrefabUtility.SaveAsPrefabAsset(root, UIPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("CANVAS_FIX_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[CANVASFIX]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Shot Control Canvas", sb.ToString(), "OK");
        }
    }
}
#endif
