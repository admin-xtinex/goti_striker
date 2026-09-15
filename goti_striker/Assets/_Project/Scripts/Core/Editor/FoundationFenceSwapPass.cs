#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Swap Foundation_Fences to approved Prop_Fence_Module. Does not touch pits/kit/lane collider.
    /// </summary>
    public static class FoundationFenceSwapPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string FenceAsset = "Assets/_Project/Art/Models/Foundation/Prop_Fence_Module.fbx";
        const string FenceMatPath = "Assets/_Project/Art/Materials/M_Wood_Rustic.mat";
        const string ResultFlag = "Library/FoundationFenceSwapPass.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_fence_gameview.png";

        // Keep full 20m usable width: place module centres at x=±10.05 (posts Ø0.08 sit outside inner edge)
        const float HalfWidth = 10.05f;
        const float Z0 = 0f;
        const float Z1 = 34f;
        const float ModuleSpan = 2.08f;

        [MenuItem("Pit Striker/Foundation Fence Swap")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[FenceSwap]</color> " + r);
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

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (prefab == null) throw new System.Exception("Missing " + FenceAsset);
            var fenceMat = AssetDatabase.LoadAssetAtPath<Material>(FenceMatPath);

            var foundation = GameObject.Find("Foundation_Gameplay");
            if (foundation == null) throw new System.Exception("Foundation_Gameplay missing");

            var old = GameObject.Find("Foundation_Fences");
            if (old != null) Object.DestroyImmediate(old);

            var fenceRoot = new GameObject("Foundation_Fences");
            fenceRoot.transform.SetParent(foundation.transform, false);

            int modules = Mathf.RoundToInt((Z1 - Z0) / ModuleSpan);
            int count = 0;
            for (int i = 0; i < modules; i++)
            {
                float z = Z0 + ModuleSpan * 0.5f + i * ModuleSpan;
                count += Place(prefab, fenceRoot.transform, "Fence_L_" + i, new Vector3(-HalfWidth, 0f, z), 0f, fenceMat);
                count += Place(prefab, fenceRoot.transform, "Fence_R_" + i, new Vector3(HalfWidth, 0f, z), 180f, fenceMat);
            }

            // Camera: elevated enough to see pits 1..3 and a marble
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 8.5f, -8.0f);
                cam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
                cam.fieldOfView = 55f;
                cam.clearFlags = CameraClearFlags.Skybox;
            }

            Capture(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            string pits = "pits missing";
            if (p1 && p2 && p3)
                pits = string.Format("pits z=({0},{1},{2}) x=({3},{4},{5})",
                    p1.transform.localPosition.z, p2.transform.localPosition.z, p3.transform.localPosition.z,
                    p1.transform.localPosition.x, p2.transform.localPosition.x, p3.transform.localPosition.x);

            return string.Format("Placed {0} Prop_Fence_Module under Foundation_Fences (span {1}, x=±{2}). Shot={3}. {4}. Lane/kit untouched.",
                count, ModuleSpan, HalfWidth, ShotPath, pits);
        }

        static int Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw, Material mat)
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
            return 1;
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