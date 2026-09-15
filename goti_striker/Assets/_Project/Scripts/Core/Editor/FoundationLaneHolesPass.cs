#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Punch visual holes in GameplayLane so pits show. MeshCollider stays full (physics unchanged).
    /// </summary>
    public static class FoundationLaneHolesPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string MeshPath = "Assets/_Project/Art/Meshes/GameplayLane_WithPitHoles.asset";
        const string ResultFlag = "Library/FoundationLaneHolesPass.result";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_fence_gameview.png";

        static readonly Vector3[] PitWorld = {
            new Vector3(0f, 0f, 3f),
            new Vector3(0f, 0f, 16.5f),
            new Vector3(0f, 0f, 31f),
        };
        const float HoleRadius = 0.85f;

        [MenuItem("Pit Striker/Foundation Lane Pit Holes")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[LaneHoles]</color> " + r);
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
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var lane = GameObject.Find("GameplayLane");
            if (lane == null) throw new System.Exception("GameplayLane missing");

            var mf = lane.GetComponent<MeshFilter>();
            var mc = lane.GetComponent<MeshCollider>();
            if (mf == null) throw new System.Exception("no MeshFilter");

            // Ensure collider keeps a full plane mesh (physics)
            var full = CreatePlaneMesh(40, 68, null, 0f);
            full.name = "GameplayLane_FullCollider";
            if (mc != null)
            {
                mc.sharedMesh = full;
                // store collider mesh as sub-asset alongside visual if needed — keep in memory via asset
            }

            var visual = CreatePlaneMesh(40, 68, PitWorld, HoleRadius);
            visual.name = "GameplayLane_WithPitHoles";

            Directory.CreateDirectory("Assets/_Project/Art/Meshes");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(visual, MeshPath);
            // collider mesh as secondary
            AssetDatabase.AddObjectToAsset(full, MeshPath);
            AssetDatabase.SaveAssets();

            mf.sharedMesh = visual;
            if (mc != null) mc.sharedMesh = full;

            // Ensure pit visuals active
            foreach (var n in new[] { "Pit_01_Round", "Pit_02_Round", "Pit_03_Round" })
            {
                var p = GameObject.Find(n);
                if (p != null) p.SetActive(true);
            }
            var marble = GameObject.Find("PlayerMarble_1_Blue");
            if (marble != null) marble.SetActive(true);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 9.5f, -9.0f);
                cam.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
                cam.fieldOfView = 55f;
            }
            Capture(cam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            return "Visual holes r=" + HoleRadius + " at pits; MeshCollider full plane kept. Shot=" + ShotPath;
        }

        // Plane in LOCAL space of GameplayLane: Unity plane is 10x10 centered, scale (2,1,3.4) → world 20x34 at pos z=17
        // Local X/Z in [-5,5]. World (wx,wz) → local: lx=wx/2, lz=(wz-17)/3.4
        static Mesh CreatePlaneMesh(int segsX, int segsZ, Vector3[] worldHoles, float holeR)
        {
            float min = -5f, max = 5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector2[] localHoles = null;
            float localR = 0f;
            if (worldHoles != null)
            {
                localHoles = new Vector2[worldHoles.Length];
                for (int i = 0; i < worldHoles.Length; i++)
                {
                    localHoles[i] = new Vector2(worldHoles[i].x / 2f, (worldHoles[i].z - 17f) / 3.4f);
                }
                // approximate: hole radius in local X uses /2, in Z uses /3.4 — use average scale ~2.7
                localR = holeR / 2.5f;
            }

            var grid = new int[segsX + 1, segsZ + 1];
            for (int z = 0; z <= segsZ; z++)
            {
                for (int x = 0; x <= segsX; x++)
                {
                    float lx = Mathf.Lerp(min, max, x / (float)segsX);
                    float lz = Mathf.Lerp(min, max, z / (float)segsZ);
                    bool inHole = false;
                    if (localHoles != null)
                    {
                        for (int h = 0; h < localHoles.Length; h++)
                        {
                            float dx = lx - localHoles[h].x;
                            float dz = lz - localHoles[h].y;
                            // elliptical compensate: stretch Z test
                            if ((dx * dx) / (localR * localR) + (dz * dz) / ((holeR / 3.4f) * (holeR / 3.4f)) <= 1f)
                            {
                                inHole = true;
                                break;
                            }
                        }
                    }
                    if (inHole) { grid[x, z] = -1; continue; }
                    grid[x, z] = verts.Count;
                    verts.Add(new Vector3(lx, 0f, lz));
                    uvs.Add(new Vector2(x / (float)segsX, z / (float)segsZ));
                }
            }

            for (int z = 0; z < segsZ; z++)
            {
                for (int x = 0; x < segsX; x++)
                {
                    int a = grid[x, z];
                    int b = grid[x + 1, z];
                    int c = grid[x, z + 1];
                    int d = grid[x + 1, z + 1];
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