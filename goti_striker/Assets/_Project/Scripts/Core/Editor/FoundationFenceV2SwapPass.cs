#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    public static class FoundationFenceV2SwapPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string FenceAsset = "Assets/_Project/Art/Models/Foundation/Prop_Fence_Module.fbx";
        const string LaneMatPath = "Assets/_Project/Art/Materials/M_GameplayLane_Soil.mat";
        const string SkirtMatPath = "Assets/_Project/Art/Materials/M_VisualGround_Skirt.mat";
        const string ResultFlag = "Library/FoundationFenceV2SwapPass.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_gameplay_gameview.png";

        static readonly Vector3 CamPos = new Vector3(0f, 1.2249999f, -9.2f);
        static readonly Quaternion CamRot = new Quaternion(0.08933931f, 0f, 0f, 0.99600124f);
        const float CamFov = 55f;
        const float HalfWidth = 10.0f;
        const float Z0 = 0f;
        const float Z1 = 34f;
        const float ModuleSpan = 2.0f;

        [MenuItem("Pit Striker/Foundation Fence V2 Swap")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[FenceV2]</color> " + r);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                Debug.LogException(e);
                throw;
            }
        }

        static string Apply()
        {
            AssetDatabase.ImportAsset(FenceAsset, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Restore gameplay camera — do not change behaviour scripts
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("Main Camera missing");
            cam.transform.position = CamPos;
            cam.transform.rotation = CamRot;
            cam.fieldOfView = CamFov;
            cam.clearFlags = CameraClearFlags.Skybox;

            // Warm soil tint (keep existing textures)
            var warm = new Color(0.82f, 0.58f, 0.34f, 1f);
            var laneMat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);
            if (laneMat != null)
            {
                laneMat.SetColor("_BaseColor", warm);
                laneMat.SetFloat("_Smoothness", 0.22f);
                laneMat.SetTextureScale("_BaseMap", new Vector2(10f, 17f));
                laneMat.SetTextureScale("_BumpMap", new Vector2(10f, 17f));
                EditorUtility.SetDirty(laneMat);
            }
            var skirtMat = AssetDatabase.LoadAssetAtPath<Material>(SkirtMatPath);
            if (skirtMat != null)
            {
                skirtMat.SetColor("_BaseColor", new Color(0.70f, 0.50f, 0.30f, 1f));
                EditorUtility.SetDirty(skirtMat);
            }
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                var light = lightGo.GetComponent<Light>();
                if (light != null) { light.color = new Color(1f, 0.95f, 0.85f); light.intensity = 1.55f; }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (prefab == null) throw new System.Exception("Missing " + FenceAsset);

            var foundation = GameObject.Find("Foundation_Gameplay");
            if (foundation == null) throw new System.Exception("Foundation_Gameplay missing");
            var old = GameObject.Find("Foundation_Fences");
            if (old != null) Object.DestroyImmediate(old);

            var fenceRoot = new GameObject("Foundation_Fences");
            fenceRoot.transform.SetParent(foundation.transform, false);

            int n = Mathf.RoundToInt((Z1 - Z0) / ModuleSpan);
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                float z = Z0 + ModuleSpan * 0.5f + i * ModuleSpan;
                // Module span along local X → yaw 90 so continuous along lane +Z
                count += Place(prefab, fenceRoot.transform, "Fence_L_" + i, new Vector3(-HalfWidth, 0f, z), 90f);
                count += Place(prefab, fenceRoot.transform, "Fence_R_" + i, new Vector3(HalfWidth, 0f, z), -90f);
            }

            Capture(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            string pits = (p1 && p2 && p3)
                ? string.Format("pits z=({0},{1},{2})", p1.transform.localPosition.z, p2.transform.localPosition.z, p3.transform.localPosition.z)
                : "pits missing";

            return string.Format("V2 fence x{0} end-to-end (span {1}). Cam gameplay. Warm soil. Shot={2}. {3}.",
                count, ModuleSpan, ShotPath, pits);
        }

        static int Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            // keep Hacky FBX materials (M_Bamboo_Fence / M_Fence_Rope)
            return 1;
        }

        static void Capture(Camera cam)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShotPath));
            int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(ShotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
#endif