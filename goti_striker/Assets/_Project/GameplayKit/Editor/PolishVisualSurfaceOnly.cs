#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Kriya — visual-only polish after no-ground fix.
    /// NEVER modifies GameplayCollider (enabled/trigger/size/layer).
    /// </summary>
    public static class PolishVisualSurfaceOnly
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string SoilMatPath = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Soil.mat";
        const string EdgeMatPath = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Edge.mat";
        const string ResultFlag = "Library/PolishVisualSurfaceOnly.result";

        [MenuItem("Pit Striker/GameplayKit/Polish Visual Surface Only (Kriya)")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[VisualPolish]</color> " + r);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                throw;
            }
        }

        static string Apply()
        {
            EnsureMats();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var collider = Find(root.transform, "GameplayCollider");
                if (collider == null) throw new System.Exception("GameplayCollider missing");
                var box = collider.GetComponent<BoxCollider>();
                if (box == null) throw new System.Exception("GameplayCollider BoxCollider missing");

                // Snapshot collider — must remain identical
                bool en = box.enabled;
                bool trig = box.isTrigger;
                Vector3 size = box.size;
                Vector3 center = box.center;
                int layer = collider.gameObject.layer;

                var visual = Find(root.transform, "VisualSurface");
                if (visual == null) throw new System.Exception("VisualSurface missing");

                // Sit visual just above collider top (local): collider at z=17, center.y=-0.25, size.y=0.5 → top y=0
                // Visual plane at y=0.02 avoids Z-fight with edge rails / map planes
                var vp = visual.localPosition;
                visual.localPosition = new Vector3(vp.x, 0.02f, vp.z);

                var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
                var edgeMat = AssetDatabase.LoadAssetAtPath<Material>(EdgeMatPath);
                var mr = visual.GetComponent<MeshRenderer>();
                if (mr != null && soil != null)
                {
                    mr.sharedMaterial = soil;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // flat ground — cleaner empty-scene look
                }

                var edgeRoot = Find(root.transform, "SurfaceEdge");
                if (edgeRoot != null)
                {
                    foreach (Transform rail in edgeRoot)
                    {
                        var p = rail.localPosition;
                        // Slightly above visual to avoid Z-fight with VisualSurface
                        rail.localPosition = new Vector3(p.x, 0.028f, p.z);
                        var r = rail.GetComponent<MeshRenderer>();
                        if (r != null)
                        {
                            if (edgeMat != null) r.sharedMaterial = edgeMat;
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                        // Ensure no collider crept onto rails
                        foreach (var c in rail.GetComponents<Collider>())
                            Object.DestroyImmediate(c);
                    }
                }

                // Wire SurfaceVisualConfig if present on GameplaySurface
                var surface = Find(root.transform, "GameplaySurface");
                if (surface != null)
                {
                    var cfg = surface.GetComponent<SurfaceVisualConfig>();
                    if (cfg == null) cfg = surface.gameObject.AddComponent<SurfaceVisualConfig>();
                    cfg.SurfaceRenderer = mr;
                    if (edgeRoot != null) cfg.EdgeRenderer = edgeRoot.GetComponentInChildren<Renderer>();
                    cfg.SurfaceMaterial = soil;
                    cfg.EdgeMaterial = edgeMat;
                    cfg.SurfaceYOffset = 0.02f;
                    cfg.SurfaceTint = new Color(0.88f, 0.64f, 0.38f, 1f);
                    cfg.Apply();
                }

                // VERIFY collider untouched
                if (box.enabled != en || box.isTrigger != trig || box.size != size || box.center != center || collider.gameObject.layer != layer)
                    throw new System.Exception("GameplayCollider was modified — aborting save");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return string.Format(
                    "Visual Y=0.02, edges Y=0.028, mats applied, rail colliders stripped. GameplayCollider unchanged size={0} center={1} layer={2}.",
                    size, center, layer);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureMats()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("URP/Lit");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Normal.png");

            var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
            if (soil == null) { soil = new Material(shader); AssetDatabase.CreateAsset(soil, SoilMatPath); }
            soil.shader = shader;
            if (albedo) soil.SetTexture("_BaseMap", albedo);
            if (normal) { soil.SetTexture("_BumpMap", normal); soil.EnableKeyword("_NORMALMAP"); soil.SetFloat("_BumpScale", 0.3f); }
            soil.SetColor("_BaseColor", new Color(0.88f, 0.64f, 0.38f, 1f));
            soil.SetFloat("_Smoothness", 0.22f);
            soil.SetTextureScale("_BaseMap", new Vector2(2.4f, 4.1f));
            soil.SetTextureScale("_BumpMap", new Vector2(2.4f, 4.1f));
            EditorUtility.SetDirty(soil);

            var edge = AssetDatabase.LoadAssetAtPath<Material>(EdgeMatPath);
            if (edge == null) { edge = new Material(shader); AssetDatabase.CreateAsset(edge, EdgeMatPath); }
            edge.shader = shader;
            edge.SetColor("_BaseColor", new Color(0.48f, 0.34f, 0.20f, 1f));
            edge.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(edge);
            AssetDatabase.SaveAssets();
        }

        static Transform Find(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }
    }
}
#endif