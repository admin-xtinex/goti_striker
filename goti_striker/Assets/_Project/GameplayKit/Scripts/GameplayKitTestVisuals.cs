using UnityEngine;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Shows/hides the editor-only visual aids (playable-surface overlay, boundary outline,
    /// pit markers). These are render-only objects with no colliders, so toggling them
    /// can never change physics. Collision surfaces and boundary walls are separate and
    /// always stay active.
    /// </summary>
    [ExecuteAlways]
    public class GameplayKitTestVisuals : MonoBehaviour
    {
        [Header("Test visuals (editor aid only — hide for final game)")]
        [Tooltip("Master switch. Off = shipping look: gameplay surface and boundary are invisible but still solid.")]
        public bool ShowTestVisuals = true;

        [Tooltip("Faint transparent overlay showing the playable surface area.")]
        public bool ShowSurfaceOverlay = true;

        [Tooltip("Outline showing where the boundary walls are.")]
        public bool ShowBoundaryOutline = true;

        [Tooltip("Rings marking the three pit openings.")]
        public bool ShowPitMarkers = true;

        [Header("Refs")]
        public Transform SurfaceOverlay;
        public Transform BoundaryOutline;
        public Transform PitMarkers;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            SetActive(SurfaceOverlay, ShowTestVisuals && ShowSurfaceOverlay);
            SetActive(BoundaryOutline, ShowTestVisuals && ShowBoundaryOutline);
            SetActive(PitMarkers, ShowTestVisuals && ShowPitMarkers);
        }

        /// <summary>Shipping preset: every visual aid off, all collision untouched.</summary>
        public void SetFinalGameMode()
        {
            ShowTestVisuals = false;
            Apply();
        }

        static void SetActive(Transform t, bool active)
        {
            if (t != null && t.gameObject.activeSelf != active)
                t.gameObject.SetActive(active);
        }
    }
}
