#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Foundation approval build: hide décor, 20x34 GameplayLane + two straight bamboo fences.
    /// Does not move pits / kit physics / marbles. No permanent deletes.
    /// </summary>
    [InitializeOnLoad]
    public static class FoundationGameplayPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string AutoFlag = "Library/FoundationGameplayPass.autorun";
        const string ResultFlag = "Library/FoundationGameplayPass.result";
        const string RootName = "Foundation_Gameplay";
        const string LaneName = "GameplayLane";
        const string FenceRootName = "Foundation_Fences";
        const string FenceAsset = "Assets/_Project/Art/Models/Phase2_P0/Prop_BambooFence_Module.fbx";
        const string SoilMatPath = "Assets/_Project/Art/Materials/M_Ground_Sand.mat";
        const string FenceMatPath = "Assets/_Project/Art/Materials/M_Wood_Rustic.mat";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_gameview.png";

        static readonly string[] HideExact =
        {
            "Phase2_P0_Dressing",
            "Phase2_P1_Dressing",
            "Village_Reference_Upgrade",
            "Environment_Village_Dressing",
            "Environment_Blender_Village_Graphics",
            "Distant_Ridge",
            "Surrounding_Earth",
            "Horizon_Ground_Skirt",
            "Rounded_Perimeter_Berm",
            "Ground_Center_Fairway",
            "Village_Reflection",
            "HUD_Canvas",
            "Canvas_GameScreens",
        };

        static FoundationGameplayPass()
        {
            EditorApplication.update += TryAutorun;
        }

        [MenuItem("Pit Striker/Foundation Gameplay Pass")]
        public static void RunMenu() { Run(); }

        static void TryAutorun()
        {
            if (!File.Exists(AutoFlag) || EditorApplication.isCompiling || EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < 4f)
                return;
            File.Delete(AutoFlag);
            Run();
        }

        public static void Run()
        {
            try
            {
                string result = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + result);
                Debug.Log("<color=#00FF88>[Foundation]</color> " + result);
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

            int hidden = HideDecor();
            RebuildFoundation();
            TuneLight();
            FrameCamera();
            CaptureShot();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Verify pits untouched
            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            string pitInfo = "pits missing";
            if (p1 && p2 && p3)
            {
                float d12 = Mathf.Abs(p2.transform.position.z - p1.transform.position.z);
                float d23 = Mathf.Abs(p3.transform.position.z - p2.transform.position.z);
                pitInfo = string.Format("pits z=({0:0.##},{1:0.##},{2:0.##}) gaps=({3:0.##},{4:0.##}) [Hacky to confirm 12m]",
                    p1.transform.position.z, p2.transform.position.z, p3.transform.position.z, d12, d23);
            }

            var lane = GameObject.Find(LaneName);
            var fences = GameObject.Find(FenceRootName);
            int fenceCount = fences ? fences.transform.childCount : 0;
            return string.Format(
                "Hidden {0} décor roots/objects. Built {1} + {2} ({3} modules). Shot -> {4}. {5}. Kit/pits not moved.",
                hidden, LaneName, FenceRootName, fenceCount, ShotPath, pitInfo);
        }

        static int HideDecor()
        {
            int n = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                string name = t.name;

                if (HideExact.Contains(name))
                {
                    if (t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        n++;
                    }
                    continue;
                }

                // Hide Village_Reference / meadow / broadleaf / palm clutter parents already covered by Village_Reference_Upgrade

                // Under Arena_Sandbox: hide non-gameplay visual helpers (keep pits + marbles)
                if (t.parent != null && t.parent.name == "Arena_Sandbox")
                {
                    if (IsGameplayKeep(name)) continue;
                    if (t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        n++;
                    }
                }
            }
            return n;
        }

        static bool IsGameplayKeep(string name)
        {
            if (name.StartsWith("Pit_")) return true;
            if (name.IndexOf("Marble", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.StartsWith("P1") || name.StartsWith("P2") || name.StartsWith("P3") || name.StartsWith("P4")) return true;
            if (name.IndexOf("Bead", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name == "Gameplay_Kit_Village") return true; // keep kit root; visuals may already be muted
            return false;
        }

        static void RebuildFoundation()
        {
            var existing = GameObject.Find(RootName);
            if (existing) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
            var fenceMat = AssetDatabase.LoadAssetAtPath<Material>(FenceMatPath);
            var fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (soil == null) throw new System.Exception("Missing soil mat " + SoilMatPath);
            if (fencePrefab == null) throw new System.Exception("Missing fence " + FenceAsset);

            // Lane: Unity plane is 10x10 → scale (2,1,3.4) = 20 x 34. Center at z=17 to cover pits 3..31
            var lane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lane.name = LaneName;
            lane.transform.SetParent(root.transform, false);
            lane.transform.localPosition = new Vector3(0f, 0.01f, 17f);
            lane.transform.localRotation = Quaternion.identity;
            lane.transform.localScale = new Vector3(2f, 1f, 3.4f);
            var laneR = lane.GetComponent<MeshRenderer>();
            laneR.sharedMaterial = soil;
            // Keep MeshCollider for marble support (replaces hidden fairway ground)

            // Two straight fence rows at x=±10 (inner edge of 20m width), spanning z=0..34
            var fenceRoot = new GameObject(FenceRootName);
            fenceRoot.transform.SetParent(root.transform, false);

            const float halfW = 10f;
            const float z0 = 0f;
            const float z1 = 34f;
            const float moduleLen = 2f;
            int modules = Mathf.RoundToInt((z1 - z0) / moduleLen); // 17

            for (int i = 0; i < modules; i++)
            {
                float z = z0 + moduleLen * 0.5f + i * moduleLen;
                PlaceFence(fencePrefab, fenceRoot.transform, "Fence_L_" + i, new Vector3(-halfW, 0f, z), 0f, fenceMat);
                PlaceFence(fencePrefab, fenceRoot.transform, "Fence_R_" + i, new Vector3(halfW, 0f, z), 180f, fenceMat);
            }
        }

        static void PlaceFence(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw, Material mat)
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

        static void TuneLight()
        {
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo == null) return;
            var light = lightGo.GetComponent<Light>();
            if (light == null) return;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
        }

        static void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Low angle looking down lane (+Z), foundation-only framing
            cam.transform.position = new Vector3(0f, 2.4f, -6.5f);
            cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            cam.fieldOfView = 55f;
        }

        static void CaptureShot()
        {
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("Main Camera missing for shot");
            Directory.CreateDirectory(Path.GetDirectoryName(ShotPath));
            int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            File.WriteAllBytes(ShotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif