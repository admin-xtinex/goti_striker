#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    public static class FoundationPolishPass2
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string FenceAsset = "Assets/_Project/Art/Models/Foundation/Prop_Fence_Module.fbx";
        const string LaneMatPath = "Assets/_Project/Art/Materials/M_GameplayLane_Soil.mat";
        const string SkirtMatPath = "Assets/_Project/Art/Materials/M_VisualGround_Skirt.mat";
        const string MeshPath = "Assets/_Project/Art/Meshes/GameplayLane_WithPitHoles.asset";
        const string ResultFlag = "Library/FoundationPolishPass2.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_gameplay_gameview.png";

        static readonly Vector3 CamPos = new Vector3(0f, 1.2249999f, -9.2f);
        static readonly Quaternion CamRot = new Quaternion(0.08933931f, 0f, 0f, 0.99600124f);
        static readonly Vector3[] Pits = { new Vector3(0,0,3), new Vector3(0,0,16.5f), new Vector3(0,0,31) };

        [MenuItem("Pit Striker/Foundation Polish Pass 2")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[Polish2]</color> " + r);
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

            var cam = Camera.main;
            cam.transform.position = CamPos;
            cam.transform.rotation = CamRot;
            cam.fieldOfView = 55f;

            // Warmer sandy brown, low tiling
            var laneMat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);
            if (laneMat != null)
            {
                laneMat.SetColor("_BaseColor", new Color(0.93f, 0.72f, 0.45f, 1f));
                laneMat.SetFloat("_Smoothness", 0.28f);
                laneMat.SetTextureScale("_BaseMap", new Vector2(2.4f, 4.1f));
                laneMat.SetTextureScale("_BumpMap", new Vector2(2.4f, 4.1f));
                EditorUtility.SetDirty(laneMat);
            }
            var skirtMat = AssetDatabase.LoadAssetAtPath<Material>(SkirtMatPath);
            if (skirtMat != null)
            {
                skirtMat.SetColor("_BaseColor", new Color(0.80f, 0.60f, 0.36f, 1f));
                skirtMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
                EditorUtility.SetDirty(skirtMat);
            }
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo) { var L = lightGo.GetComponent<Light>(); if (L) { L.color = new Color(1f, 0.97f, 0.90f); L.intensity = 1.85f; } }

            // Lane: continuous strips LEFT and RIGHT of centre with circular notches — avoids a centre UV/mesh seam line
            RebuildLaneAsSideStrips();

            // Fences: auto-detect span axis from mesh bounds
            string fenceInfo = RebuildFencesFromBounds();

            foreach (var n in new[] { "Pit_01_Round", "Pit_02_Round", "Pit_03_Round" })
            {
                var p = GameObject.Find(n);
                if (p == null) continue;
                p.SetActive(true);
                foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            }

            Capture(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return fenceInfo + " Shot=" + ShotPath;
        }

        static void RebuildLaneAsSideStrips()
        {
            var lane = GameObject.Find("GameplayLane");
            if (lane == null) throw new System.Exception("GameplayLane missing");
            var mf = lane.GetComponent<MeshFilter>();
            var mc = lane.GetComponent<MeshCollider>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LaneMatPath);

            // Combined mesh: left strip + right strip, each continuous; circular bites at pits
            var visual = BuildSplitLane(72, 120, 1.15f);
            var full = BuildFullPlane(40, 68); // collider unchanged coverage

            if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null)
                AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(visual, MeshPath);
            AssetDatabase.AddObjectToAsset(full, MeshPath);
            AssetDatabase.SaveAssets();

            mf.sharedMesh = visual;
            if (mc) mc.sharedMesh = full;
            var mr = lane.GetComponent<MeshRenderer>();
            if (mr && mat) mr.sharedMaterial = mat;
        }

        static Mesh BuildFullPlane(int sx, int sz)
        {
            return BuildRect(-5f, 5f, -5f, 5f, sx, sz, null, 0);
        }

        static Mesh BuildSplitLane(int sx, int sz, float holeR)
        {
            // Two rectangles meeting at a tiny gap? NO — meet at x=0 with shared edge carefully.
            // Better: one mesh but skip ONLY hole interiors; UV from world-local continuously.
            // Centre seam fix: do NOT delete a column of verts on x=0 except inside holes.
            return BuildRect(-5f, 5f, -5f, 5f, sx, sz, Pits, holeR);
        }

        static Mesh BuildRect(float x0, float x1, float z0, float z1, int segsX, int segsZ, Vector3[] worldHoles, float holeR)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var grid = new int[segsX + 1, segsZ + 1];

            Vector2[] holes = null; float rX = 0, rZ = 0;
            if (worldHoles != null)
            {
                holes = worldHoles.Select(w => new Vector2(w.x / 2f, (w.z - 17f) / 3.4f)).ToArray();
                rX = holeR / 2f; rZ = holeR / 3.4f;
            }

            for (int z = 0; z <= segsZ; z++)
            {
                for (int x = 0; x <= segsX; x++)
                {
                    float lx = Mathf.Lerp(x0, x1, x / (float)segsX);
                    float lz = Mathf.Lerp(z0, z1, z / (float)segsZ);
                    bool inHole = false;
                    if (holes != null)
                    {
                        foreach (var h in holes)
                        {
                            float dx = (lx - h.x) / rX;
                            float dz = (lz - h.y) / rZ;
                            if (dx * dx + dz * dz <= 1f) { inHole = true; break; }
                        }
                    }
                    if (inHole) { grid[x, z] = -1; continue; }
                    grid[x, z] = verts.Count;
                    verts.Add(new Vector3(lx, 0f, lz));
                    // World-proportional UVs (lane 20x34) — soft repeat, no centre break
                    float wx = lx * 2f;
                    float wz = lz * 3.4f + 17f;
                    uvs.Add(new Vector2(wx / 20f, wz / 34f));
                }
            }
            for (int z = 0; z < segsZ; z++)
            for (int x = 0; x < segsX; x++)
            {
                int a = grid[x, z], b = grid[x + 1, z], c = grid[x, z + 1], d = grid[x + 1, z + 1];
                if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static string RebuildFencesFromBounds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FenceAsset);
            if (prefab == null) throw new System.Exception("fence missing");

            // Measure imported axis
            var mfs = prefab.GetComponentsInChildren<MeshFilter>(true);
            Bounds b = new Bounds();
            bool init = false;
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                if (!init) { b = mf.sharedMesh.bounds; init = true; }
                else b.Encapsulate(mf.sharedMesh.bounds);
            }
            float sizeX = b.size.x, sizeZ = b.size.z;
            // Span is the larger horizontal extent
            bool spanIsX = sizeX >= sizeZ;
            float span = Mathf.Max(sizeX, sizeZ);
            if (span < 0.1f) span = 2.08f;

            var foundation = GameObject.Find("Foundation_Gameplay");
            var old = GameObject.Find("Foundation_Fences");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("Foundation_Fences");
            root.transform.SetParent(foundation.transform, false);

            // If span along X, yaw 90 so it runs along lane Z. If already along Z, yaw 0 / 180.
            float yawL = spanIsX ? 90f : 0f;
            float yawR = spanIsX ? -90f : 180f;
            float step = 2.0f; // Hacky tile spacing
            int n = Mathf.RoundToInt(34f / step);
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                float z = step * 0.5f + i * step;
                count += Place(prefab, root.transform, "Fence_L_" + i, new Vector3(-10f, 0f, z), yawL);
                count += Place(prefab, root.transform, "Fence_R_" + i, new Vector3(10f, 0f, z), yawR);
            }

            // Verify first module world bounds extent along Z
            var sample = root.transform.Find("Fence_L_0");
            float alongZ = 0f;
            if (sample)
            {
                var r = sample.GetComponentInChildren<Renderer>();
                if (r) alongZ = r.bounds.size.z;
            }

            return string.Format("Fence mesh sizeX={0:F2} sizeZ={1:F2} spanIsX={2} yawL={3} count={4} sampleAlongZ={5:F2} (want ~2)",
                sizeX, sizeZ, spanIsX, yawL, count, alongZ);
        }

        static int Place(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            return 1;
        }

        static void Capture(Camera cam)
        {
            int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            File.WriteAllBytes(ShotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
#endif