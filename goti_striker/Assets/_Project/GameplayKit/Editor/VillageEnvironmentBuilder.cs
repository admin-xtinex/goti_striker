#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Dresses the open scene around the gameplay area, visual only:
    ///   - soil (PlanExample_Soil_Better) as the kit's ground, with pit holes cut;
    ///   - a kraweznik curb border exactly on the kit's collision boundary (x ±10, z -8.5 .. 37.5);
    ///   - terrain outside the area (sunk below the soil inside it, so it never shows in a pit);
    ///   - grass, flowers, bushes and mushrooms filled around the boundary, denser near it;
    ///   - spruce trees further out.
    /// Everything generated lives under "Environment_Village" in named groups. Nothing gets a collider,
    /// nothing tall goes behind the camera or inside the camera's keep-clear volume, and physics,
    /// gameplay objects and settings are untouched. Re-running rebuilds it (fixed seed, same result).
    /// </summary>
    public static class VillageEnvironmentBuilder
    {
        const string RootName = "Environment_Village";
        const string GeneratedFolder = "Assets/_Project/Art/Environment/Village";

        // Kit collision boundary (BoundaryCollision inner faces).
        const float HalfWidth = 10f, ZMin = -8.5f, ZMax = 37.5f;
        static float ZMid => (ZMin + ZMax) * 0.5f;

        // Camera keep-clear volume from the gameplay area spec: nothing tall inside it.
        const float KeepClearX = 11.5f, KeepClearZMin = -13.5f, KeepClearZMax = 42.5f;
        // Behind the start the camera looks forward over this strip: keep it low for longer.
        const float BehindStartClearX = 16f, BehindStartClearZ = -26f;

        const int Seed = 20260917;

        [MenuItem("Pit Striker/Environment/Build Village Environment")]
        public static void BuildMenu()
        {
            string report = Build();
            Debug.Log("[ENVIRONMENT]\n" + report);
            EditorUtility.DisplayDialog("Village Environment", report + "\n\nSave the scene to keep it.", "OK");
        }

        /// <summary>Batch entry: builds into the gameplay scene and saves it.</summary>
        public static void BuildBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Village_Graphics_Test.unity", OpenSceneMode.Single);
            string report;
            try { report = Build(); }
            catch (System.Exception ex) { report = "FAIL " + ex; }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/VillageEnvironment.result", report);
            Debug.Log("[ENVIRONMENT]\n" + report);
            EditorApplication.Exit(report.StartsWith("FAIL") ? 1 : 0);
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            var rng = new System.Random(Seed);
            Directory.CreateDirectory(GeneratedFolder);

            // Loose pieces placed by hand are folded into the build: curbs are rebuilt as one
            // consistent border, hand-placed grass is kept and grouped.
            var looseCurbs = SceneRoots().Where(g => g.name.ToLower().StartsWith("boundary")).ToList();
            var looseGrass = SceneRoots().Where(g => g.name.StartsWith("UNS_Grass")).ToList();

            var old = SceneRoots().FirstOrDefault(g => g.name == RootName);
            if (old != null) { Undo.DestroyObjectImmediate(old); sb.AppendLine("rebuilt previous environment"); }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build village environment");
            var boundaryGroup = Group(root, "Boundary");
            var terrainGroup = Group(root, "Terrain");
            var vegetation = Group(root, "Vegetation");
            var grassGroup = Group(vegetation, "Grass");
            var flowerGroup = Group(vegetation, "Flowers");
            var bushGroup = Group(vegetation, "Bushes");
            var mushroomGroup = Group(vegetation, "Mushrooms");
            var treeGroup = Group(root, "Trees");

            sb.AppendLine(BuildSoil());
            var terrain = BuildTerrain(terrainGroup, rng, sb);
            BuildBoundary(boundaryGroup, looseCurbs, sb);

            var occupied = new List<Vector3>();
            foreach (var g in looseGrass)
            {
                Undo.SetTransformParent(g.transform, grassGroup, "Group grass");
                StripColliders(g);
                MarkStatic(g);
                occupied.Add(g.transform.position);
            }
            if (looseGrass.Count > 0) sb.AppendLine($"grouped {looseGrass.Count} hand-placed grass");

            float Ground(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;

            // Vegetation: a dense band hugging the boundary, then thinning out.
            int grass = Scatter("Assets/Vegetation/Grass/Prefabs/UNS_Grass.prefab", grassGroup, rng, Ground, occupied,
                count: 1400, near: 0.35f, far: 26f, bias: 2.2f, spacing: 0.55f, scale: (0.8f, 1.3f), low: true);
            int flowers = Scatter("Assets/Vegetation/Flowers/Prefabs/UNS_Flower.prefab", flowerGroup, rng, Ground, occupied,
                count: 260, near: 0.6f, far: 20f, bias: 1.6f, spacing: 0.7f, scale: (0.8f, 1.2f), low: true);
            int bushes = Scatter("Assets/Vegetation/Bushes/Prefabs/UNS_Bush.prefab", bushGroup, rng, Ground, occupied,
                count: 140, near: 1.6f, far: 30f, bias: 1.4f, spacing: 1.6f, scale: (0.8f, 1.35f), low: false);
            int mushrooms = Scatter("Assets/Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab", mushroomGroup, rng, Ground, occupied,
                count: 70, near: 1.0f, far: 24f, bias: 1.2f, spacing: 1.0f, scale: (0.8f, 1.2f), low: true);
            sb.AppendLine($"vegetation: {grass} grass, {flowers} flowers, {bushes} bushes, {mushrooms} mushroom patches");

            // Trees: further out, never between the camera and the lane.
            var treeSpots = new List<Vector3>();
            int trees = 0;
            string[] treePrefabs = { "Assets/Trees/Fir/Prefabs/UNS_Spruce_01.prefab", "Assets/Trees/Fir/Prefabs/UNS_Spruce_02.prefab" };
            for (int attempt = 0; attempt < 6000 && trees < 170; attempt++)
            {
                var p = RandomAround(rng, 6f, 70f, 1.0f);
                if (!TallAllowed(p)) continue;
                if (treeSpots.Any(t => (t - p).sqrMagnitude < 4.2f * 4.2f)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabs[rng.Next(treePrefabs.Length)]);
                if (prefab == null) break;
                p.y = Ground(p);
                Place(prefab, treeGroup, p, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.85f, 1.45f, (float)rng.NextDouble()));
                treeSpots.Add(p);
                trees++;
            }
            sb.AppendLine($"trees: {trees}");

            HideKitTestVisuals(sb);
            EditorSceneManager.MarkSceneDirty(root.scene);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ soil

        static string BuildSoil()
        {
            const string soilModel = "Assets/PlanExample_Soil_Better_Unity6_6/PlanExample_Soil_Unity6_Better_LOD0.obj";
            var kit = Object.FindAnyObjectByType<GameplayKitRoot>();
            var slot = kit != null ? kit.transform.Find(GroundWithPitHoles.SlotName) : null;

            // Already the kit ground? Just make sure the holes are current.
            if (slot != null && slot.Cast<Transform>().Any(c => c.name.StartsWith("PlanExample_Soil")))
                return "soil: already the kit ground; " + slot.GetComponent<GroundWithPitHoles>().Refresh(force: false);

            // Readable so the pit holes can be cut from it.
            if (AssetImporter.GetAtPath(soilModel) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var soil = SceneRoots().FirstOrDefault(g => g.name.StartsWith("PlanExample_Soil"));
            if (soil == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(soilModel);
                if (model == null) return "soil: model not found at " + soilModel;
                soil = (GameObject)PrefabUtility.InstantiatePrefab(model);
                Undo.RegisterCreatedObjectUndo(soil, "Add soil");
            }
            soil.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            soil.transform.localScale = Vector3.one;

            var mat = SoilMaterial();
            foreach (var r in soil.GetComponentsInChildren<MeshRenderer>(true))
            {
                Undo.RecordObject(r, "Soil material");
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // The hand-made placeholder ground would z-fight with the soil.
            var placeholder = SceneRoots().FirstOrDefault(g => g.name == "TemporaryGround_PlaceholderMap");
            if (placeholder != null && placeholder.activeSelf) { Undo.RecordObject(placeholder, "Hide placeholder"); placeholder.SetActive(false); }

            return "soil: " + (GroundSlotMenu.Install(soil, interactive: false) ?? "cancelled").Replace("\n\n", " ").Replace("\n", " ");
        }

        static Material SoilMaterial()
        {
            string path = $"{GeneratedFolder}/M_Village_Soil.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            const string dir = "Assets/PlanExample_Soil_Better_Unity6_6/";
            ConfigureTexture(dir + "Soil_BaseColor_2K.png", normalMap: false, srgb: true);
            ConfigureTexture(dir + "Soil_Normal_2K.png", normalMap: true, srgb: false);
            ConfigureTexture(dir + "Soil_AO_2K.png", normalMap: false, srgb: false);

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Soil_BaseColor_2K.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Soil_Normal_2K.png");
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", 0.35f); mat.EnableKeyword("_NORMALMAP"); }
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Soil_AO_2K.png");
            if (ao != null) { mat.SetTexture("_OcclusionMap", ao); mat.SetFloat("_OcclusionStrength", 0.65f); mat.EnableKeyword("_OCCLUSIONMAP"); }
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void ConfigureTexture(string path, bool normalMap, bool srgb)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType == type && ti.sRGBTexture == srgb && ti.wrapMode == TextureWrapMode.Repeat) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ terrain

        static Terrain BuildTerrain(Transform parent, System.Random rng, StringBuilder sb)
        {
            var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/Data/UNS_Terrain.asset");
            if (source == null) { sb.AppendLine("terrain: UNS_Terrain.asset not found, skipped"); return null; }

            string path = $"{GeneratedFolder}/Village_TerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset("Assets/Terrain/Data/UNS_Terrain.asset", path);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            const float size = 200f, baseY = -0.6f, heightScale = 14f;
            data.size = new Vector3(size, heightScale, size);
            var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layers/UNS_Grass.terrainlayer");
            var dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layers/UNS_Dirt.terrainlayer");
            data.terrainLayers = new[] { grass, dirt }.Where(l => l != null).ToArray();
            data.treeInstances = new TreeInstance[0];

            Vector3 origin = new Vector3(-size * 0.5f, baseY, ZMid - size * 0.5f);
            float noiseX = (float)rng.NextDouble() * 1000f, noiseZ = (float)rng.NextDouble() * 1000f;

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float wx = origin.x + ix / (float)(res - 1) * size;
                float wz = origin.z + iz / (float)(res - 1) * size;
                float y = GroundHeight(wx, wz, noiseX, noiseZ);
                heights[iz, ix] = Mathf.Clamp01((y - baseY) / heightScale);
            }
            data.SetHeights(0, 0, heights);

            if (data.terrainLayers.Length >= 2)
            {
                int ar = data.alphamapResolution;
                var alpha = new float[ar, ar, data.terrainLayers.Length];
                for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float wx = origin.x + ix / (float)(ar - 1) * size;
                    float wz = origin.z + iz / (float)(ar - 1) * size;
                    float d = DistanceOutside(wx, wz);
                    float edgeDirt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 4.5f, d));
                    float patches = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.78f, Mathf.PerlinNoise(noiseX + wx * 0.06f, noiseZ + wz * 0.06f)));
                    float dirtW = Mathf.Clamp01(Mathf.Max(edgeDirt, patches * 0.7f));
                    alpha[iz, ix, 0] = 1f - dirtW;
                    alpha[iz, ix, 1] = dirtW;
                }
                data.SetAlphamaps(0, 0, alpha);
            }
            EditorUtility.SetDirty(data);

            // A Terrain without a TerrainCollider: scenery only.
            var go = new GameObject("Terrain_Outside");
            Undo.RegisterCreatedObjectUndo(go, "Terrain");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            var terrain = go.AddComponent<Terrain>();
            terrain.terrainData = data;
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline != null && pipeline.defaultTerrainMaterial != null) terrain.materialTemplate = pipeline.defaultTerrainMaterial;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 60f;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            terrain.Flush();
            sb.AppendLine($"terrain: {size}x{size} m, heightmap {res}, no collider");
            return terrain;
        }

        /// <summary>Metres outside the gameplay rectangle (0 inside it).</summary>
        static float DistanceOutside(float x, float z)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - HalfWidth, 0f);
            float dz = Mathf.Max(Mathf.Max(ZMin - z, z - ZMax), 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float GroundHeight(float x, float z, float nx, float nz)
        {
            float d = DistanceOutside(x, z);
            // Inside the area (and a little around its edge) the terrain sits below the pit bowls,
            // which reach 0.18 m down, so it can never show through a pit or poke through the soil.
            if (d < 0.25f) return -0.32f;
            float edge = Mathf.Lerp(-0.32f, -0.03f, Mathf.InverseLerp(0.25f, 1.2f, d));
            float ripple = (Mathf.PerlinNoise(nx + x * 0.09f, nz + z * 0.09f) - 0.5f) * 0.25f * Mathf.InverseLerp(2f, 8f, d);
            float hills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 60f, d))
                          * (1.2f + 4.5f * Mathf.PerlinNoise(nx * 0.5f + x * 0.025f, nz * 0.5f + z * 0.025f));
            return edge + ripple + hills;
        }

        // ------------------------------------------------------------------ boundary

        static void BuildBoundary(Transform parent, List<GameObject> handPlaced, StringBuilder sb)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Free Starter Pack/Prefabs/kraweznik.prefab");
            if (prefab == null) { sb.AppendLine("boundary: kraweznik prefab not found"); return; }

            // Measure one curb as placed with no rotation.
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var b = CombinedBounds(probe);
            Object.DestroyImmediate(probe);
            bool longOnZ = b.size.z >= b.size.x;
            float length = longOnZ ? b.size.z : b.size.x;
            float depth = longOnZ ? b.size.x : b.size.z;
            Vector3 pivotToCentre = b.center;   // model pivot may not be the centre

            // Follow the height and inset of a hand-placed curb when there is one (it sits on the
            // collision line); otherwise put the curb's inner face on the line.
            float y = 0f, inset = depth * 0.5f;
            var sample = handPlaced.FirstOrDefault();
            if (sample != null)
            {
                var sb2 = CombinedBounds(sample);
                y = sample.transform.position.y;
                bool sideCurb = Mathf.Abs(sample.transform.position.x) > 8f;
                inset = sideCurb ? Mathf.Abs(sb2.center.x) - HalfWidth : inset;
            }
            foreach (var g in handPlaced) Undo.DestroyObjectImmediate(g);

            int pieces = 0;
            // Sides run the full length plus the corners; ends run between the side curbs.
            pieces += Edge(parent, prefab, new Vector3(-HalfWidth - inset, y, ZMin - depth), new Vector3(-HalfWidth - inset, y, ZMax + depth), length, longOnZ, pivotToCentre);
            pieces += Edge(parent, prefab, new Vector3(HalfWidth + inset, y, ZMin - depth), new Vector3(HalfWidth + inset, y, ZMax + depth), length, longOnZ, pivotToCentre);
            pieces += Edge(parent, prefab, new Vector3(-HalfWidth + depth * 0.5f, y, ZMin - inset), new Vector3(HalfWidth - depth * 0.5f, y, ZMin - inset), length, longOnZ, pivotToCentre);
            pieces += Edge(parent, prefab, new Vector3(-HalfWidth + depth * 0.5f, y, ZMax + inset), new Vector3(HalfWidth - depth * 0.5f, y, ZMax + inset), length, longOnZ, pivotToCentre);
            sb.AppendLine($"boundary: {pieces} kraweznik curbs (segment {length:F2} m) on x ±{HalfWidth}, z {ZMin}..{ZMax}" +
                          (handPlaced.Count > 0 ? $", replacing {handPlaced.Count} hand-placed" : ""));
        }

        /// <summary>Lays curbs end to end from a to b, stretched slightly so they meet exactly.</summary>
        static int Edge(Transform parent, GameObject prefab, Vector3 a, Vector3 b, float length, bool longOnZ, Vector3 pivotToCentre)
        {
            Vector3 dir = b - a;
            float total = dir.magnitude;
            int count = Mathf.Max(1, Mathf.RoundToInt(total / length));
            float stretch = total / (count * length);
            dir.Normalize();
            // Rotate the curb's long axis onto the edge.
            Quaternion rot = Quaternion.FromToRotation(longOnZ ? Vector3.forward : Vector3.right, dir);
            for (int i = 0; i < count; i++)
            {
                Vector3 centre = a + dir * ((i + 0.5f) * length * stretch);
                Vector3 scale = longOnZ ? new Vector3(1f, 1f, stretch) : new Vector3(stretch, 1f, 1f);
                var go = Place(prefab, parent, Vector3.zero, 0f, 1f);
                go.transform.rotation = rot;
                go.transform.localScale = scale;
                // Put the mesh centre (not the pivot) on the line.
                Vector3 offset = rot * Vector3.Scale(pivotToCentre, scale);
                go.transform.position = new Vector3(centre.x - offset.x, a.y, centre.z - offset.z);
                go.name = $"Curb_{parent.childCount:000}";
            }
            return count;
        }

        // ------------------------------------------------------------------ scattering

        delegate float GroundFn(Vector3 p);

        static int Scatter(string prefabPath, Transform parent, System.Random rng, GroundFn ground, List<Vector3> occupied,
                           int count, float near, float far, float bias, float spacing, (float min, float max) scale, bool low)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return 0;
            int placed = 0;
            float sp2 = spacing * spacing;
            for (int attempt = 0; attempt < count * 8 && placed < count; attempt++)
            {
                var p = RandomAround(rng, near, far, bias);
                if (!low && !TallAllowed(p)) continue;
                if (low && !LowAllowed(p)) continue;
                bool clash = false;
                foreach (var o in occupied)
                    if ((o.x - p.x) * (o.x - p.x) + (o.z - p.z) * (o.z - p.z) < sp2) { clash = true; break; }
                if (clash) continue;
                p.y = ground(p);
                Place(prefab, parent, p, (float)rng.NextDouble() * 360f,
                      Mathf.Lerp(scale.min, scale.max, (float)rng.NextDouble()));
                occupied.Add(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// A point outside the boundary, between <paramref name="near"/> and <paramref name="far"/> metres
        /// from it; <paramref name="bias"/> > 1 crowds points toward the boundary.
        /// </summary>
        static Vector3 RandomAround(System.Random rng, float near, float far, float bias)
        {
            float t = Mathf.Pow((float)rng.NextDouble(), bias);
            float d = Mathf.Lerp(near, far, t);
            // Pick a spot on the boundary perimeter uniformly, then push outward.
            float w = HalfWidth * 2f, l = ZMax - ZMin, perim = 2f * (w + l);
            float s = (float)rng.NextDouble() * perim;
            float along = (float)rng.NextDouble() * 2f - 1f;   // spread around corners
            if (s < l) return new Vector3(HalfWidth + d, 0f, ZMin + s);                               // right
            if (s < 2 * l) return new Vector3(-HalfWidth - d, 0f, ZMin + (s - l));                    // left
            if (s < 2 * l + w) return new Vector3(-HalfWidth + (s - 2 * l) + along * 0.5f, 0f, ZMax + d);  // far
            return new Vector3(-HalfWidth + (s - 2 * l - w) + along * 0.5f, 0f, ZMin - d);           // near
        }

        /// <summary>Low vegetation may go anywhere outside the boundary.</summary>
        static bool LowAllowed(Vector3 p) => DistanceOutside(p.x, p.z) > 0.3f;

        /// <summary>Bushes and trees stay out of the camera keep-clear volume and the strip behind the start.</summary>
        static bool TallAllowed(Vector3 p)
        {
            if (DistanceOutside(p.x, p.z) < 1.2f) return false;
            if (Mathf.Abs(p.x) < KeepClearX && p.z > KeepClearZMin && p.z < KeepClearZMax) return false;
            if (Mathf.Abs(p.x) < BehindStartClearX && p.z < ZMin && p.z > BehindStartClearZ) return false;
            return true;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            StripColliders(go);
            FillMissingMaterials(go, prefab.name);
            MarkStatic(go);
            return go;
        }

        /// <summary>
        /// Some supplied prefabs reference materials that were not included (the spruce bark, the
        /// mushroom patch), which renders magenta. Fill only those empty slots with a plain URP Lit
        /// stand-in; the prefab assets themselves are not changed.
        /// </summary>
        static void FillMissingMaterials(GameObject go, string prefabName)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) continue;
                    mats[i] = StandIn(prefabName);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static readonly Dictionary<string, Material> StandIns = new Dictionary<string, Material>();

        static Material StandIn(string prefabName)
        {
            string key; Color color; float smooth;
            if (prefabName.Contains("Spruce") || prefabName.Contains("Tree")) { key = "M_Village_Bark"; color = new Color(0.27f, 0.19f, 0.13f); smooth = 0.05f; }
            else if (prefabName.Contains("Mushroom")) { key = "M_Village_Mushroom"; color = new Color(0.86f, 0.78f, 0.66f); smooth = 0.25f; }
            else { key = "M_Village_Plain"; color = new Color(0.45f, 0.40f, 0.32f); smooth = 0.1f; }

            if (StandIns.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = $"{GeneratedFolder}/{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smooth);
                mat.SetFloat("_Metallic", 0f);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            StandIns[key] = mat;
            return mat;
        }

        /// <summary>Scenery must not touch physics: remove any collider the prefab brings.</summary>
        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        }

        static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = go.name.StartsWith("UNS_Spruce") ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static Bounds CombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Transform Group(GameObject parent, string name) => Group(parent.transform, name);

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static IEnumerable<GameObject> SceneRoots() => EditorSceneManager.GetActiveScene().GetRootGameObjects();

        static void HideKitTestVisuals(StringBuilder sb)
        {
            var tv = Object.FindAnyObjectByType<GameplayKitTestVisuals>();
            if (tv == null || !tv.ShowTestVisuals) return;
            Undo.RecordObject(tv, "Hide kit test visuals");
            tv.ShowTestVisuals = false;
            tv.Apply();
            EditorUtility.SetDirty(tv);
            sb.AppendLine("kit test overlay hidden (ShowTestVisuals off) so it does not draw over the soil");
        }
    }
}
#endif
