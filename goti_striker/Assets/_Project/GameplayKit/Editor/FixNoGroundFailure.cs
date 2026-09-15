#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitStriker.GameplayKit;
using PitStriker.Physics;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Fixes empty-scene fall-through: replace non-convex MeshCollider with BoxCollider,
    /// split visual/physics, layers, safe spawn heights.
    /// </summary>
    public static class FixNoGroundFailure
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const int LayerMarble = 6;
        const int LayerGround = 7;
        const int LayerObstacle = 8;
        const int LayerPitTrigger = 9;


        public static void FixSilent()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[GameplayKit] No-ground failure fix saved to prefab (silent).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        public static void FixBatch()
        {
            try
            {
                FixSilent();
                System.IO.File.WriteAllText("Library/GameplayKit_NoGround_Reapply.result", "OK");
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("Library/GameplayKit_NoGround_Reapply.result", "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Fix No-Ground Failure")]
        public static void Fix()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[GameplayKit] No-ground failure fix saved to prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("GameplayKit", "No-ground fix applied to prefab.\nRe-open GameplayKit_Verification and Play (empty scene / no plane).", "OK");
        }

        public static void Apply(GameObject root)
        {
            EnsureLayersExist();

            var surfaceRoot = root.transform.Find("GameplaySurface");
            if (surfaceRoot == null)
            {
                var go = new GameObject("GameplaySurface");
                go.transform.SetParent(root.transform, false);
                surfaceRoot = go.transform;
            }

            // Find or create VisualSurface (GameplayLane mesh)
            Transform visual = surfaceRoot.Find("VisualSurface");
            var lane = surfaceRoot.Find("GameplayLane");
            if (visual == null && lane != null)
            {
                lane.name = "VisualSurface";
                visual = lane;
            }
            if (visual == null)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "VisualSurface";
                plane.transform.SetParent(surfaceRoot, false);
                plane.transform.localPosition = new Vector3(0f, 0.01f, 17f);
                plane.transform.localScale = new Vector3(2f, 1f, 3.4f);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                visual = plane.transform;
            }

            // Strip ALL colliders from visual (mesh must not be the physics surface)
            foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);

            // GameplayCollider child with BoxCollider
            Transform colT = surfaceRoot.Find("GameplayCollider");
            GameObject colGo;
            if (colT == null)
            {
                colGo = new GameObject("GameplayCollider");
                colGo.transform.SetParent(surfaceRoot, false);
            }
            else colGo = colT.gameObject;

            // Cover spawn tee (z~-6) through far pit (z=31). Old box at z=17 size 34 only covered 0..34 — spawn fell through.
            const float laneMinZ = -8f;
            const float laneMaxZ = 34f;
            float laneLen = laneMaxZ - laneMinZ; // 42
            float laneMidZ = (laneMinZ + laneMaxZ) * 0.5f; // 13
            colGo.transform.localPosition = new Vector3(0f, 0f, laneMidZ);
            colGo.transform.localRotation = Quaternion.identity;
            colGo.transform.localScale = Vector3.one;
            colGo.layer = LayerGround;
            colGo.isStatic = true;

            // Remove mesh colliders if any
            foreach (var c in colGo.GetComponents<Collider>())
                Object.DestroyImmediate(c);

            var box = colGo.AddComponent<BoxCollider>();
            box.enabled = true;
            box.isTrigger = false;
            // ~20 wide, full tee to pit length, thickness 0.5, top at local y=0
            box.size = new Vector3(20f, 0.5f, laneLen);
            box.center = new Vector3(0f, -0.25f, 0f); // top face at y=0 in collider local

            visual.gameObject.layer = LayerGround; // visual only; no collider

            // SurfaceGroundConfig
            var cfg = root.GetComponentInChildren<SurfaceGroundConfig>(true);
            if (cfg == null)
            {
                var cfgGo = new GameObject("SurfaceGroundConfig");
                cfgGo.transform.SetParent(root.transform, false);
                cfg = cfgGo.AddComponent<SurfaceGroundConfig>();
            }
            cfg.VisualSurface = visual;
            cfg.GameplayCollider = box;
            cfg.GameplaySurfaceRoot = surfaceRoot;
            cfg.ShowGameplaySurfaceVisual = true;
            cfg.EnableGameplaySurfaceCollider = true;
            cfg.SpawnSafetyOffset = 0.05f;
            cfg.Apply();

            // Origin start height
            var origin = root.GetComponentInChildren<GameplayOrigin>(true);
            if (origin != null)
            {
                origin.StartCenterLocal = new Vector3(0f, 0.30f, -6f);
                origin.TeeAfterPit1Local = new Vector3(0f, 0.30f, 4.5f);
                origin.TeeAfterPit2Local = new Vector3(0f, 0.30f, 18f);
            }

            // Marbles: layer + safe Y + safety guard
            foreach (var mc in root.GetComponentsInChildren<MarbleController>(true))
            {
                var go = mc.gameObject;
                go.layer = LayerMarble;
                float r = 0.25f;
                var sphere = go.GetComponent<SphereCollider>();
                if (sphere != null) r = sphere.radius * Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.y, go.transform.lossyScale.z);

                var lp = go.transform.localPosition;
                // Approximate: surface top ~0 under kit at identity; spawn = r + offset
                lp.y = r + cfg.SpawnSafetyOffset;
                go.transform.localPosition = lp;

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }

                if (go.GetComponent<MarbleSafetyGuard>() == null)
                    go.AddComponent<MarbleSafetyGuard>();
            }

            // Pits: mesh stay Default/ground; trigger spheres → PitTrigger
            foreach (var pit in root.GetComponentsInChildren<Transform>(true))
            {
                if (!pit.name.StartsWith("Pit_")) continue;
                pit.gameObject.layer = LayerGround;
                foreach (var sc in pit.GetComponents<SphereCollider>())
                {
                    if (sc.isTrigger)
                        pit.gameObject.layer = LayerPitTrigger; // whole pit go — better set trigger child
                }
            }
            // More precise: any trigger collider object under pits
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger && col.transform.GetComponentInParent<PitStriker.Gameplay.PitZone>() != null)
                    col.gameObject.layer = LayerPitTrigger;
            }

            // Kit root scale identity
            root.transform.localScale = Vector3.one;

            UnityEngine.Physics.IgnoreLayerCollision(LayerMarble, LayerGround, false);
            UnityEngine.Physics.IgnoreLayerCollision(LayerMarble, LayerMarble, false);
            UnityEngine.Physics.IgnoreLayerCollision(LayerMarble, LayerObstacle, false);
            // PitTrigger stays as trigger — collision matrix still allows overlap queries
        }

        static void EnsureLayersExist()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            void Set(int idx, string name)
            {
                var sp = layers.GetArrayElementAtIndex(idx);
                if (sp != null && sp.stringValue != name)
                    sp.stringValue = name;
            }
            Set(LayerMarble, "Marble");
            Set(LayerGround, "GameplayGround");
            Set(LayerObstacle, "Obstacle");
            Set(LayerPitTrigger, "PitTrigger");
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif


