#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PitStriker.UI;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Temporarily hide menu/HUD canvases and aim Main Camera at the dirt lane for a clean Game-view shot.
    /// Does not save scene changes (visual prep only). Restore with RestoreForGameViewShot.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewLanePrep
    {
        const string AutoPrep = "Library/GameViewLanePrep.autorun";
        const string AutoRestore = "Library/GameViewLaneRestore.autorun";
        const string Result = "Library/GameViewLanePrep.result";
        static bool s_hidden;

        static GameViewLanePrep()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlaying) return;
            if (File.Exists(AutoPrep))
            {
                File.Delete(AutoPrep);
                Prep();
            }
            if (File.Exists(AutoRestore))
            {
                File.Delete(AutoRestore);
                Restore();
            }
        }

        [MenuItem("Pit Striker/Prep Game View Lane Shot")]
        public static void Prep()
        {
            // Hide overlay canvases (menus)
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.sortingOrder >= 100)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
            var menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
            if (menu != null) menu.gameObject.SetActive(false);
            var hud = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
            if (hud != null) hud.gameObject.SetActive(false);

            // Aim main camera at lane (behind marbles looking down pits) — approx Kerala preview angle
            var cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                // Disable follow script temporarily if present
                var follow = cam.GetComponent("SmoothFollowCamera") as Behaviour;
                if (follow != null) follow.enabled = false;

                // Look from near player end toward pit 3
                var pit1 = GameObject.Find("Pit_01_Round");
                var pit3 = GameObject.Find("Pit_03_Round");
                Vector3 lookTarget = pit1 != null ? pit1.transform.position : new Vector3(0, 0, 8);
                if (pit3 != null) lookTarget = Vector3.Lerp(pit1 != null ? pit1.transform.position : lookTarget, pit3.transform.position, 0.55f);
                lookTarget.y = 0.15f;

                Vector3 pos = lookTarget + new Vector3(0f, 1.35f, -6.5f);
                if (pit1 != null && pit3 != null)
                {
                    Vector3 along = (pit3.transform.position - pit1.transform.position).normalized;
                    pos = pit1.transform.position - along * 5.5f + Vector3.up * 1.4f;
                }
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation((lookTarget - pos).normalized, Vector3.up);
                cam.fieldOfView = 55f;
            }

            // Focus Game view
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                var win = EditorWindow.GetWindow(gameViewType);
                win.Focus();
                win.Repaint();
            }

            s_hidden = true;
            File.WriteAllText(Result, "PREPPED menu/hud hidden; camera aimed at lane. Snap now.");
            Debug.Log("<color=#00FFAA>[GameViewLanePrep]</color> Ready for screenshot — menu/HUD off, camera on dirt lane.");
        }

        [MenuItem("Pit Striker/Restore After Game View Shot")]
        public static void Restore()
        {
            var menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
            if (menu != null) menu.gameObject.SetActive(true);
            var hud = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
            if (hud != null) hud.gameObject.SetActive(true);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    canvas.gameObject.SetActive(true);
            }
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent("SmoothFollowCamera") as Behaviour;
                if (follow != null) follow.enabled = true;
            }
            File.WriteAllText(Result, "RESTORED menu/hud/camera follow.");
            Debug.Log("<color=#00FFAA>[GameViewLanePrep]</color> Restored.");
            s_hidden = false;
        }
    }
}
#endif
