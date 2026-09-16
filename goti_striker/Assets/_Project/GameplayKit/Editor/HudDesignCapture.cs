#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitStriker.Gameplay;
using PitStriker.UI;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Captures the in-game view, UI included, for design review. Run with the editor window
    /// (not -batchmode): a Game view is needed for ScreenCapture to include overlay canvases.
    ///
    ///   Unity.exe -projectPath ... -executeMethod PitStriker.GameplayKit.EditorTools.HudDesignCapture.Run
    ///   env GOTI_CAPTURE_DIR = output folder, GOTI_CAPTURE_TAG = file name prefix
    ///
    /// Starts a local 4-player match (1 human, 3 bots) and saves a frame from the toss.
    /// Visual inspection only: nothing it touches is saved to the project.
    /// </summary>
    public static class HudDesignCapture
    {
        const string StageKey = "PitStriker.HudDesignCapture.stage";
        const string Scene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const int Width = 1672, Height = 941;

        static IEnumerator<float> _script;
        static double _resumeAt;
        static string _pending;          // capture requested; file written at the end of a frame
        static int _ticks;

        public static void Run()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SetGameViewSize(Width, Height);
            SessionState.SetString(StageKey, "playing");
            Subscribe();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (SessionState.GetString(StageKey, "") == "playing") Subscribe();
        }

        static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static string OutDir => Environment.GetEnvironmentVariable("GOTI_CAPTURE_DIR") ?? "Library/HudCapture";
        static string Tag => Environment.GetEnvironmentVariable("GOTI_CAPTURE_TAG") ?? "capture";

        static void Tick()
        {
            if (++_ticks > 400000) { Finish("FAIL watchdog"); return; }
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < _resumeAt) return;
            try
            {
                if (_script == null) _script = Script().GetEnumerator();
                if (_script.MoveNext()) _resumeAt = EditorApplication.timeSinceStartup + _script.Current;
                else Finish("DONE");
            }
            catch (Exception ex) { Finish("FAIL " + ex); }
        }

        static IEnumerable<float> Script()
        {
            Directory.CreateDirectory(OutDir);
            FocusGameView();
            yield return 4f;

            var menu = UnityEngine.Object.FindAnyObjectByType<MenuManager>();
            if (menu == null) throw new Exception("no MenuManager");
            var t = typeof(MenuManager);
            t.GetField("_selectedPlayerCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, 4);
            t.GetField("_isAISlot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, new[] { false, true, true, true });
            t.GetMethod("HandleStartMatchClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
            yield return 3f;

            foreach (var w in Capture("toss")) yield return w;

            // Normal turn frame. Capture-only shortcut past the toss: puts player 1 on turn as the
            // end of the toss would. Never runs in the game.
            var tm = TurnManager.Instance;
            var tmt = typeof(TurnManager);
            StopBots();
            tm.CurrentPlayerIndex = 0;
            tmt.GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tm, new object[] { TurnManager.GameState.ReadyToAim });
            PitStriker.Input.SwipeLaunchController.Instance?.SetAimMode(PitStriker.Input.SwipeLaunchController.AimMode.PrecisionPullBack);
            tmt.GetMethod("ActivateCurrentPlayer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tm, null);
            yield return 2.5f;
            foreach (var w in Capture("play")) yield return w;
            yield return 0.5f;
        }

        static void StopBots()
        {
            var ai = PitStriker.AI.AIMarbleController.Instance;
            if (ai != null) ai.CancelAITurn();
        }

        /// <summary>
        /// Renders the main camera, with every overlay canvas temporarily drawn by that camera,
        /// into a texture. Independent of the Game view, which does not refresh while the editor
        /// window is in the background.
        /// </summary>
        static IEnumerable<float> Capture(string name)
        {
            var cam = Camera.main;
            if (cam == null) throw new Exception("no main camera");
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;

            // The scene goes through the main camera (with its post-processing, as in the game);
            // the UI is rendered separately onto transparent black and blended on the CPU, because
            // overlay UI is never post-processed. The UI camera sits far below the world, looking
            // down, so it sees nothing but the canvases assigned to it.
            var uiCamGo = new GameObject("HudCaptureUICamera");
            var uiCam = uiCamGo.AddComponent<Camera>();
            uiCam.clearFlags = CameraClearFlags.SolidColor;
            uiCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            uiCam.orthographic = true;
            uiCam.nearClipPlane = 0.01f; uiCam.farClipPlane = 5f;
            uiCam.targetTexture = uiRt;
            uiCam.enabled = false;
            uiCamGo.transform.SetPositionAndRotation(new Vector3(0f, -5000f, 0f), Quaternion.Euler(90f, 0f, 0f));

            var changed = new List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = uiCam;
                c.planeDistance = 1f + c.sortingOrder * 0.0001f * -1f + 2f;
                changed.Add(c);
            }
            yield return 0.4f;   // let scalers and layout settle at the texture's size

            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            uiCam.Render();
            var prev = RenderTexture.active;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var ui = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = uiRt;
            ui.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = prev;

            // UI shaders write premultiplied-looking colour with straight alpha; blend "over".
            var scene = tex.GetPixels32();
            var over = ui.GetPixels32();
            for (int i = 0; i < scene.Length; i++)
            {
                int a = over[i].a;
                if (a == 0) continue;
                scene[i].r = (byte)((over[i].r * 255 + scene[i].r * (255 - a)) / 255 > 255 ? 255 : (over[i].r * 255 + scene[i].r * (255 - a)) / 255);
                scene[i].g = (byte)((over[i].g * 255 + scene[i].g * (255 - a)) / 255 > 255 ? 255 : (over[i].g * 255 + scene[i].g * (255 - a)) / 255);
                scene[i].b = (byte)((over[i].b * 255 + scene[i].b * (255 - a)) / 255 > 255 ? 255 : (over[i].b * 255 + scene[i].b * (255 - a)) / 255);
                scene[i].a = 255;
            }
            tex.SetPixels32(scene);
            tex.Apply();
            UnityEngine.Object.DestroyImmediate(ui);
            uiRt.Release();

            foreach (var c in changed) if (c != null) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
            cam.targetTexture = oldTarget;
            UnityEngine.Object.DestroyImmediate(uiCamGo);

            string path = Path.GetFullPath(Path.Combine(OutDir, $"{Tag}_{name}.png"));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            File.AppendAllText(Path.Combine(OutDir, $"{Tag}.log"),
                $"{name}: {path} state={TurnManager.Instance?.CurrentState} canvases={changed.Count}" + System.Environment.NewLine);
        }

        static void Finish(string status)
        {
            EditorApplication.update -= Tick;
            SessionState.SetString(StageKey, "done");
            Debug.Log($"[HUDCAPTURE] {status} (out: {OutDir})");
            try { File.AppendAllText(Path.Combine(OutDir, $"{Tag}.log"), status + "\n"); }
            catch (Exception ex) { Debug.LogWarning("[HUDCAPTURE] could not write log: " + ex.Message); }
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ Game view sizing

        static void FocusGameView()
        {
            var gv = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gv).Focus();
        }

        static void SetGameViewSize(int w, int h)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = single.GetProperty("instance").GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });

            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
            var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
            var size = ctor.Invoke(new[] { Enum.Parse(sizeKind, "FixedResolution"), w, h, "GotiCapture" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;

            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            var prop = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) prop.SetValue(gv, index);
            else gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?.Invoke(gv, new object[] { index, null });
            gv.Repaint();
        }
    }
}
#endif
