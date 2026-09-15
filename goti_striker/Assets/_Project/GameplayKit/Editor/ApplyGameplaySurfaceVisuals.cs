#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Adds soft edge border + kit surface materials to PitStriker_GameplayKit.
    /// Does NOT modify MeshCollider meshes/sizes or pit transforms.
    /// </summary>
    public static class ApplyGameplaySurfaceVisuals
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string SoilMatPath = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Soil.mat";
        const string EdgeMatPath = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Edge.mat";
        const string Albedo = "Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png";
        const string Normal = "Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Normal.png";
        const string ResultFlag = "Library/ApplyGameplaySurfaceVisuals.result";

        [MenuItem("Pit Striker/GameplayKit/Apply Surface Visuals (Kriya)")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[SurfaceVisual]</color> " + r);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                throw;
            }
        }

        static string Apply()
        {
            EnsureMaterials();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var surface = FindChild(root.transform, "GameplaySurface");
                var lane = FindChild(root.transform, "GameplayLane");
                if (surface == null || lane == null)
                    throw new System.Exception("GameplaySurface/GameplayLane missing on prefab");

                var laneMr = lane.GetComponent<MeshRenderer>();
                var laneMc = lane.GetComponent<MeshCollider>();
                Vector3 colSizeBefore = laneMc != null && laneMc.sharedMesh != null ? laneMc.sharedMesh.bounds.size : Vector3.zero;

                var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
                var edgeMat = AssetDatabase.LoadAssetAtPath<Material>(EdgeMatPath);
                if (laneMr != null) laneMr.sharedMaterial = soil;

                // Soft edge rim — visual only, no collider
                var edge = FindChild(surface, "SurfaceEdge");
                if (edge == null)
                {
                    var edgeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    edgeGo.name = "SurfaceEdge";
                    Object.DestroyImmediate(edgeGo.GetComponent<BoxCollider>());
                    edgeGo.transform.SetParent(surface, false);
                    // Frame around 20x34 lane: slightly larger, thin, sitting at y=0
                    // Cube default 1m; scale to border ring approximation (outer slab with hole not easy —
                    // use 4 rails)
                    Object.DestroyImmediate(edgeGo);
                    CreateEdgeRails(surface, edgeMat);
                }
                else
                {
                    foreach (var r in edge.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = edgeMat;
                }

                var cfg = surface.GetComponent<SurfaceVisualConfig>();
                if (cfg == null) cfg = surface.gameObject.AddComponent<SurfaceVisualConfig>();
                cfg.SurfaceRenderer = laneMr;
                var edgeRoot = FindChild(surface, "SurfaceEdge");
                if (edgeRoot != null)
                    cfg.EdgeRenderer = edgeRoot.GetComponentInChildren<Renderer>();
                cfg.SurfaceMaterial = soil;
                cfg.EdgeMaterial = edgeMat;
                cfg.Preset = SurfaceVisualConfig.MapPreset.Village;
                cfg.SurfaceTint = new Color(0.88f, 0.64f, 0.38f, 1f);
                cfg.EdgeTint = new Color(0.50f, 0.36f, 0.22f, 1f);
                cfg.SurfaceTiling = new Vector2(2.4f, 4.1f);
                cfg.SurfaceYOffset = 0.01f;
                cfg.Apply();

                // Verify collider mesh unchanged
                Vector3 colSizeAfter = laneMc != null && laneMc.sharedMesh != null ? laneMc.sharedMesh.bounds.size : Vector3.zero;
                if (colSizeBefore != Vector3.zero && colSizeAfter != colSizeBefore)
                    throw new System.Exception("Collider mesh size changed — abort");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return "Surface soil+edge applied. Collider size unchanged=" + colSizeAfter + ". Config on GameplaySurface.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void CreateEdgeRails(Transform surface, Material edgeMat)
        {
            var root = new GameObject("SurfaceEdge");
            root.transform.SetParent(surface, false);
            // Lane world ~20 x 34 centered at local z=17 under surface; rails in surface local space
            // Approximate outer frame: X ±10.15, Z from -0.15 to 34.15
            float halfW = 10.15f;
            float z0 = -0.15f, z1 = 34.15f;
            float thick = 0.3f;
            float y = 0.005f;
            MakeRail(root.transform, "Edge_L", new Vector3(-halfW, y, (z0 + z1) * 0.5f), new Vector3(thick, 0.02f, z1 - z0), edgeMat);
            MakeRail(root.transform, "Edge_R", new Vector3(halfW, y, (z0 + z1) * 0.5f), new Vector3(thick, 0.02f, z1 - z0), edgeMat);
            MakeRail(root.transform, "Edge_Near", new Vector3(0f, y, z0), new Vector3(halfW * 2f, 0.02f, thick), edgeMat);
            MakeRail(root.transform, "Edge_Far", new Vector3(0f, y, z1), new Vector3(halfW * 2f, 0.02f, thick), edgeMat);
        }

        static void MakeRail(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        static void EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("URP/Lit");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Albedo);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Normal);

            var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
            if (soil == null)
            {
                soil = new Material(shader);
                AssetDatabase.CreateAsset(soil, SoilMatPath);
            }
            soil.shader = shader;
            if (albedo) soil.SetTexture("_BaseMap", albedo);
            if (normal) { soil.SetTexture("_BumpMap", normal); soil.EnableKeyword("_NORMALMAP"); soil.SetFloat("_BumpScale", 0.35f); }
            soil.SetColor("_BaseColor", new Color(0.88f, 0.64f, 0.38f, 1f));
            soil.SetFloat("_Smoothness", 0.24f);
            soil.SetFloat("_Metallic", 0f);
            soil.SetTextureScale("_BaseMap", new Vector2(2.4f, 4.1f));
            soil.SetTextureScale("_BumpMap", new Vector2(2.4f, 4.1f));
            EditorUtility.SetDirty(soil);

            var edge = AssetDatabase.LoadAssetAtPath<Material>(EdgeMatPath);
            if (edge == null)
            {
                edge = new Material(shader);
                AssetDatabase.CreateAsset(edge, EdgeMatPath);
            }
            edge.shader = shader;
            edge.SetColor("_BaseColor", new Color(0.50f, 0.36f, 0.22f, 1f));
            edge.SetFloat("_Smoothness", 0.18f);
            edge.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(edge);
            AssetDatabase.SaveAssets();
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }
    }
}
#endif