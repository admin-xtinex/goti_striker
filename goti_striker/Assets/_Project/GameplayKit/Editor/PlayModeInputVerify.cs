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
    /// Enters Play mode and checks the input routing against the LIVE scene rather than the
    /// authored one. That distinction matters here: MenuManager destroys its authored children on
    /// startup and rebuilds the pages, so a static scan of the saved scene reports UI that never
    /// exists at runtime and misses UI that only exists at runtime.
    ///
    /// Survives the domain reload that entering Play mode triggers by parking its progress in
    /// SessionState and re-subscribing from InitializeOnLoadMethod.
    /// </summary>
    public static class PlayModeInputVerify
    {
        const string StageKey = "PitStriker.PlayModeInputVerify.stage";
        const string Result = "Library/PlayModeInputVerify.result";
        const string Scene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const int SettleFrames = 180;   // ~3s at 60fps: menus build, match auto-starts

        const int WatchdogTicks = 6000;   // generous: play mode normally settles in a few hundred

        static int _frames;
        static int _watchdog;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SessionState.SetString(StageKey, "playing");
            File.WriteAllText(Result, "STARTED (play mode not yet reached)\n");

            // Subscribe here as well as from Hook. Hook alone is not enough: it runs on domain
            // reload, and this project has Enter Play Mode Options with Reload Domain disabled,
            // so entering play mode reloads nothing, Hook never fires again, and the session
            // hangs in play mode forever waiting for a Tick that was never wired up.
            Subscribe();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (SessionState.GetString(StageKey, "") != "playing") return;
            Subscribe();
        }

        static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            // Watchdog: never sit in play mode indefinitely. A batch session with no -quit and a
            // check that never fires will otherwise hold the project lock until it is killed.
            if (++_watchdog > WatchdogTicks)
            {
                EditorApplication.update -= Tick;
                SessionState.SetString(StageKey, "done");
                File.WriteAllText(Result, $"FAIL watchdog: play mode never settled after {WatchdogTicks} editor ticks "
                                        + $"(isPlaying={EditorApplication.isPlaying})\n");
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(1);
                return;
            }

            if (!EditorApplication.isPlaying) return;   // still transitioning
            if (++_frames < SettleFrames) return;

            EditorApplication.update -= Tick;
            SessionState.SetString(StageKey, "done");

            string report;
            try { report = Verify(); }
            catch (System.Exception ex) { report = "FAIL exception\n" + ex; }

            File.WriteAllText(Result, report);
            Debug.Log("[PLAYVERIFY]\n" + report);

            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(report.Contains("FAIL") ? 1 : 0);
        }

        static string Verify()
        {
            var sb = new StringBuilder();
            int fails = 0;

            // --- wiring -----------------------------------------------------------------
            var binder = Object.FindAnyObjectByType<ShotControlBinder>();
            var drag = Object.FindAnyObjectByType<CameraDragInput>();
            var cam = SmoothFollowCamera.Instance;

            sb.AppendLine($"ShotControlBinder present      : {binder != null}");
            sb.AppendLine($"CameraDragInput present        : {drag != null}");
            sb.AppendLine($"SmoothFollowCamera.Instance    : {cam != null}");
            if (binder == null || drag == null || cam == null) { fails++; sb.AppendLine("FAIL missing component"); }

            if (binder != null)
            {
                sb.AppendLine($"  PowerArea wired              : {binder.PowerArea != null}");
                sb.AppendLine($"  PanelImage wired             : {binder.PanelImage != null}"
                            + (binder.PanelImage != null ? $" sprite={binder.PanelImage.sprite?.name}" : ""));
                sb.AppendLine($"  Thumb wired                  : {binder.Thumb != null}");
                if (binder.PowerArea == null || binder.PanelImage == null || binder.Thumb == null)
                { fails++; sb.AppendLine("FAIL shot UI not fully wired"); }
            }

            // --- enter gameplay before judging blockers ------------------------------------
            // The menu legitimately covers the screen with MenuBackdrop; that is not a bug, and
            // MenuManager already clears it for ScreenType.InGame. What matters is whether
            // anything still covers the view once a match is actually running, so switch first.
            var menu = Object.FindAnyObjectByType<PitStriker.UI.MenuManager>();
            if (menu != null)
            {
                menu.ShowScreen(PitStriker.UI.MenuManager.ScreenType.InGame);
                Canvas.ForceUpdateCanvases();
                sb.AppendLine("switched to ScreenType.InGame before auditing blockers");
            }
            else
            {
                sb.AppendLine("WARNING no MenuManager found; auditing whatever state play mode left");
            }

            // --- runtime raycast blockers -------------------------------------------------
            int blockers = 0;
            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!g.raycastTarget || !g.isActiveAndEnabled) continue;
                var canvas = g.canvas; if (canvas == null) continue;
                var crt = canvas.transform as RectTransform; if (crt == null) continue;

                var corners = new Vector3[4];
                g.rectTransform.GetWorldCorners(corners);
                Vector2 min = crt.InverseTransformPoint(corners[0]), max = min;
                for (int i = 1; i < 4; i++)
                {
                    Vector2 p = crt.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
                Vector2 size = max - min;
                if (size.x >= crt.rect.width * 0.92f && size.y >= crt.rect.height * 0.92f)
                {
                    blockers++;
                    sb.AppendLine($"  LIVE screen-covering raycast target: {g.name} (on {canvas.name})");
                }
            }
            sb.AppendLine($"runtime screen-covering blockers: {blockers}");
            if (blockers > 0) { fails++; sb.AppendLine("FAIL a live blocker would swallow every camera drag"); }

            // --- routing arbitration ------------------------------------------------------
            GestureRouter.ReleaseAll();

            bool camClaim = GestureRouter.TryClaim(GestureOwner.Camera, 7);
            bool shotStolen = GestureRouter.TryClaim(GestureOwner.Shot, 8);
            sb.AppendLine($"camera claims pointer 7        : {camClaim} (expect True)");
            sb.AppendLine($"shot cannot steal w/ pointer 8 : {!shotStolen} (expect True)");
            if (!camClaim || shotStolen) { fails++; sb.AppendLine("FAIL arbitration let two owners in"); }

            GestureRouter.Release(8);   // wrong pointer must not end the gesture
            bool survived = GestureRouter.Owns(GestureOwner.Camera, 7);
            sb.AppendLine($"foreign release ignored        : {survived} (expect True)");
            if (!survived) { fails++; sb.AppendLine("FAIL a second finger lifting cancelled the drag"); }

            GestureRouter.Release(7);
            sb.AppendLine($"router idle after release      : {GestureRouter.IsIdle} (expect True)");
            if (!GestureRouter.IsIdle) { fails++; sb.AppendLine("FAIL claim was stranded"); }

            // --- panel hit test sits on the right, clear of the lane ----------------------
            if (binder != null && binder.PowerArea != null)
            {
                var c = new Vector3[4];
                binder.PowerArea.GetWorldCorners(c);
                var sc = RectTransformUtility.WorldToScreenPoint(null, binder.PowerArea.position);
                bool onRight = sc.x > Screen.width * 0.5f;
                bool centreIsFree = !binder.IsScreenPosOverShotUI(new Vector2(Screen.width * 0.4f, Screen.height * 0.5f));
                sb.AppendLine($"panel centre on right half     : {onRight} (screen x={sc.x:F0}/{Screen.width})");
                sb.AppendLine($"lane centre NOT over shot UI   : {centreIsFree} (expect True)");
                if (!centreIsFree) { fails++; sb.AppendLine("FAIL panel covers the play view centre"); }
            }

            sb.Insert(0, fails == 0 ? "PASS\n\n" : $"FAIL ({fails} problem(s))\n\n");
            return sb.ToString();
        }
    }
}
#endif
