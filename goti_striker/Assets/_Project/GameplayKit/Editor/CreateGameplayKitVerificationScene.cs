#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PitStriker.GameplayKit.EditorTools
{
    public static class CreateGameplayKitVerificationScene
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string ScenePath = "Assets/_Project/GameplayKit/Demo/GameplayKit_Verification.unity";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Create Verification Scene")]
        public static void Create()
        {
            CreateInternal();
        }

        public static void CreateBatch()
        {
            try
            {
                CreateInternal();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        static void CreateInternal()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Remove default Main Camera and Light we'll keep light; replace camera optional
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Verify_PlainSurface";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80x80m-ish

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new System.Exception("Missing prefab at " + PrefabPath);

            var kit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            kit.name = "PitStriker_GameplayKit";
            kit.transform.position = new Vector3(0f, 0.02f, 0f);

            // Validate pit locals
            var pits = kit.GetComponentsInChildren<Transform>(true);
            foreach (var t in pits)
            {
                if (t.name.Contains("Pit_01")) Debug.Log($"[VERIFY] {t.name} local={t.localPosition}");
                if (t.name.Contains("Pit_02")) Debug.Log($"[VERIFY] {t.name} local={t.localPosition}");
                if (t.name.Contains("Pit_03")) Debug.Log($"[VERIFY] {t.name} local={t.localPosition}");
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/GameplayKit/Demo");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[VERIFY] Saved " + ScenePath);
            System.IO.File.WriteAllText("Library/GameplayKit_Verification.result", "SUCCESS " + ScenePath);
        }
    }
}
#endif
