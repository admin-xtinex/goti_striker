using UnityEngine;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Kriya — map-adaptable surface look. Does not touch colliders or pit transforms.
    /// </summary>
    public class SurfaceVisualConfig : MonoBehaviour
    {
        public enum MapPreset { Village, Beach, Forest, Desert, Courtyard, SimpleTest, Custom }

        [Header("Targets (visual only)")]
        public Renderer SurfaceRenderer;
        public Renderer EdgeRenderer;

        [Header("Materials")]
        public Material SurfaceMaterial;
        public Material EdgeMaterial;

        [Header("Look")]
        public MapPreset Preset = MapPreset.Village;
        public Color SurfaceTint = new Color(0.88f, 0.64f, 0.38f, 1f);
        public Color EdgeTint = new Color(0.55f, 0.40f, 0.24f, 1f);
        public Vector2 SurfaceTiling = new Vector2(2.4f, 4.1f);
        public float SurfaceYOffset = 0.01f;
        [Tooltip("Enable/disable the portable surface mesh when the host map already has ground.")]
        public bool SurfaceVisible = true;

        static readonly Color[] PresetTints =
        {
            new Color(0.88f, 0.64f, 0.38f), // Village warm laterite
            new Color(0.92f, 0.82f, 0.55f), // Beach sand
            new Color(0.45f, 0.52f, 0.32f), // Forest earth
            new Color(0.85f, 0.62f, 0.35f), // Desert
            new Color(0.70f, 0.68f, 0.62f), // Courtyard stone-dust
            new Color(0.75f, 0.75f, 0.75f), // Simple test
        };

        void OnValidate() { Apply(); }
        void Start() { Apply(); }

        public void ApplyPreset(MapPreset preset)
        {
            Preset = preset;
            if (preset != MapPreset.Custom && (int)preset < PresetTints.Length)
                SurfaceTint = PresetTints[(int)preset];
            Apply();
        }

        [ContextMenu("Apply Surface Look")]
        public void Apply()
        {
            if (SurfaceRenderer != null)
            {
                SurfaceRenderer.enabled = SurfaceVisible;
                if (SurfaceMaterial != null) SurfaceRenderer.sharedMaterial = SurfaceMaterial;
                var mat = SurfaceRenderer.sharedMaterial;
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", SurfaceTint);
                    if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", SurfaceTiling);
                    if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", SurfaceTiling);
                }
                var t = SurfaceRenderer.transform;
                var p = t.localPosition;
                t.localPosition = new Vector3(p.x, SurfaceYOffset, p.z);
            }
            if (EdgeRenderer != null)
            {
                EdgeRenderer.enabled = SurfaceVisible;
                if (EdgeMaterial != null) EdgeRenderer.sharedMaterial = EdgeMaterial;
                var mat = EdgeRenderer.sharedMaterial;
                if (mat != null && mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", EdgeTint);
            }
        }
    }
}