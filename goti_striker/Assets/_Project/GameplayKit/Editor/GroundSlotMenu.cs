#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Makes a ground model the gameplay kit's ground: it goes into the kit prefab's GroundVisual
    /// slot (created on first use), replacing the previous ground there, and GroundWithPitHoles cuts
    /// the pit holes into it. To change soil or ground later, select the new model and run this again.
    /// </summary>
    public static class GroundSlotMenu
    {
        [MenuItem("Pit Striker/Ground/Use Selected As Gameplay Ground")]
        public static void UseSelected()
        {
            var ground = Selection.activeGameObject;
            if (ground == null || !ground.scene.IsValid())
            {
                EditorUtility.DisplayDialog("Gameplay Ground", "Drag the ground model into the scene, select it in the Hierarchy, then run this.", "OK");
                return;
            }
            var kit = Object.FindAnyObjectByType<GameplayKitRoot>();
            if (kit == null)
            {
                EditorUtility.DisplayDialog("Gameplay Ground", "No gameplay kit (GameplayKitRoot) in the open scene.", "OK");
                return;
            }
            if (ground.transform.IsChildOf(kit.transform))
            {
                EditorUtility.DisplayDialog("Gameplay Ground", "Select a ground model that is not already part of the gameplay kit.", "OK");
                return;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(kit.gameObject);
            bool kitIsPrefab = !string.IsNullOrEmpty(prefabPath);

            // 1. Colliders on the model: the kit already provides the playing surface and pit colliders.
            var colliders = ground.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToList();
            if (colliders.Count > 0 && EditorUtility.DisplayDialog("Gameplay Ground",
                    $"'{ground.name}' has {colliders.Count} collider(s). The gameplay kit already provides the playing " +
                    "surface and the pit colliders; a collider on the ground model covers the pits and can stop " +
                    "marbles dropping in.\n\nDisable the model's colliders?", "Disable (recommended)", "Keep them"))
            {
                foreach (var c in colliders) { Undo.RecordObject(c, "Disable ground collider"); c.enabled = false; }
            }

            // 2. The slot, created in the kit prefab on first use.
            var slot = kit.transform.Find(GroundWithPitHoles.SlotName);
            if (slot == null)
            {
                if (kitIsPrefab)
                {
                    var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                    var s = new GameObject(GroundWithPitHoles.SlotName);
                    s.transform.SetParent(contents.transform, false);
                    s.AddComponent<GroundWithPitHoles>();
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                    slot = kit.transform.Find(GroundWithPitHoles.SlotName);
                }
                else
                {
                    var s = new GameObject(GroundWithPitHoles.SlotName);
                    Undo.RegisterCreatedObjectUndo(s, "Create ground slot");
                    s.transform.SetParent(kit.transform, false);
                    s.AddComponent<GroundWithPitHoles>();
                    slot = s.transform;
                }
            }
            if (slot == null) { EditorUtility.DisplayDialog("Gameplay Ground", "Could not create the ground slot in the kit.", "OK"); return; }

            // 3. Replace whatever ground was there.
            var previous = new List<Transform>();
            foreach (Transform child in slot) previous.Add(child);
            if (previous.Count > 0)
            {
                string names = string.Join(", ", previous.Select(p => p.name));
                if (!EditorUtility.DisplayDialog("Gameplay Ground", $"Replace the current ground ({names}) with '{ground.name}'?", "Replace", "Cancel"))
                    return;
                if (kitIsPrefab)
                {
                    var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                    var contentSlot = contents.transform.Find(GroundWithPitHoles.SlotName);
                    if (contentSlot != null)
                        for (int i = contentSlot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(contentSlot.GetChild(i).gameObject);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                foreach (var p in previous) if (p != null) Undo.DestroyObjectImmediate(p.gameObject);   // scene-only leftovers
            }

            // 4. Move the model in, keeping its world placement, and store it in the kit prefab.
            Undo.SetTransformParent(ground.transform, slot, "Use as gameplay ground");
            if (kitIsPrefab) PrefabUtility.ApplyAddedGameObject(ground, prefabPath, InteractionMode.UserAction);

            // 5. Cut the pit holes now rather than on the next editor tick.
            string report = slot.GetComponent<GroundWithPitHoles>().Refresh(force: true);
            EditorSceneManager.MarkSceneDirty(kit.gameObject.scene);
            EditorUtility.DisplayDialog("Gameplay Ground",
                $"'{ground.name}' is now the gameplay ground{(kitIsPrefab ? " in " + prefabPath : "")}.\n\n{report}\n\nSave the scene.", "OK");
        }

        [MenuItem("Pit Striker/Ground/Re-cut Pit Holes")]
        public static void Recut()
        {
            var slots = Object.FindObjectsByType<GroundWithPitHoles>(FindObjectsInactive.Exclude);
            if (slots.Length == 0)
            {
                EditorUtility.DisplayDialog("Gameplay Ground", "No ground slot yet. Use 'Use Selected As Gameplay Ground' first.", "OK");
                return;
            }
            EditorUtility.DisplayDialog("Gameplay Ground", string.Join("\n", slots.Select(s => s.Refresh(force: true))), "OK");
        }
    }
}
#endif
