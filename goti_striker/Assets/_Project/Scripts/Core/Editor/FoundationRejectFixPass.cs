#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Rejection fix: restore gameplay camera, warm soil, continuous fence rows (span along lane).
    /// Does not move pits, marble spawn, detection, or camera behaviour scripts.
    /// </summary>
    public static class FoundationRejectFixPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string FenceAsset = "Assets/_Project/Art/Models/Foundation/Prop_Fence_Module.fbx";
        const string FenceMatPath = "Assets/_Project/Art/Materials/M_Wood_Rustic.mat";
        const string LaneMatPath = "Assets/_Project/Art/Materials/M_GameplayLane_Soil.mat";
        const string SkirtMatPath = "Assets/_Project/Art/Materials/M_VisualGround_Skirt.mat";
        const string ResultFlag = "Library/FoundationRejectFixPass.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_gameplay_gameview.png";

        // Verified gameplay camera from pre-foundation backup
        static readonly Vector3 CamPos = new Vector3(0f, 1.2249999f, -9.2f);
        static readonly Quaternion CamRot = new Quaternion(0.08933931f, 0f, 0f, 0.99600124f);
        const float CamFov = 55f;

        // Lane width 20m â†’ fences at Â±10. Keep planted, continuous along +Z
        const float HalfWidth = 10.0f;
        const float Z0 = 0f;
        const float Z1 = 34f;
        const float ModuleSpan = 2.0f; // end-to-end, no gap

        [MenuItem("Pit Striker/Foundation Reject Fix")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[RejectFix]</color> " + r);
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
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RestoreGameplayCamera();
            WarmSoil();
            RebuildContinuousFences();

            // Capture from restored gameplay camera only â€” do not move after restore
            Capture(Camera.main);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            string pits = "pits missing";
            if (p1 && p2 && p3)
                pits = string.Format("pits z=({0},{1},{2})", p1.transform.localPosition.z, p2.transform.localPosition.z, p3.transform.localPosition.z);

            var cam = Camera.main;
            return string.Format(
                "Cam restored to gameplay ({0}). Warm soil. Continuous fences span-along-Z. Shot={1}. {2}.",
                cam != null ? cam.transform.position.ToString("F3") : "?", ShotPath, pits);
        }

        static void RestoreGameplayCamera()
        {
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("Main Camera missing");
            Undo.RecordObject(cam.transform, "Restore gameplay camera");
            cam.transform.position = CamPos;
            cam.transform.rotation = CamRot;
            cam.fieldOfView = CamFov;
            cam.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(cam.transform);
            EditorUtility.SetDirty(cam);
        }

        static void WarmSoil()
        {
            // Warmer Kerala laterite: more yellow-brown, less dark maroon
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Normal.png");
            var laneMat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);
            var skirtMat = AssetDatabase.LoadAssetAtPath<Material>(SkirtMatPath);
            if (laneMat == null) throw new System.Exception("lane mat missing");

            // Tint warm sandy-brown (not dark red)
            var warm = new Color(0.82f, 0.58f, 0.34f, 1f);
            var skirtWarm = new Color(0.70f, 0.50f, 0.30f, 1f);
            var tiling = new Vector2(10f, 17f); // reduce stretch/repetition on 20x34

            ApplySoil(laneMat, albedo, normal, warm, tiling, smoothness: 0.22f, bump: 0.4f);
            if (skirtMat != null)
                ApplySoil(skirtMat, albedo, normal, skirtWarm, new Vector2(22f, 22f), smoothness: 0.18f, bump: 0.3f);

            var lane = GameObject.Find("GameplayLane");
            if (lane != null)
            {
                var mr = lane.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = laneMat;
            }

            // Soften light so soil reads warmer (don't touch camera)
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                var light = lightGo.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1f, 0.95f, 0.85f);
                    light.intensity = 1.55f;
                }
            }
        }

        static void ApplySoil(Material mat, Texture2D albedo, Texture2D normal, Color tint, Vector2 tiling, float smoothness, float bump)
        {
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", bump);
            }
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_BumpMap", tiling);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        static void RebuildContinuousFences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (prefab == null) throw new System.Exception("Missing fence " + FenceAsset);
            var fenceMat = AssetDatabase.LoadAssetAtPath<Material>(FenceMatPath);
            // Prefer brighter bamboo-ish tint if wood rustic is too dark
            if (fenceMat != null)
            {
                fenceMat.SetColor("_BaseColor", new Color(0.70f, 0.55f, 0.32f, 1f));
                fenceMat.SetFloat("_Smoothness", 0.28f);
                EditorUtility.SetDirty(fenceMat);
            }

            var foundation = GameObject.Find("Foundation_Gameplay");
            if (foundation == null) throw new System.Exception("Foundation_Gameplay missing");

            var old = GameObject.Find("Foundation_Fences");
            if (old != null) Object.DestroyImmediate(old);

            var fenceRoot = new GameObject("Foundation_Fences");
            fenceRoot.transform.SetParent(foundation.transform, false);

            // Module built along Blender/Unity X â€” rotate 90Â° so span runs along lane +Z
            int n = Mathf.RoundToInt((Z1 - Z0) / ModuleSpan);
            for (int i = 0; i < n; i++)
            {
                float z = Z0 + ModuleSpan * 0.5f + i * ModuleSpan;
                // Left: span along +Z, posts outside / on the line
                Place(prefab, fenceRoot.transform, "Fence_L_" + i, new Vector3(-HalfWidth, 0f, z), 90f, fenceMat);
                // Right: face inward
                Place(prefab, fenceRoot.transform, "Fence_R_" + i, new Vector3(HalfWidth, 0f, z), -90f, fenceMat);
            }
        }

        static void Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw, Material mat)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            if (mat != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        static void Capture(Camera cam)
        {
            if (cam == null) throw new System.Exception("No Main Camera");
            Directory.CreateDirectory(Path.GetDirectoryName(ShotPath));
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
        }
    }
}
#endif