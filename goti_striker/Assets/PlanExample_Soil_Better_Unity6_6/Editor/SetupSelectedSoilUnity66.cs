using System.IO;
using UnityEditor;
using UnityEngine;

public static class SetupSelectedSoilUnity66
{
    [MenuItem("Tools/Goti Striker/Setup Selected Soil (Unity 6.6 Better)")]
    public static void SetupSelectedSoil()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Setup Soil", "Select the soil object in the Hierarchy first.", "OK");
            return;
        }
        string[] baseGuids = AssetDatabase.FindAssets("Soil_BaseColor_2K t:Texture2D");
        string[] normalGuids = AssetDatabase.FindAssets("Soil_Normal_2K t:Texture2D");
        string[] aoGuids = AssetDatabase.FindAssets("Soil_AO_2K t:Texture2D");
        if (baseGuids.Length == 0 || normalGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Setup Soil", "Soil textures were not found. Keep the package folder inside Assets.", "OK");
            return;
        }
        string basePath = AssetDatabase.GUIDToAssetPath(baseGuids[0]);
        string folder = Path.GetDirectoryName(basePath).Replace('\\', '/');
        string matPath = folder + "/M_Soil_Better_URP.mat";
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(normalGuids[0]));
        Texture2D aoTex = aoGuids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(aoGuids[0])) : null;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); } else { mat.shader = shader; }
        SetTextureImporter(basePath, false);
        SetTextureImporter(AssetDatabase.GUIDToAssetPath(normalGuids[0]), true);
        if (aoTex != null) SetTextureImporter(AssetDatabase.GUIDToAssetPath(aoGuids[0]), false);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normalTex);
        if (mat.HasProperty("_OcclusionMap") && aoTex != null) mat.SetTexture("_OcclusionMap", aoTex);
        if (mat.HasProperty("_OcclusionStrength")) mat.SetFloat("_OcclusionStrength", 0.65f);
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.22f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_NORMALMAP");
        if (aoTex != null) mat.EnableKeyword("_OCCLUSIONMAP");
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) renderer.sharedMaterial = mat; else { mats[0] = mat; renderer.sharedMaterials = mats; }
        }
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.25f, 14.5f);
        box.size = new Vector3(20f, 0.5f, 46f);
        box.isTrigger = false;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        EditorUtility.DisplayDialog("Setup Soil", "Soil material and collider were applied.", "OK");
    }
    static void SetTextureImporter(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.SaveAndReimport();
    }
}
