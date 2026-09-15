#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    public static class FoundationPolishPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string FenceAsset = "Assets/_Project/Art/Models/Foundation/Prop_Fence_Module.fbx";
        const string LaneMatPath = "Assets/_Project/Art/Materials/M_GameplayLane_Soil.mat";
        const string SkirtMatPath = "Assets/_Project/Art/Materials/M_VisualGround_Skirt.mat";
        const string MeshPath = "Assets/_Project/Art/Meshes/GameplayLane_WithPitHoles.asset";
        const string ResultFlag = "Library/FoundationPolishPass.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_gameplay_gameview.png";

        static readonly Vector3 CamPos = new Vector3(0f, 1.2249999f, -9.2f);
        static readonly Quaternion CamRot = new Quaternion(0.08933931f, 0f, 0f, 0.99600124f);
        const float CamFov = 55f;
        const float HalfWidth = 10f;
        const float ModuleSpan = 2.0f; // centre-to-centre; module ~2.08 → slight overlap, no gaps
        static readonly Vector3[] Pits = { new Vector3(0,0,3), new Vector3(0,0,16.5f), new Vector3(0,0,31) };
        const float HoleR = 1.05f;

        [MenuItem("Pit Striker/Foundation Polish Pass")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[Polish]</color> " + r);
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
            AssetDatabase.ImportAsset("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RestoreCam();
            WarmSoil();
            RebuildLaneMeshClean();
            RebuildFencesContinuous();
            EnsurePitsAndMarblesVisible();

            Capture(Camera.main);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            return string.Format("Polish done. Cam gameplay. Fences L/R continuous. Holes r={0}. pits z=({1},{2},{3}). Shot={4}",
                HoleR,
                p1 ? p1.transform.localPosition.z : -1,
                p2 ? p2.transform.localPosition.z : -1,
                p3 ? p3.transform.localPosition.z : -1,
                ShotPath);
        }

        static void RestoreCam()
        {
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("no cam");
            cam.transform.position = CamPos;
            cam.transform.rotation = CamRot;
            cam.fieldOfView = CamFov;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        static void WarmSoil()
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Normal.png");
            var laneMat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);
            var skirtMat = AssetDatabase.LoadAssetAtPath<Material>(SkirtMatPath);
            // less tiling = fewer repeats
            var tile = new Vector2(3.2f, 5.4f);
            var warm = new Color(0.88f, 0.64f, 0.38f, 1f);
            if (laneMat != null)
            {
                if (albedo) laneMat.SetTexture("_BaseMap", albedo);
                if (normal) { laneMat.SetTexture("_BumpMap", normal); laneMat.EnableKeyword("_NORMALMAP"); laneMat.SetFloat("_BumpScale", 0.35f); }
                laneMat.SetColor("_BaseColor", warm);
                laneMat.SetFloat("_Smoothness", 0.25f);
                laneMat.SetFloat("_Metallic", 0f);
                laneMat.SetTextureScale("_BaseMap", tile);
                laneMat.SetTextureScale("_BumpMap", tile);
                EditorUtility.SetDirty(laneMat);
            }
            if (skirtMat != null)
            {
                if (albedo) skirtMat.SetTexture("_BaseMap", albedo);
                skirtMat.SetColor("_BaseColor", new Color(0.75f, 0.55f, 0.32f, 1f));
                skirtMat.SetTextureScale("_BaseMap", new Vector2(8f, 8f));
                EditorUtility.SetDirty(skirtMat);
            }
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo)
            {
                var L = lightGo.GetComponent<Light>();
                if (L) { L.color = new Color(1f, 0.96f, 0.88f); L.intensity = 1.7f; }
            }
            AssetDatabase.SaveAssets();
        }

        static void RebuildLaneMeshClean()
        {
            var lane = GameObject.Find("GameplayLane");
            if (lane == null) throw new System.Exception("GameplayLane missing");
            var mf = lane.GetComponent<MeshFilter>();
            var mc = lane.GetComponent<MeshCollider>();

            // Full collider plane (physics unchanged)
            var full = BuildPlane(48, 80, null, 0f);
            full.name = "GameplayLane_FullCollider";
            // Visual with clean holes + continuous UVs (no centre UV break)
            var visual = BuildPlane(64, 108, Pits, HoleR);
            visual.name = "GameplayLane_WithPitHoles";

            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
                AssetDatabase.DeleteAsset(MeshPath);
            Directory.CreateDirectory("Assets/_Project/Art/Meshes");
            AssetDatabase.CreateAsset(visual, MeshPath);
            AssetDatabase.AddObjectToAsset(full, MeshPath);
            AssetDatabase.SaveAssets();

            mf.sharedMesh = visual;
            if (mc != null) mc.sharedMesh = full;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);
            var mr = lane.GetComponent<MeshRenderer>();
            if (mr && mat) mr.sharedMaterial = mat;
        }

        // Local plane [-5,5]^2 under GameplayLane scale (2,1,3.4) → world 20x34 at z=17
        static Mesh BuildPlane(int segsX, int segsZ, Vector3[] worldHoles, float holeR)
        {
            float min = -5f, max = 5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var grid = new int[segsX + 1, segsZ + 1];

            Vector2[] holes = null;
            float rX = 0, rZ = 0;
            if (worldHoles != null)
            {
                holes = new Vector2[worldHoles.Length];
                for (int i = 0; i < worldHoles.Length; i++)
                    holes[i] = new Vector2(worldHoles[i].x / 2f, (worldHoles[i].z - 17f) / 3.4f);
                rX = holeR / 2f;
                rZ = holeR / 3.4f;
            }

            for (int z = 0; z <= segsZ; z++)
            {
                for (int x = 0; x <= segsX; x++)
                {
                    float lx = Mathf.Lerp(min, max, x / (float)segsX);
                    float lz = Mathf.Lerp(min, max, z / (float)segsZ);
                    bool inHole = false;
                    if (holes != null)
                    {
                        for (int h = 0; h < holes.Length; h++)
                        {
                            float dx = (lx - holes[h].x) / rX;
                            float dz = (lz - holes[h].y) / rZ;
                            if (dx * dx + dz * dz <= 1f) { inHole = true; break; }
                        }
                    }
                    if (inHole) { grid[x, z] = -1; continue; }
                    grid[x, z] = verts.Count;
                    verts.Add(new Vector3(lx, 0f, lz));
                    // Continuous UVs from local pos — avoids centre seam from index-based UV
                    uvs.Add(new Vector2((lx - min) / (max - min), (lz - min) / (max - min)));
                }
            }

            for (int z = 0; z < segsZ; z++)
            {
                for (int x = 0; x < segsX; x++)
                {
                    int a = grid[x, z], b = grid[x + 1, z], c = grid[x, z + 1], d = grid[x + 1, z + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void RebuildFencesContinuous()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (prefab == null) throw new System.Exception("fence missing");
            var foundation = GameObject.Find("Foundation_Gameplay");
            if (foundation == null) throw new System.Exception("Foundation_Gameplay missing");
            var old = GameObject.Find("Foundation_Fences");
            if (old) Object.DestroyImmediate(old);

            var root = new GameObject("Foundation_Fences");
            root.transform.SetParent(foundation.transform, false);

            // Start at z=0 post line through z=34; centres every 2m
            int n = Mathf.RoundToInt((34f - 0f) / ModuleSpan);
            for (int i = 0; i < n; i++)
            {
                float z = ModuleSpan * 0.5f + i * ModuleSpan;
                Place(prefab, root.transform, "Fence_L_" + i, new Vector3(-HalfWidth, 0f, z), 90f);
                Place(prefab, root.transform, "Fence_R_" + i, new Vector3(HalfWidth, 0f, z), -90f);
            }
        }

        static void Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
        }

        static void EnsurePitsAndMarblesVisible()
        {
            foreach (var n in new[] { "Pit_01_Round", "Pit_02_Round", "Pit_03_Round" })
            {
                var p = GameObject.Find(n);
                if (p == null) continue;
                p.SetActive(true);
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
            }
            foreach (var n in new[] { "PlayerMarble_1_Blue", "PlayerMarble_2_Red", "PlayerMarble_3_Green", "PlayerMarble_4_Amber" })
            {
                var m = GameObject.Find(n);
                if (m != null) m.SetActive(true);
            }
        }

        static void Capture(Camera cam)
        {
            if (cam == null) throw new System.Exception("no cam");
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