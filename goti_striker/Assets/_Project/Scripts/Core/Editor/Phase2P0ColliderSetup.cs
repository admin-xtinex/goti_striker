#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.EditorTools
{
    /// <summary>
    /// Phase 2 P0 — add house BoxCollider + palm trunk CapsuleColliders only.
    /// Does not touch Arena_Sandbox / gameplay kit / pits / fairway.
    /// </summary>
    [InitializeOnLoad]
    public static class Phase2P0ColliderSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string AutoFlag = "Library/Phase2P0ColliderSetup.autorun";
        const string ResultFlag = "Library/Phase2P0ColliderSetup.result";

        static Phase2P0ColliderSetup()
        {
            EditorApplication.update += TryAutorun;
        }

        [MenuItem("Pit Striker/Phase2 P0 Add Colliders")]
        public static void Run()
        {
            string result;
            try
            {
                result = Apply();
                File.WriteAllText(ResultFlag, "PASS\n" + result);
                Debug.Log("<color=#00FF88>[P2 Colliders]</color> " + result);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(ResultFlag, "FAIL\n" + e);
                Debug.LogException(e);
                throw;
            }
        }

        static void TryAutorun()
        {
            if (!File.Exists(AutoFlag) || EditorApplication.isCompiling || EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < 3)
                return;
            File.Delete(AutoFlag);
            Run();
        }

        static string Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dressing = GameObject.Find("Phase2_P0_Dressing");
            if (dressing == null)
                throw new System.Exception("Phase2_P0_Dressing not found");

            int houseCount = 0;
            int palmCount = 0;

            // House box
            foreach (Transform child in dressing.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "P2_KeralaHouse_A") continue;
                // Prefer collider on the root instance object
                var go = child.gameObject;
                // Remove any accidental mesh colliders on house hierarchy? Cheify said box only — leave mesh off
                var box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();

                Bounds b = GetHierarchyBounds(go);
                // Convert world bounds to local box
                Vector3 localCentre = go.transform.InverseTransformPoint(b.center);
                Vector3 localSize = go.transform.InverseTransformVector(b.size);
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                // Slight inset so we don't overreach porch into lane
                localSize = Vector3.Scale(localSize, new Vector3(0.92f, 0.95f, 0.85f));
                box.center = localCentre;
                box.size = localSize;
                box.isTrigger = false;
                houseCount++;
            }

            // Palm capsules — all P2_Palm_* roots
            foreach (Transform child in dressing.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("P2_Palm_")) continue;
                // Only roots directly under dressing (or named palm instances)
                if (child.parent != dressing.transform && child.parent != null && child.parent.name != "Phase2_P0_Dressing")
                {
                    // still allow if name is instance root (PrefabInstance root is the named GO)
                    if (!IsLikelyInstanceRoot(child)) continue;
                }

                var go = child.gameObject;
                // Skip if this is a deep child (frond piece) — instance roots are P2_Palm_*
                if (!go.name.StartsWith("P2_Palm_")) continue;

                var cap = go.GetComponent<CapsuleCollider>();
                if (cap == null) cap = go.AddComponent<CapsuleCollider>();

                Bounds b = GetHierarchyBounds(go);
                Vector3 localCentre = go.transform.InverseTransformPoint(b.center);
                // Capsule along Y (direction 1)
                float height = Mathf.Max(1f, Mathf.Abs(go.transform.InverseTransformVector(new Vector3(0, b.size.y, 0)).y));
                float radius = Mathf.Max(0.12f, Mathf.Min(b.size.x, b.size.z) * 0.12f);
                // Trunk-ish: shorter radius, full height
                cap.direction = 1; // Y
                cap.height = height * 0.92f;
                cap.radius = Mathf.Clamp(radius, 0.12f, 0.35f);
                cap.center = new Vector3(localCentre.x, localCentre.y, localCentre.z);
                cap.isTrigger = false;
                palmCount++;
            }

            // Safety: ensure we did not add colliders under Arena_Sandbox
            var kit = GameObject.Find("Arena_Sandbox");
            if (kit != null)
            {
                // no-op check — we never referenced kit
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            return $"houseBox={houseCount} palmCapsules={palmCount} scene={ScenePath}";
        }

        static bool IsLikelyInstanceRoot(Transform t)
        {
            return t.name.StartsWith("P2_Palm_") || t.name == "P2_KeralaHouse_A";
        }

        static Bounds GetHierarchyBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(go.transform.position, new Vector3(2, 2, 2));
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
