#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PitStriker.GameplayKit;
using PitStriker.GameplayKit.UI;
using PitStriker.Gameplay;
using PitStriker.Physics;
using PitStriker.Input;
using PitStriker.CameraSystem;
using PitStriker.Audio;
using PitStriker.VFX;
using PitStriker.UI;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Builds PitStriker_GameplayKit prefab by DUPLICATING gameplay objects from the
    /// village reference scene — does not gut SC_Village_Graphics_Test.
    /// </summary>
    public static class BuildGameplayKitPrefab
    {
        const string VillageScene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        const string ShotUiPrefab = "Assets/_Project/GameplayKit/UI/Prefabs/UI_ShotControl.prefab";

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Build GameplayKit Prefab")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var village = EditorSceneManager.OpenScene(VillageScene, OpenSceneMode.Single);
            var arena = GameObject.Find("Arena_Sandbox");
            if (arena == null)
            {
                Debug.LogError("[GameplayKit] Arena_Sandbox not found in village scene.");
                return;
            }

            // Temp staging scene so we don't dirty village hierarchy permanently
            var stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var rootGo = new GameObject(GameplayKitRoot.PrefabName);
            var root = rootGo.AddComponent<GameplayKitRoot>();
            var originGo = new GameObject("GameplayOrigin");
            originGo.transform.SetParent(rootGo.transform, false);
            var origin = originGo.AddComponent<GameplayOrigin>();
            root.Origin = origin;

            var shotCfgGo = new GameObject("ShotModeConfig");
            shotCfgGo.transform.SetParent(rootGo.transform, false);
            root.ShotConfig = shotCfgGo.AddComponent<ShotModeConfig>();

            // Folders
            Transform Mk(string n)
            {
                var g = new GameObject(n);
                g.transform.SetParent(rootGo.transform, false);
                return g.transform;
            }

            var surfaceT = Mk("GameplaySurface");
            var pitsT = Mk("Pits");
            var marblesT = Mk("Marbles");
            var spawnT = Mk("SpawnPoints");
            var launchT = Mk("LaunchSystem");
            var turnT = Mk("TurnSystem");
            var detectT = Mk("DetectionZones");
            var camT = Mk("Cameras");
            var inputT = Mk("Input");
            var hudT = Mk("HUD");
            var audioT = Mk("Audio");
            var vfxT = Mk("VFX");
            var safetyT = Mk("SafetyRespawn");

            root.GameplaySurface = surfaceT;
            root.Pits = pitsT;
            root.Marbles = marblesT;
            root.Cameras = camT;
            root.HUD = hudT;
            root.Audio = audioT;

            // Clone pits + marbles from arena (keep world poses → then reparent under origin as local)
            foreach (var name in new[] { "Pit_01_Round", "Pit_02_Round", "Pit_03_Round" })
            {
                var src = FindChild(arena.transform, name);
                if (src == null) continue;
                var clone = Object.Instantiate(src.gameObject, pitsT);
                clone.name = name;
                // Keep world position then convert to local under root
                clone.transform.position = src.position;
                clone.transform.rotation = src.rotation;
                clone.transform.localScale = src.lossyScale;
            }

            foreach (Transform child in arena.transform)
            {
                if (!child.name.StartsWith("PlayerMarble_")) continue;
                var clone = Object.Instantiate(child.gameObject, marblesT);
                clone.name = child.name;
                clone.transform.position = child.position;
                clone.transform.rotation = child.rotation;
            }

            // Gameplay surface: prefer Foundation GameplayLane if present, else create plane
            var lane = GameObject.Find("GameplayLane");
            if (lane != null)
            {
                var laneClone = Object.Instantiate(lane, surfaceT);
                laneClone.name = "GameplayLane";
                laneClone.transform.position = lane.transform.position;
            }
            else
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "GameplayLane";
                plane.transform.SetParent(surfaceT, false);
                plane.transform.localPosition = new Vector3(0f, 0.01f, 17f);
                plane.transform.localScale = new Vector3(2.0f, 1f, 3.4f); // ~20x34
            }

            // Systems: clone TurnManager, managers, camera
            CloneRootIfExists("TurnManager", turnT);
            CloneRootIfExists("AudioManager", audioT);
            CloneRootIfExists("VFXManager", vfxT);

            var mainCam = Camera.main != null ? Camera.main.gameObject : GameObject.Find("Main Camera");
            if (mainCam != null)
            {
                var camClone = Object.Instantiate(mainCam, camT);
                camClone.name = "GameplayCamera";
                camClone.tag = "MainCamera";
            }

            // Swipe launch lives on marble often — ensure a kit-level controller exists
            if (Object.FindObjectsByType<SwipeLaunchController>(FindObjectsInactive.Include).Length == 0)
            {
                launchT.gameObject.AddComponent<SwipeLaunchController>();
            }
            else
            {
                // Prefer clone from active marble's component host if separate
                var any = Object.FindAnyObjectByType<SwipeLaunchController>();
                if (any != null && any.transform.root == arena.transform.root)
                {
                    // already on marble clones if component was on marble
                }
            }

            // HUD: EventSystem + Canvas if present
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null)
            {
                var esClone = Object.Instantiate(es.gameObject, hudT);
                esClone.name = "EventSystem";
            }
            var hud = Object.FindAnyObjectByType<HUDManager>();
            if (hud != null)
            {
                var hudClone = Object.Instantiate(hud.gameObject, hudT);
                hudClone.name = hud.gameObject.name;
            }

            // Shot control UI
            var shotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShotUiPrefab);
            if (shotPrefab != null)
            {
                var shot = (GameObject)PrefabUtility.InstantiatePrefab(shotPrefab);
                shot.transform.SetParent(hudT, false);
                shot.name = "UI_ShotControl";
                root.ShotControlUI = shot.transform;
                if (shot.GetComponent<ShotControlBinder>() == null)
                    shot.AddComponent<ShotControlBinder>();
                // Wire power area by name
                var binder = shot.GetComponent<ShotControlBinder>();
                var power = shot.transform.Find("PowerArea") as RectTransform;
                if (power != null) binder.PowerArea = power;
            }

            // Rebase all children so origin is at world 0 matching current layout
            // (pits already at z 3/16.5/31 world — keep as local under root at identity)
            rootGo.transform.position = Vector3.zero;
            rootGo.transform.rotation = Quaternion.identity;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PrefabPath)));
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);
            EditorSceneManager.CloseScene(stage, true);
            // Restore village
            EditorSceneManager.OpenScene(VillageScene, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log($"[GameplayKit] Prefab written: {PrefabPath}");
            EditorUtility.DisplayDialog("GameplayKit", "Prefab built:\n" + PrefabPath + "\n\nVillage scene left intact.", "OK");
        }

        static void CloneRootIfExists(string name, Transform parent)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            var clone = Object.Instantiate(go, parent);
            clone.name = name;
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindChild(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
#endif

