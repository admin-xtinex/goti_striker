#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PitStriker.GameplayKit.EditorTools
{
    public static class TestA_NoGroundVerify
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string ScenePath = "Assets/_Project/GameplayKit/Demo/GameplayKit_TestA_NoGround.unity";
        const string ResultPath = "Library/GameplayKit_TestA.result";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Test A No-Ground Scene")]
        public static void CreateMenu() => CreateInternal(false);

        public static void CreateBatch()
        {
            try
            {
                CreateInternal(true);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                File.WriteAllText(ResultPath, "FAIL " + ex);
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void CreateInternal(bool writeResult)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Keep only light + camera from defaults — strip any Plane if present
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "Plane" || go.GetComponent<MeshCollider>() != null && go.name.Contains("Plane"))
                    Object.DestroyImmediate(go);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new System.Exception("Missing prefab " + PrefabPath);

            var kit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            kit.name = "PitStriker_GameplayKit";
            kit.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Structural asserts
            var sb = new StringBuilder();
            sb.AppendLine("EDITOR=6000.6.0f1");
            var colGo = kit.transform.Find("GameplaySurface/GameplayCollider");
            if (colGo == null)
            {
                // search
                foreach (var t in kit.GetComponentsInChildren<Transform>(true))
                    if (t.name == "GameplayCollider") { colGo = t; break; }
            }
            var box = colGo != null ? colGo.GetComponent<BoxCollider>() : null;
            sb.AppendLine("GameplayCollider=" + (colGo != null));
            sb.AppendLine("BoxCollider=" + (box != null));
            if (box != null)
            {
                sb.AppendLine("Enabled=" + box.enabled);
                sb.AppendLine("IsTrigger=" + box.isTrigger);
                sb.AppendLine("Size=" + box.size);
                sb.AppendLine("Center=" + box.center);
                sb.AppendLine("Layer=" + colGo.gameObject.layer + " (" + LayerMask.LayerToName(colGo.gameObject.layer) + ")");
            }
            // external ground check
            int meshCols = 0, planes = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == "Plane" || go.name.Contains("Verify_Plain")) planes++;
            }
            sb.AppendLine("ExternalPlanes=" + planes);
            sb.AppendLine("KitOnlyExpected=true");

            // pit locals
            foreach (var t in kit.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Pit_01") || t.name.Contains("Pit_02") || t.name.Contains("Pit_03"))
                    sb.AppendLine(t.name + " local=" + t.localPosition);
            }

            bool structOk = box != null && box.enabled && !box.isTrigger && planes == 0
                && colGo.gameObject.layer == LayerMask.NameToLayer("GameplayGround");
            // layer may be numeric 7 if name missing in batch early
            if (!structOk && box != null && box.enabled && !box.isTrigger && planes == 0 && colGo.gameObject.layer == 7)
                structOk = true;

            // Spawn coverage — the regression that produced the no-ground failure.
            // Test A has no host ground by design, so every spawn must fall inside the kit box.
            var kitRoot = kit.GetComponent<GameplayKitRoot>();
            bool spawnOk = false;
            if (kitRoot == null) sb.AppendLine("- GameplayKitRoot missing");
            else spawnOk = SpawnBoundsCheck.Evaluate(kitRoot, box, sb);
            sb.AppendLine("SpawnCoverage=" + (spawnOk ? "PASS" : "FAIL"));
            structOk = structOk && spawnOk;

            Directory.CreateDirectory("Assets/_Project/GameplayKit/Demo");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            sb.AppendLine("Scene=" + ScenePath);
            sb.AppendLine(structOk ? "STRUCTURAL_PASS" : "STRUCTURAL_FAIL");
            if (writeResult) File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log("[TESTA]\n" + sb);
        }
    }
}
#endif
