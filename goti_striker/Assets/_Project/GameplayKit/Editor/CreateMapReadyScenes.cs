#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Creates the two verification scenes: one bare (kit supplies all collision) and one
    /// with a stand-in map surface (kit surface authoritative via the host-map policy).
    /// </summary>
    public static class CreateMapReadyScenes
    {
        const string Prefab = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit_MapReady.prefab";
        const string CleanScene = "Assets/_Project/GameplayKit/Demo/GameplayKit_MapReady_Clean.unity";
        const string MapScene = "Assets/_Project/GameplayKit/Demo/GameplayKit_MapReady_WithMap.unity";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Create Map-Ready Test Scenes")]
        public static void CreateMenu() => Create(false);

        public static void CreateBatch()
        {
            try { Create(true); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        static void Create(bool batch)
        {
            Directory.CreateDirectory("Assets/_Project/GameplayKit/Demo");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) throw new System.Exception("Missing " + Prefab);

            // --- clean scene: no ground at all, kit must stand on its own ---
            var s1 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            StripPlane();
            StripDefaultCamera();
            var k1 = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            k1.transform.position = Vector3.zero;
            AddAutoStart(k1);
            EditorSceneManager.SaveScene(s1, CleanScene);

            // --- map scene: stand-in for a downloaded free-asset ground ---
            var s2 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            StripPlane();
            StripDefaultCamera();
            var map = GameObject.CreatePrimitive(PrimitiveType.Cube);
            map.name = "HostMap_Ground_Standin";
            map.transform.position = new Vector3(0f, -0.5f, 14f);
            map.transform.localScale = new Vector3(60f, 1f, 80f);

            var k2 = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            k2.transform.position = Vector3.zero;

            // kit surface is authoritative so the map floor cannot seal the pit basins
            var cfg = k2.GetComponentInChildren<SurfaceGroundConfig>(true);
            if (cfg != null)
            {
                cfg.IgnoreHostMapCollision = true;
                cfg.HostMapLayers = 1 << map.layer;
                EditorUtility.SetDirty(cfg);
            }
            AddAutoStart(k2);
            EditorSceneManager.SaveScene(s2, MapScene);

            AssetDatabase.SaveAssets();
            string msg = $"Created:\n{CleanScene}\n{MapScene}";
            File.WriteAllText("Library/GameplayKit_Scenes.result", msg);
            Debug.Log("[SCENES] " + msg);
            if (!batch) EditorUtility.DisplayDialog("Map-Ready Scenes", msg, "OK");
        }

        static void StripPlane()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "Plane") Object.DestroyImmediate(go);
        }

        /// The kit brings its own camera and AudioListener, so the default Main Camera
        /// would render instead of the follow camera and trigger "2 audio listeners".
        static void StripDefaultCamera()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "Main Camera") Object.DestroyImmediate(go);
        }

        /// TurnManager starts in Menu and blocks input until a menu starts the match.
        static void AddAutoStart(GameObject kit)
        {
            var go = new GameObject("TEST_AutoStartMatch");
            var auto = go.AddComponent<GameplayKitAutoStart>();
            auto.PlayerCount = 2;
            auto.IsAI = new bool[] { false, false };
        }
    }
}
#endif
