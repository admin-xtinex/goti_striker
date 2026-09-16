#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Builds the map-independent gameplay prefab from the original kit:
    /// strips the brown ground mesh and edge rails, replaces them with an invisible tiled
    /// collision surface that leaves the pit basins clear, adds boundary walls, and adds
    /// toggleable test-only visuals. The source prefab is left untouched.
    /// </summary>
    public static class BuildMapReadyKit
    {
        const string SourcePrefab = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string OutPrefab = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit_MapReady.prefab";
        const string MatDir = "Assets/_Project/GameplayKit/Materials";
        const string Result = "Library/GameplayKit_MapReady.result";

        // Lane geometry — must match GameplayOrigin pit locals (3.0 / 16.5 / 31.0).
        // Z range deliberately covers MarbleController's own hardcoded clamp (minZ -8.5,
        // maxZ 37.5). The old ground stopped at 34, so the script could pin a marble
        // between 34 and 37.5 with no floor under it — it then fell and was respawned.
        // X half-width 10 comfortably covers the script's tapering ±5.15..±6.65 corridor.
        const float HalfWidth = 10f;
        const float ZMin = -8.5f;
        const float ZMax = 37.5f;
        const float SurfaceTopY = 0f;
        const float SurfaceThickness = 0.5f;

        // Pit rim radius is 0.52 (PitZone). A square gap of half-size 0.36 is fully inside
        // that disc (0.36 * sqrt2 = 0.509 < 0.52), so the saucer mesh always floors the gap
        // and no marble can drop past it, while a 0.72 m opening easily admits a 0.32 m marble.
        const float PitGapHalf = 0.36f;
        static readonly float[] PitZ = { 3.0f, 16.5f, 31.0f };

        const float WallHeight = 1.5f;
        const float WallThickness = 0.3f;

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Build Map-Ready Kit")]
        public static void BuildMenu() => Build(false);

        public static void BuildBatch()
        {
            try { Build(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Build(bool batch)
        {
            var sb = new StringBuilder();
            int ground = Mathf.Max(0, LayerMask.NameToLayer("GameplayGround"));
            int obstacle = Mathf.Max(0, LayerMask.NameToLayer("Obstacle"));

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (src == null) throw new System.Exception("Missing source prefab " + SourcePrefab);

            var kit = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(kit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            kit.name = "PitStriker_GameplayKit_MapReady";
            kit.transform.position = Vector3.zero;

            // ---------- groups ----------
            var gameplay = NewChild(kit.transform, "Gameplay");
            var pitGroup = NewChild(kit.transform, "PitCollidersAndDetection");
            var surfaceGroup = NewChild(kit.transform, "SurfaceCollision");
            var boundaryGroup = NewChild(kit.transform, "BoundaryCollision");
            var testGroup = NewChild(kit.transform, "TestVisuals");

            // ---------- move gameplay content ----------
            foreach (string n in new[] { "GameplayOrigin", "ShotModeConfig", "Marbles", "SpawnPoints",
                                         "LaunchSystem", "TurnSystem", "DetectionZones", "Cameras",
                                         "Input", "HUD", "Audio", "VFX", "SafetyRespawn", "SurfaceGroundConfig" })
            {
                var t = kit.transform.Find(n);
                if (t != null) t.SetParent(gameplay, true);
                else sb.AppendLine($"- NOTE: '{n}' not found in source");
            }

            // pits keep world position
            var pits = kit.transform.Find("Pits");
            if (pits != null)
            {
                for (int i = pits.childCount - 1; i >= 0; i--)
                    pits.GetChild(i).SetParent(pitGroup, true);
                Object.DestroyImmediate(pits.gameObject);
            }

            // ---------- drop the old visual ground + rails ----------
            var oldSurface = kit.transform.Find("GameplaySurface");
            if (oldSurface != null)
            {
                sb.AppendLine("- Removed GameplaySurface (brown ground mesh 'GameplayLane_WithPitHoles', "
                            + "material M_Kit_Surface_Soil, 4 edge rails, and the sealing 20x0.5x42 BoxCollider)");
                Object.DestroyImmediate(oldSurface.gameObject);
            }

            // ---------- invisible tiled collision surface (gaps at pit openings) ----------
            float cy = SurfaceTopY - SurfaceThickness * 0.5f;
            float[] cuts = new float[PitZ.Length * 2 + 2];
            cuts[0] = ZMin;
            for (int i = 0; i < PitZ.Length; i++)
            {
                cuts[1 + i * 2] = PitZ[i] - PitGapHalf;
                cuts[2 + i * 2] = PitZ[i] + PitGapHalf;
            }
            cuts[cuts.Length - 1] = ZMax;

            int seg = 0;
            for (int i = 0; i + 1 < cuts.Length; i += 2) // full-width spans between pit bands
            {
                float z0 = cuts[i], z1 = cuts[i + 1];
                if (z1 - z0 <= 0.001f) continue;
                AddBox(surfaceGroup, $"Surface_Span_{++seg}",
                    new Vector3(0f, cy, (z0 + z1) * 0.5f),
                    new Vector3(HalfWidth * 2f, SurfaceThickness, z1 - z0), ground);
            }

            float sideWidth = HalfWidth - PitGapHalf;
            float sideCenter = (HalfWidth + PitGapHalf) * 0.5f;
            for (int i = 0; i < PitZ.Length; i++) // side strips level with each pit
            {
                AddBox(surfaceGroup, $"Surface_PitBand_{i + 1}_L",
                    new Vector3(-sideCenter, cy, PitZ[i]),
                    new Vector3(sideWidth, SurfaceThickness, PitGapHalf * 2f), ground);
                AddBox(surfaceGroup, $"Surface_PitBand_{i + 1}_R",
                    new Vector3(sideCenter, cy, PitZ[i]),
                    new Vector3(sideWidth, SurfaceThickness, PitGapHalf * 2f), ground);
            }
            sb.AppendLine($"- SurfaceCollision: {surfaceGroup.childCount} boxes, top y={SurfaceTopY}, "
                        + $"{PitZ.Length} openings of {PitGapHalf * 2f:F2} m left clear");

            // ---------- boundary walls (always solid, never rendered) ----------
            float zc = (ZMin + ZMax) * 0.5f, zl = ZMax - ZMin;
            float wy = SurfaceTopY + WallHeight * 0.5f;
            AddBox(boundaryGroup, "Wall_Left", new Vector3(-HalfWidth - WallThickness * 0.5f, wy, zc),
                new Vector3(WallThickness, WallHeight, zl + WallThickness * 2f), obstacle);
            AddBox(boundaryGroup, "Wall_Right", new Vector3(HalfWidth + WallThickness * 0.5f, wy, zc),
                new Vector3(WallThickness, WallHeight, zl + WallThickness * 2f), obstacle);
            AddBox(boundaryGroup, "Wall_Near", new Vector3(0f, wy, ZMin - WallThickness * 0.5f),
                new Vector3(HalfWidth * 2f, WallHeight, WallThickness), obstacle);
            AddBox(boundaryGroup, "Wall_Far", new Vector3(0f, wy, ZMax + WallThickness * 0.5f),
                new Vector3(HalfWidth * 2f, WallHeight, WallThickness), obstacle);
            sb.AppendLine($"- BoundaryCollision: 4 walls, height {WallHeight} m");

            // ---------- test visuals (render-only, zero colliders) ----------
            var matOverlay = MakeTransparent("M_Kit_Test_SurfaceOverlay", new Color(0.35f, 0.75f, 1f, 0.10f));
            var matBoundary = MakeTransparent("M_Kit_Test_Boundary", new Color(1f, 0.62f, 0.15f, 0.40f));
            var matPit = MakeTransparent("M_Kit_Test_PitMarker", new Color(0.30f, 1f, 0.45f, 0.45f));

            // Overlay sits 12 mm above the collision top so it never z-fights a map surface at y=0.
            var overlay = NewChild(testGroup, "SurfaceOverlay");
            var quad = MakeVisual(PrimitiveType.Quad, overlay, "PlayableArea", matOverlay);
            quad.localPosition = new Vector3(0f, SurfaceTopY + 0.012f, zc);
            quad.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.localScale = new Vector3(HalfWidth * 2f, zl, 1f);

            var outline = NewChild(testGroup, "BoundaryOutline");
            MakeRail(outline, "Rail_Left", matBoundary, new Vector3(-HalfWidth, SurfaceTopY + 0.03f, zc), new Vector3(0.12f, 0.02f, zl));
            MakeRail(outline, "Rail_Right", matBoundary, new Vector3(HalfWidth, SurfaceTopY + 0.03f, zc), new Vector3(0.12f, 0.02f, zl));
            MakeRail(outline, "Rail_Near", matBoundary, new Vector3(0f, SurfaceTopY + 0.03f, ZMin), new Vector3(HalfWidth * 2f, 0.02f, 0.12f));
            MakeRail(outline, "Rail_Far", matBoundary, new Vector3(0f, SurfaceTopY + 0.03f, ZMax), new Vector3(HalfWidth * 2f, 0.02f, 0.12f));

            var markers = NewChild(testGroup, "PitMarkers");
            for (int i = 0; i < PitZ.Length; i++)
            {
                var disc = MakeVisual(PrimitiveType.Cylinder, markers, $"PitMarker_{i + 1}", matPit);
                disc.localPosition = new Vector3(0f, SurfaceTopY + 0.014f, PitZ[i]);
                disc.localScale = new Vector3(1.04f, 0.004f, 1.04f); // 0.52 m rim radius
            }
            sb.AppendLine("- TestVisuals: surface overlay + boundary outline + 3 pit markers (no colliders)");

            // ---------- wiring ----------
            var root = kit.GetComponent<GameplayKitRoot>();
            if (root != null)
            {
                root.GameplaySurface = surfaceGroup;
                root.Pits = pitGroup;
                root.Marbles = gameplay.Find("Marbles");
                root.Cameras = gameplay.Find("Cameras");
                root.HUD = gameplay.Find("HUD");
                root.Audio = gameplay.Find("Audio");
                root.Origin = kit.GetComponentInChildren<GameplayOrigin>(true);
                root.ShotConfig = kit.GetComponentInChildren<ShotModeConfig>(true);
                var ui = kit.GetComponentInChildren<PitStriker.GameplayKit.UI.ShotControlBinder>(true);
                if (ui != null) root.ShotControlUI = ui.transform;
            }

            var cfg = kit.GetComponentInChildren<SurfaceGroundConfig>(true);
            if (cfg != null)
            {
                cfg.VisualSurface = null;
                cfg.GameplaySurfaceRoot = null;   // no visual root to offset any more
                cfg.SurfaceCollisionRoot = surfaceGroup;
                cfg.BoundaryCollisionRoot = boundaryGroup;
                cfg.GameplayCollider = surfaceGroup.GetComponentInChildren<Collider>(true);
                cfg.ShowGameplaySurfaceVisual = false;
                cfg.EnableGameplaySurfaceCollider = true;
            }

            var tv = kit.GetComponent<GameplayKitTestVisuals>();
            if (tv == null) tv = kit.AddComponent<GameplayKitTestVisuals>();
            tv.SurfaceOverlay = overlay;
            tv.BoundaryOutline = outline;
            tv.PitMarkers = markers;
            tv.ShowTestVisuals = true;
            tv.Apply();

            // ---------- save ----------
            Directory.CreateDirectory(Path.GetDirectoryName(OutPrefab));
            PrefabUtility.SaveAsPrefabAsset(kit, OutPrefab);
            Object.DestroyImmediate(kit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine("- Saved " + OutPrefab);
            sb.AppendLine("BUILD_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[MAPREADY]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Map-Ready Kit", sb.ToString(), "OK");
        }

        // ---------- helpers ----------
        static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void AddBox(Transform parent, string name, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.layer = layer;
            go.AddComponent<BoxCollider>().size = size;
        }

        static Transform MakeVisual(PrimitiveType type, Transform parent, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        static void MakeRail(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
        {
            var t = MakeVisual(PrimitiveType.Cube, parent, name, mat);
            t.localPosition = pos;
            t.localScale = scale;
        }

        static Material MakeTransparent(string name, Color color)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
