#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Soil warm-brown + non-playable visual skirt. Does not touch pits, kit, fences, or GameplayLane collider.
    /// </summary>
    public static class FoundationSoilSkirtPass
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string ResultFlag = "Library/FoundationSoilSkirtPass.result";
        const string Albedo = "Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Albedo.png";
        const string Normal = "Assets/_Project/Art/Textures/Foundation/T_Soil_Lane_Normal.png";
        const string MatPath = "Assets/_Project/Art/Materials/M_GameplayLane_Soil.mat";
        const string SkirtMatPath = "Assets/_Project/Art/Materials/M_VisualGround_Skirt.mat";
        const string ShotPath = "C:/Users/Tisan/Documents/PitStriker-Working/Preview/foundation_soil_gameview.png";

        [MenuItem("Pit Striker/Foundation Soil + Skirt Pass")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            try
            {
                string r = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + r);
                Debug.Log("<color=#00FF88>[SoilSkirt]</color> " + r);
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

            // Configure texture imports
            SetTextureImport(Albedo, sRGB: true, normal: false);
            SetTextureImport(Normal, sRGB: false, normal: true);
            AssetDatabase.ImportAsset(Albedo); AssetDatabase.ImportAsset(Normal);

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Albedo);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Normal);
            if (albedo == null || normal == null)
                throw new System.Exception("Soil textures missing after import");

            var laneMat = CreateOrUpdateSoilMat(MatPath, albedo, normal, new Vector2(5f, 8.5f), new Color(0.55f, 0.33f, 0.18f));
            var skirtMat = CreateOrUpdateSoilMat(SkirtMatPath, albedo, normal, new Vector2(18f, 18f), new Color(0.48f, 0.29f, 0.15f));

            var lane = GameObject.Find("GameplayLane");
            if (lane == null) throw new System.Exception("GameplayLane not found");

            // Material only — do not touch MeshCollider / transform / scale
            var mr = lane.GetComponent<MeshRenderer>();
            if (mr == null) throw new System.Exception("GameplayLane missing MeshRenderer");
            mr.sharedMaterial = laneMat;
            var col = lane.GetComponent<MeshCollider>();
            bool colOk = col != null && col.enabled;

            // Visual skirt — no collider
            var oldSkirt = GameObject.Find("VisualGroundSkirt");
            if (oldSkirt) Object.DestroyImmediate(oldSkirt);
            var skirt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            skirt.name = "VisualGroundSkirt";
            Object.DestroyImmediate(skirt.GetComponent<MeshCollider>());
            var foundation = GameObject.Find("Foundation_Gameplay");
            if (foundation != null) skirt.transform.SetParent(foundation.transform, true);
            skirt.transform.position = new Vector3(0f, -0.02f, 17f);
            skirt.transform.rotation = Quaternion.identity;
            // 10m * 14 = 140m visual extent
            skirt.transform.localScale = new Vector3(14f, 1f, 14f);
            skirt.GetComponent<MeshRenderer>().sharedMaterial = skirtMat;

            // Camera: show lane + pits + marble
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 3.2f, -4.5f);
                cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                cam.fieldOfView = 58f;
            }

            Capture(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Pit lock check (read-only)
            var p1 = GameObject.Find("Pit_01_Round");
            var p2 = GameObject.Find("Pit_02_Round");
            var p3 = GameObject.Find("Pit_03_Round");
            string pits = "pits missing";
            if (p1 && p2 && p3)
                pits = string.Format("pits z=({0},{1},{2}) x=({3},{4},{5})",
                    p1.transform.localPosition.z, p2.transform.localPosition.z, p3.transform.localPosition.z,
                    p1.transform.localPosition.x, p2.transform.localPosition.x, p3.transform.localPosition.x);

            return string.Format(
                "Lane soil mat applied (colliderEnabled={0}). VisualGroundSkirt added (no collider). Shot={1}. {2}. Fences untouched.",
                colOk, ShotPath, pits);
        }

        static void SetTextureImport(string path, bool sRGB, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.sRGBTexture = sRGB;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        static Material CreateOrUpdateSoilMat(string path, Texture2D albedo, Texture2D normal, Vector2 tiling, Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("URP/Lit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 0.55f);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.16f);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_BumpMap", tiling);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Capture(Camera cam)
        {
            if (cam == null) throw new System.Exception("No Main Camera");
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