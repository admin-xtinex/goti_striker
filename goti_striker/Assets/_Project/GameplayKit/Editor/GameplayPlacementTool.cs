#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PitStriker.GameplayKit;
using PitStriker.Physics;

namespace PitStriker.GameplayKit.EditorTools
{
    public class GameplayPlacementTool : EditorWindow
    {
        const string PrefabPath = "Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab";
        float _offset = 0f;
        float _maxSlope = 8f;

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Open Placement Window")]
        public static void Open() => GetWindow<GameplayPlacementTool>("Gameplay Placement");

        [MenuItem("Tools/Pit Striker/Gameplay Placement/Create Gameplay Module")]
        public static void CreateModule()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("GameplayKit", "Prefab missing — run Build GameplayKit Prefab first.", "OK");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = GameplayKitRoot.PrefabName;
            Selection.activeGameObject = inst;
            Undo.RegisterCreatedObjectUndo(inst, "Create Gameplay Module");
        }

        void OnGUI()
        {
            GUILayout.Label("Pit Striker — Gameplay Placement", EditorStyles.boldLabel);
            _offset = EditorGUILayout.FloatField("Surface height offset", _offset);
            _maxSlope = EditorGUILayout.FloatField("Max slope (deg)", _maxSlope);

            if (GUILayout.Button("Create Gameplay Module"))
                CreateModule();

            if (GUILayout.Button("Align to Selected Surface"))
                AlignToSelected();

            if (GUILayout.Button("Validate Placement"))
                Validate();

            if (GUILayout.Button("Reset Local Alignment"))
                ResetLocal();
        }

        void AlignToSelected()
        {
            var kit = Object.FindAnyObjectByType<GameplayKitRoot>();
            if (kit == null) { EditorUtility.DisplayDialog("GameplayKit", "No kit in scene.", "OK"); return; }
            var sel = Selection.activeTransform;
            if (sel == null) { EditorUtility.DisplayDialog("GameplayKit", "Select a surface collider/mesh.", "OK"); return; }

            var col = sel.GetComponent<Collider>();
            Vector3 point = sel.position;
            Vector3 normal = Vector3.up;
            if (col != null)
            {
                point = col.ClosestPoint(kit.transform.position + Vector3.up * 50f);
                // raycast down
                if (UnityEngine.Physics.Raycast(kit.transform.position + Vector3.up * 20f, Vector3.down, out var hit, 100f))
                {
                    point = hit.point;
                    normal = hit.normal;
                }
            }
            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > _maxSlope)
                Debug.LogWarning($"[GameplayKit] Slope {slope:F1}° exceeds max {_maxSlope}°.");

            Undo.RecordObject(kit.transform, "Align GameplayKit");
            kit.transform.position = point + Vector3.up * _offset;
            kit.SurfaceHeightOffset = _offset;
            kit.ApplySurfaceOffset();
        }

        
        void Validate()
        {
            ValidateGroundDetailed();
        }

        void ValidateGroundDetailed()
        {
            var kit = Object.FindAnyObjectByType<GameplayKitRoot>();
            if (kit == null) { Debug.LogError("[GameplayKit] INVALID — no kit in scene."); return; }

            var cfg = kit.GetComponentInChildren<SurfaceGroundConfig>(true);
            var col = cfg != null ? cfg.GameplayCollider : null;
            if (col == null)
            {
                var t = kit.transform.Find("GameplaySurface/GameplayCollider");
                if (t != null) col = t.GetComponent<Collider>();
            }

            string verdict = "VALID";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // A missing/disabled kit collider is legal when the host map supplies the play surface.
            // Spawn coverage below decides — it accepts host ground under every spawn point.
            if (col == null) { verdict = "WARNING"; sb.AppendLine("- No GameplayCollider — relying on host map ground"); }
            else
            {
                if (!col.enabled) { verdict = "WARNING"; sb.AppendLine("- GameplayCollider disabled — relying on host map ground"); }
                if (col.isTrigger) { verdict = "INVALID"; sb.AppendLine("- GameplayCollider is Trigger"); }
                if (!(col is BoxCollider) && !(col is TerrainCollider))
                    sb.AppendLine("- WARNING: prefer BoxCollider for flat lane");
                if (col is MeshCollider mc && !mc.convex)
                { verdict = "INVALID"; sb.AppendLine("- Non-convex MeshCollider (dynamic RB will fall through)"); }
                int ground = LayerMask.NameToLayer("GameplayGround");
                if (ground >= 0 && col.gameObject.layer != ground)
                { if (verdict != "INVALID") verdict = "WARNING"; sb.AppendLine("- GameplayCollider not on GameplayGround layer"); }
            }

            if (kit.transform.localScale != Vector3.one)
            { verdict = "INVALID"; sb.AppendLine("- Kit root scale is not (1,1,1)"); }

            int marble = LayerMask.NameToLayer("Marble");
            int groundL = LayerMask.NameToLayer("GameplayGround");
            if (marble >= 0 && groundL >= 0 && UnityEngine.Physics.GetIgnoreLayerCollision(marble, groundL))
            { verdict = "INVALID"; sb.AppendLine("- Marble↔GameplayGround collision DISABLED in matrix"); }

            sb.AppendLine("Spawn coverage:");
            if (!SpawnBoundsCheck.Evaluate(kit, col, sb))
                verdict = "INVALID";

            Debug.Log($"[GameplayKit Validate] {verdict}\n{sb}");
            EditorUtility.DisplayDialog("GameplayKit Validate", verdict + "\n\n" + sb, "OK");
        }
        void ResetLocal()
        {
            var kit = Object.FindAnyObjectByType<GameplayKitRoot>();
            if (kit == null) return;
            Undo.RecordObject(kit.transform, "Reset Kit Alignment");
            kit.transform.localPosition = Vector3.zero;
            kit.transform.localRotation = Quaternion.identity;
            kit.SurfaceHeightOffset = 0f;
            kit.ApplySurfaceOffset();
        }
    }
}
#endif






