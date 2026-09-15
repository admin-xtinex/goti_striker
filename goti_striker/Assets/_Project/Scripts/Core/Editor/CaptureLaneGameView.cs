#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitStriker.UI;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Capture a real village-lane PNG via Camera.Render (edit mode) — not a desktop grab.
    /// </summary>
    [InitializeOnLoad]
    public static class CaptureLaneGameView
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string OutPath = @"C:\Users\Tisan\Documents\PitStriker-Working\Preview\p0_lane_gameview.png";
        const string AutoFlag = "Library/CaptureLaneGameView.autorun";
        const string ResultFlag = "Library/CaptureLaneGameView.result";

        static CaptureLaneGameView()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!File.Exists(AutoFlag) || EditorApplication.isCompiling || EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < 5f)
                return;
            File.Delete(AutoFlag);
            Run();
        }

        [MenuItem("Pit Striker/Capture Lane Game View PNG")]
        public static void Run()
        {
            try
            {
                // Ensure village scene
                var active = EditorSceneManager.GetActiveScene();
                if (!active.path.Replace('\\', '/').EndsWith("SC_Village_Graphics_Test.unity"))
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                // Hide menu / HUD overlays
                var menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
                if (menu != null) menu.gameObject.SetActive(false);
                var hud = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
                if (hud != null) hud.gameObject.SetActive(false);
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.sortingOrder >= 100)
                        canvas.gameObject.SetActive(false);
                }

                var cam = Camera.main;
                if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    throw new System.Exception("No camera found");

                var follow = cam.GetComponent("SmoothFollowCamera") as Behaviour;
                bool followWas = follow != null && follow.enabled;
                if (follow != null) follow.enabled = false;

                var pit1 = GameObject.Find("Pit_01_Round");
                var pit3 = GameObject.Find("Pit_03_Round");
                Vector3 lookTarget = new Vector3(0f, 0.15f, 6f);
                Vector3 pos = new Vector3(0f, 1.4f, -1f);
                if (pit1 != null && pit3 != null)
                {
                    Vector3 along = (pit3.transform.position - pit1.transform.position);
                    if (along.sqrMagnitude < 0.01f) along = Vector3.forward;
                    along.Normalize();
                    lookTarget = Vector3.Lerp(pit1.transform.position, pit3.transform.position, 0.45f);
                    lookTarget.y = 0.2f;
                    pos = pit1.transform.position - along * 5.8f + Vector3.up * 1.45f;
                }
                else if (pit1 != null)
                {
                    lookTarget = pit1.transform.position + Vector3.forward * 4f;
                    lookTarget.y = 0.2f;
                    pos = pit1.transform.position - Vector3.forward * 5.5f + Vector3.up * 1.4f;
                }

                var oldPos = cam.transform.position;
                var oldRot = cam.transform.rotation;
                var oldFov = cam.fieldOfView;
                var oldTarget = cam.targetTexture;

                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation((lookTarget - pos).normalized, Vector3.up);
                cam.fieldOfView = 55f;

                int w = 1600, h = 900;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = oldTarget;

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                File.WriteAllBytes(OutPath, tex.EncodeToPNG());

                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);

                // Leave camera framed for live Game tab; keep UI hidden until restore
                // (do not restore follow yet — Crea may still look)

                long bytes = new FileInfo(OutPath).Length;
                string msg = $"CAPTURED {OutPath} bytes={bytes} camPos={pos} look={lookTarget} followWas={followWas}";
                File.WriteAllText(ResultFlag, msg);
                Debug.Log("<color=#00FF88>[CaptureLane]</color> " + msg);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                Debug.LogException(e);
            }
        }
    }
}
#endif
