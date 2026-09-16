using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Ground slot of the gameplay kit. Put any ground model (soil, grass, a map's terrain mesh)
    /// under this object and, in the editor, it is given round holes wherever the pits are, so the
    /// pit bowls show through. Swap the model and the new one is cut automatically; move or resize
    /// a pit and the holes follow. Use Pit Striker > Ground > Use Selected As Gameplay Ground.
    ///
    /// How: each child MeshFilter's original mesh is remembered, a copy with the holes cut is baked
    /// into Assets/_Project/Art/Ground/Generated and assigned to the MeshFilter. The baked mesh is an
    /// ordinary asset, so builds just use it - nothing runs at play time.
    ///
    /// Visual only: colliders are never touched here, and the source model files are not modified.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GroundWithPitHoles : MonoBehaviour
    {
        public const string SlotName = "GroundVisual";
        public const string GeneratedFolder = "Assets/_Project/Art/Ground/Generated";

        [Serializable]
        class Entry
        {
            public MeshFilter Filter;
            public Mesh Source;       // the mesh as the model provided it
            public Mesh Cut;          // the baked copy currently assigned
            public string Signature;  // pits + placement the copy was cut for
        }

        [SerializeField] List<Entry> _entries = new List<Entry>();

#if UNITY_EDITOR
        double _nextCheck;

        void OnEnable() { EditorApplication.update -= EditorTick; EditorApplication.update += EditorTick; _nextCheck = 0; }
        void OnDisable() => EditorApplication.update -= EditorTick;
        void OnTransformChildrenChanged() => _nextCheck = 0;

        void EditorTick()
        {
            if (this == null) { EditorApplication.update -= EditorTick; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1.0;
            Refresh(force: false);
        }

        /// <summary>Cuts any child ground mesh that is new, swapped, or out of date with the pits.</summary>
        public string Refresh(bool force) => Refresh(force, applyToPrefab: true);

        /// <summary>
        /// As <see cref="Refresh(bool)"/>; <paramref name="applyToPrefab"/> false leaves prefab syncing
        /// to the caller (used while a ground is being added to the kit prefab).
        /// </summary>
        public string Refresh(bool force, bool applyToPrefab)
        {
            var kit = GetComponentInParent<GameplayKitRoot>();
            var holes = PitHoleMeshCutter.FindHoles(kit != null ? kit.transform : null);
            if (holes.Count == 0) return "No pits found.";

            _entries.RemoveAll(e => e == null || e.Filter == null);
            var report = new List<string>();
            bool changed = false;

            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var entry = _entries.Find(e => e.Filter == mf);
                if (entry == null) { entry = new Entry { Filter = mf, Source = mf.sharedMesh }; _entries.Add(entry); changed = true; }
                else if (mf.sharedMesh != entry.Cut && mf.sharedMesh != entry.Source) { entry.Source = mf.sharedMesh; entry.Cut = null; changed = true; }
                if (entry.Source == null) entry.Source = mf.sharedMesh;
                // Never cut a cut copy: trace a generated mesh back to the model's own mesh.
                entry.Source = OriginalMesh(mf, entry.Source);

                string signature = Signature(holes, mf.transform, entry.Source);
                if (!force && entry.Cut != null && mf.sharedMesh == entry.Cut && entry.Signature == signature) continue;

                Mesh cut;
                int dropped, clipped;
                try { cut = PitHoleMeshCutter.Cut(entry.Source, mf.transform, holes, out dropped, out clipped); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GROUND] Could not read '{entry.Source.name}' to cut pit holes: {ex.Message}");
                    entry.Cut = mf.sharedMesh; entry.Signature = signature;
                    continue;
                }

                Mesh assigned;
                if (dropped == 0 && clipped == 0)
                {
                    DestroyImmediate(cut);
                    assigned = entry.Source;   // no pit under this mesh
                    report.Add($"{mf.name}: no pits underneath, left as is");
                }
                else
                {
                    assigned = SaveCut(cut, entry, signature);
                    report.Add($"{mf.name}: {dropped} triangles removed, {clipped} clipped round {holes.Count} pits");
                }

                Undo.RecordObject(mf, "Cut pit holes");
                mf.sharedMesh = assigned;
                EditorUtility.SetDirty(mf);
                entry.Cut = assigned;
                entry.Signature = signature;
                changed = true;
                Debug.Log($"[GROUND] {report[report.Count - 1]}");
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
                if (applyToPrefab) ApplyToPrefabIfInstance();
            }
            return report.Count > 0 ? string.Join("\n", report) : "Ground already cut for the current pits.";
        }

        Mesh SaveCut(Mesh cut, Entry entry, string signature)
        {
            System.IO.Directory.CreateDirectory(GeneratedFolder);
            string path = $"{GeneratedFolder}/{entry.Source.name}_{Hash(signature)}_PitHoles.asset";

            // Replace this ground's previous copy rather than piling up generated meshes.
            if (entry.Cut != null && entry.Cut != entry.Source)
            {
                string old = AssetDatabase.GetAssetPath(entry.Cut);
                if (!string.IsNullOrEmpty(old) && old.StartsWith(GeneratedFolder) && old != path) AssetDatabase.DeleteAsset(old);
            }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(cut, existing);
                DestroyImmediate(cut);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            cut.name = $"{entry.Source.name}_PitHoles";
            AssetDatabase.CreateAsset(cut, path);
            AssetDatabase.SaveAssets();
            return cut;
        }

        /// <summary>When this slot is inside a prefab instance, keep the prefab asset in step.</summary>
        public void ApplyToPrefabIfInstance()
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(this)) return;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(this);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                // Whole-object applies: per-property applies of the entry list crashed natively
                // when its references pointed at a ground that had just been replaced.
                foreach (var e in _entries)
                {
                    if (e.Filter == null || !PrefabUtility.IsPartOfPrefabInstance(e.Filter)) continue;
                    if (PrefabUtility.IsAddedGameObjectOverride(e.Filter.gameObject)) continue;
                    if (PrefabUtility.HasPrefabInstanceAnyOverrides(PrefabUtility.GetNearestPrefabInstanceRoot(e.Filter), false))
                        PrefabUtility.ApplyObjectOverride(e.Filter, path, InteractionMode.AutomatedAction);
                }
                PrefabUtility.ApplyObjectOverride(this, path, InteractionMode.AutomatedAction);
            }
            catch (Exception ex)
            {
                // Leave it as a scene override rather than fail; the cut is still in place.
                Debug.LogWarning($"[GROUND] Could not apply the cut ground to {path}: {ex.Message}");
            }
        }

        static Mesh OriginalMesh(MeshFilter mf, Mesh candidate)
        {
            string path = AssetDatabase.GetAssetPath(candidate);
            if (string.IsNullOrEmpty(path) || !path.StartsWith(GeneratedFolder)) return candidate;
            var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(mf);
            if (original != null && original.sharedMesh != null && !AssetDatabase.GetAssetPath(original.sharedMesh).StartsWith(GeneratedFolder))
                return original.sharedMesh;
            return candidate;
        }

        static string Signature(List<PitHoleMeshCutter.Hole> holes, Transform ground, Mesh source)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(AssetDatabase.GetAssetPath(source)).Append('|').Append(source.name).Append('|').Append(source.vertexCount);
            foreach (var h in holes) sb.Append($"|{h.Centre.x:F3},{h.Centre.y:F3},{h.Radius:F3}");
            var m = ground.localToWorldMatrix;
            for (int i = 0; i < 16; i++) sb.Append(',').Append(m[i].ToString("F3"));
            return sb.ToString();
        }

        static string Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return h.ToString("x8");
            }
        }
#endif
    }
}
