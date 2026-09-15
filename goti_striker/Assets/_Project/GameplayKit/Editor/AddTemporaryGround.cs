#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Adds a placeholder soil ground to the MAP SCENE — deliberately not to the gameplay
    /// prefab, which must stay map-independent. When the real map arrives you edit the scene
    /// and delete this group; the prefab is never touched.
    ///
    /// The lane reuses GameplayLane_WithPitHoles, the mesh that already has the three pit
    /// openings cut, so the pits stay visible. Flat aprons cover the tee and run-off areas
    /// beyond that mesh, which contain no pits and so need no holes.
    ///
    /// Purely visual: no colliders, so it cannot affect the validated physics.
    /// </summary>
    public static class AddTemporaryGround
    {
        const string Scene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string LaneMesh = "Assets/_Project/Art/Meshes/GameplayLane_WithPitHoles.asset";
        const string SoilMat = "Assets/_Project/GameplayKit/Materials/M_Kit_Surface_Soil.mat";
        const string GroupName = "TemporaryGround_PlaceholderMap";
        const string Result = "Library/TemporaryGround.result";

        // Must match BuildMapReadyKit: collision surface spans this range, top at y = 0.
        const float HalfWidth = 10f;
        const float ZMin = -8.5f;
        const float ZMax = 37.5f;
        // Sits exactly on the collision top so marbles rest on the soil rather than looking
        // sunk. Kit test visuals live at +0.012 and above, so nothing z-fights.
        const float GroundY = 0f;

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Add Temporary Soil Ground To Map")]
        public static void AddMenu() => Add(false);

        public static void AddBatch()
        {
            try { Add(true); EditorApplication.Exit(0); }
            catch (System.Exception ex)
            {
                File.WriteAllText(Result, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Add(bool batch)
        {
            var sb = new StringBuilder();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LaneMesh);
            var soil = AssetDatabase.LoadAssetAtPath<Material>(SoilMat);
            if (mesh == null) throw new System.Exception("missing " + LaneMesh);
            if (soil == null) throw new System.Exception("missing " + SoilMat);

            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            // Replace any previous placeholder so this is re-runnable.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != GroupName) continue;
                Object.DestroyImmediate(root);
                sb.AppendLine("removed previous placeholder group");
                break;
            }

            var group = new GameObject(GroupName);
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // 1. Lane with pit holes, at the transform the original village visual surface
            //    used, so the holes stay aligned with the pits at z 3.0 / 16.5 / 31.0.
            var lane = new GameObject("Ground_Lane_WithPitHoles");
            lane.transform.SetParent(group.transform, false);
            lane.transform.localPosition = new Vector3(0f, GroundY, 17f);
            lane.transform.localScale = new Vector3(2f, 1f, 3.4f);
            lane.AddComponent<MeshFilter>().sharedMesh = mesh;
            var laneRenderer = lane.AddComponent<MeshRenderer>();
            laneRenderer.sharedMaterial = soil;
            laneRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Measure what the lane mesh actually covers so the aprons meet it exactly
            // instead of relying on a hardcoded guess.
            Bounds b = laneRenderer.bounds;
            sb.AppendLine($"lane mesh covers z {b.min.z:F2}..{b.max.z:F2}, x {b.min.x:F2}..{b.max.x:F2}");

            // 2. Flat aprons for the tee and run-off. No pits there, so no holes needed.
            //    Slight overlap so no seam shows.
            if (b.min.z > ZMin + 0.05f)
                AddApron(group.transform, "Ground_Apron_Near", soil, ZMin, b.min.z + 0.05f, sb);
            if (b.max.z < ZMax - 0.05f)
                AddApron(group.transform, "Ground_Apron_Far", soil, b.max.z - 0.05f, ZMax, sb);

            // 3. Backing plane. The holes cut in the lane mesh are far wider than the 0.52 m
            //    pit saucer, so without this you see straight through the ground to the sky
            //    around each pit. Sits just below the saucer floor (-0.18) so the bowl still
            //    reads as a depression.
            AddApron(group.transform, "Ground_Backing_UnderPits", soil, ZMin, ZMax, sb, y: -0.26f);

            // A placeholder visual must never add physics.
            int colliders = group.GetComponentsInChildren<Collider>(true).Length;
            sb.AppendLine($"colliders inside placeholder = {colliders} (must be 0)");

            // The pit saucers ship with PitSoil, whose VillageGround shader was authored for
            // the village environment that no longer exists, so they render flat white against
            // the placeholder soil. Override the material on the SCENE INSTANCE only — a prefab
            // instance override lives in the scene, so the gameplay prefab is left untouched
            // and this disappears with the placeholder when the real map lands.
            int retinted = 0, skippedAssets = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var pit in root.GetComponentsInChildren<PitStriker.Gameplay.PitZone>(true))
                {
                    var mr = pit.GetComponent<MeshRenderer>();
                    if (mr == null) continue;
                    // Guard: never write to an object that lives in a prefab asset.
                    // FindObjectsByType(..Include..) returns those too, which would edit the
                    // prefab on disk instead of creating a scene-instance override.
                    if (EditorUtility.IsPersistent(mr)) { skippedAssets++; continue; }
                    mr.sharedMaterial = soil;
                    EditorUtility.SetDirty(mr);
                    retinted++;
                }
            }
            sb.AppendLine($"pit saucers retinted via scene-instance override = {retinted} "
                        + $"(prefab-asset objects skipped: {skippedAssets})");

            sb.AppendLine("\nscene roots:");
            foreach (var root in scene.GetRootGameObjects())
                sb.AppendLine($"  {root.name,-38} active={root.activeSelf}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sb.AppendLine("TEMP_GROUND_OK");
            File.WriteAllText(Result, sb.ToString());
            Debug.Log("[TEMPGROUND]\n" + sb);
            if (!batch) EditorUtility.DisplayDialog("Temporary Ground", sb.ToString(), "OK");
        }

        /// <summary>Flat soil quad spanning the given Z range across the full lane width.</summary>
        static void AddApron(Transform parent, string name, Material soil, float z0, float z1,
                             StringBuilder sb, float? y = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);

            float length = z1 - z0;
            go.transform.localPosition = new Vector3(0f, y ?? GroundY, (z0 + z1) * 0.5f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(HalfWidth * 2f, length, 1f);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = soil;
            r.shadowCastingMode = ShadowCastingMode.Off;
            sb.AppendLine($"  {name}: z {z0:F2}..{z1:F2} (length {length:F2})");
        }
    }
}
#endif
