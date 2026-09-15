#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PitStriker.Input;
using PitStriker.CameraSystem;
using PitStriker.GameplayKit.UI;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Installs the drag-anywhere camera and the swipe-to-shoot panel.
    ///
    /// Rebuilds UI_ShotControl with the supplied panel/thumb art, attaches CameraDragInput beside
    /// the follow camera in both the gameplay prefab and the map scene, and then audits the
    /// things that silently break this routing — a full-screen raycast target over the play view
    /// being the main one, since it would swallow every camera drag without any error.
    /// </summary>
    public static class ApplySwipeShootControls
    {
        const string CorePrefab = "Assets/_Project/GameplayKit/Prefabs/GotiStriker_GameplayCore.prefab";
        const string Scene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string Result = "Library/SwipeShootControls.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Apply Swipe-Shoot Controls")]
        public static void RunMenu() => Run(false);

        public static void RunBatch()
        {
            try { Run(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Run(bool batch)
        {
            var sb = new StringBuilder();

            // 1. UI prefab with the new art -------------------------------------------------
            BuildShotControlUIPrefab.Run();
            sb.AppendLine("rebuilt UI_ShotControl with SwipeShootPanel + SwipeShootThumb");

            // 2. Camera drag component in the gameplay prefab --------------------------------
            var contents = PrefabUtility.LoadPrefabContents(CorePrefab);
            try
            {
                int changed = EnsureCameraDragInput(contents.transform, sb, "prefab");
                changed += PatchShotControl(contents.transform, sb, "prefab");
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(contents, CorePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            // 3. Same in the live scene -------------------------------------------------------
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int sceneAdded = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                sceneAdded += EnsureCameraDragInput(root.transform, sb, "scene");
                sceneAdded += PatchShotControl(root.transform, sb, "scene");
            }

            if (sceneAdded > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            // 4. Audit what would silently break the routing ----------------------------------
            AuditRaycastBlockers(scene, sb);

            AssetDatabase.SaveAssets();
            sb.AppendLine("SWIPE_CONTROLS_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[SWIPECTRL]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Swipe-Shoot Controls", sb.ToString(), "OK");
        }

        // Reference-pixel geometry; mirrors BuildShotControlUIPrefab so prefab and patched
        // instances end up identical. Panel art is 941x1672, so 200 wide => 356 tall.
        const float PanelWidth = 200f, PanelHeight = 356f, PanelMargin = 84f, ThumbSize = 96f;
        const string PanelSprite = "Assets/_Project/GameplayKit/UI/Sprites/SwipeShootPanel.png";
        const string ThumbSprite = "Assets/_Project/GameplayKit/UI/Sprites/SwipeShootThumb.png";

        /// <summary>
        /// Brings an already-placed UI_ShotControl up to the new layout in place, rather than
        /// swapping the whole subtree for a fresh prefab instance — the existing one carries
        /// wiring from other tools (toggle, power fill, tutorial) that a wholesale replace would
        /// silently drop.
        /// </summary>
        static int PatchShotControl(Transform root, StringBuilder sb, string where)
        {
            int changed = 0;

            foreach (var binder in root.GetComponentsInChildren<ShotControlBinder>(true))
            {
                if (where == "scene" && EditorUtility.IsPersistent(binder)) continue;

                var area = binder.transform.Find("PowerArea") as RectTransform;
                if (area == null) { sb.AppendLine($"[{where}] {binder.name}: no PowerArea, skipped"); continue; }

                // Compact strip on the right edge, vertically centred: away from the lane down
                // the middle of the screen, so it never sits over the marble or the pits.
                area.anchorMin = area.anchorMax = area.pivot = new Vector2(1f, 0.5f);
                area.anchoredPosition = new Vector2(-PanelMargin, 0f);
                area.sizeDelta = new Vector2(PanelWidth, PanelHeight);

                var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
                var thumbSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThumbSprite);
                if (panelSprite == null || thumbSprite == null)
                {
                    sb.AppendLine($"[{where}] sprites missing — panel={panelSprite != null} thumb={thumbSprite != null}");
                    continue;
                }

                var panel = EnsureImage(area, "PanelImage", panelSprite, true);
                panel.preserveAspect = true;
                var prt = panel.rectTransform;
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                panel.transform.SetSiblingIndex(0);   // behind the fill and the thumb

                var thumb = EnsureImage(area, "Thumb", thumbSprite, false);
                thumb.preserveAspect = true;
                var trt = thumb.rectTransform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
                trt.anchoredPosition = Vector2.zero;
                trt.sizeDelta = new Vector2(ThumbSize, ThumbSize);
                thumb.transform.SetAsLastSibling();   // always on top of the track

                binder.PanelImage = panel;
                binder.Thumb = trt;
                binder.PowerArea = area;

                // The old frame art is superseded by the supplied panel. Left in place but made
                // invisible and non-interactive, so nothing that references it breaks.
                var frame = area.Find("Frame");
                if (frame != null)
                {
                    var fi = frame.GetComponent<Image>();
                    if (fi != null) { fi.enabled = false; fi.raycastTarget = false; }
                }

                EditorUtility.SetDirty(binder);
                sb.AppendLine($"[{where}] patched {HierarchyPath(binder.transform)}: panel+thumb wired, "
                            + $"area {PanelWidth}x{PanelHeight} at right edge");
                changed++;
            }

            return changed;
        }

        static Image EnsureImage(Transform parent, string name, Sprite sprite, bool raycast)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            img.color = Color.white;
            img.enabled = true;
            return img;
        }

        /// <summary>Puts CameraDragInput on the same object as the follow camera.</summary>
        static int EnsureCameraDragInput(Transform root, StringBuilder sb, string where)
        {
            int added = 0;
            foreach (var cam in root.GetComponentsInChildren<SmoothFollowCamera>(true))
            {
                // Never write into a prefab asset from a scene sweep — that edits the file on
                // disk instead of the instance, which has bitten this project before.
                if (where == "scene" && EditorUtility.IsPersistent(cam)) continue;

                if (cam.GetComponent<CameraDragInput>() != null)
                {
                    sb.AppendLine($"[{where}] CameraDragInput already on {cam.name}");
                    continue;
                }

                cam.gameObject.AddComponent<CameraDragInput>();
                EditorUtility.SetDirty(cam.gameObject);
                sb.AppendLine($"[{where}] added CameraDragInput to {cam.name}");
                added++;
            }
            if (added == 0 && where == "prefab") sb.AppendLine("[prefab] no SmoothFollowCamera found");
            return added;
        }

        /// <summary>
        /// A Graphic with raycastTarget covering the play view eats every camera drag, because
        /// CameraDragInput refuses to start over UI. This finds those before they reach a device.
        /// </summary>
        static void AuditRaycastBlockers(UnityEngine.SceneManagement.Scene scene, StringBuilder sb)
        {
            int blockers = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (EditorUtility.IsPersistent(g)) continue;
                    if (!g.raycastTarget || !g.gameObject.activeInHierarchy) continue;

                    // Measure what the rect actually covers. An earlier version of this check
                    // tested "stretched to both anchors with no inset", which is only relative to
                    // the PARENT — a small panel's stretched child matched it and was reported as
                    // a full-screen blocker. Compare real corners against the canvas instead.
                    var canvas = g.canvas;
                    if (canvas == null) continue;

                    var canvasRt = canvas.transform as RectTransform;
                    if (canvasRt == null) continue;

                    Rect covered = RectInCanvasSpace(g.rectTransform, canvasRt);
                    Rect screen = canvasRt.rect;

                    // "Blocking" means it spans essentially the whole canvas in both axes.
                    const float Tolerance = 0.92f;
                    bool spansAll = covered.width >= screen.width * Tolerance
                                    && covered.height >= screen.height * Tolerance;
                    if (!spansAll) continue;

                    blockers++;
                    sb.AppendLine($"  WARNING screen-covering raycast target: {HierarchyPath(g.transform)}"
                                + $" ({covered.width:F0}x{covered.height:F0} of {screen.width:F0}x{screen.height:F0})");
                }
            }
            sb.AppendLine(blockers == 0
                ? "raycast audit: no screen-covering blockers (camera drag can reach the view)"
                : $"raycast audit: {blockers} blocker(s) would swallow every camera drag");
        }

        /// <summary>The rect's real extent expressed in the canvas's own space.</summary>
        static Rect RectInCanvasSpace(RectTransform rt, RectTransform canvasRt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Vector2 min = canvasRt.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = canvasRt.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return new Rect(min, max - min);
        }

        // Named HierarchyPath, not Path: a static method called Path would shadow System.IO.Path
        // in this file, which also uses File/Directory from that namespace.
        static string HierarchyPath(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
#endif
