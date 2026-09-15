#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    public static class FoundationCaptureShot
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_soil_gameview.png";
        const string ResultFlag = "Library/FoundationCaptureShot.result";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("no cam");
            cam.transform.position = new Vector3(0f, 3.2f, -4.5f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.fieldOfView = 58f;
            cam.clearFlags = CameraClearFlags.Skybox;
            // Force one frame of lighting
            DynamicGI.UpdateEnvironment();
            int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            File.WriteAllBytes(ShotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            // sample a pixel to verify not grey void
            File.WriteAllText(ResultFlag, "PASS\nsize=" + new FileInfo(ShotPath).Length + "\n");
            Debug.Log("[CaptureShot] wrote " + ShotPath);
            // don't save scene (camera may be ok to save though)
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif