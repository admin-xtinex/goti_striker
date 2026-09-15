using UnityEngine;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Marker + bookkeeping for the portable PitStriker_GameplayKit prefab root.
    /// </summary>
    public class GameplayKitRoot : MonoBehaviour
    {
        public const string PrefabName = "PitStriker_GameplayKit";

        public GameplayOrigin Origin;
        public ShotModeConfig ShotConfig;
        public Transform GameplaySurface;
        public Transform Pits;
        public Transform Marbles;
        public Transform Cameras;
        public Transform HUD;
        public Transform Audio;
        public Transform ShotControlUI;

        [Tooltip("When false, surface mesh/collider stay but can be hidden for maps that supply their own flat play area.")]
        public bool GameplaySurfaceEnabled = true;

        public float SurfaceHeightOffset = 0f;

        private void Reset()
        {
            Origin = GetComponentInChildren<GameplayOrigin>(true);
            ShotConfig = GetComponentInChildren<ShotModeConfig>(true);
        }

        public void ApplySurfaceOffset()
        {
            if (GameplaySurface == null) return;
            var lp = GameplaySurface.localPosition;
            lp.y = SurfaceHeightOffset;
            GameplaySurface.localPosition = lp;
        }

        public void SetSurfaceActive(bool active)
        {
            GameplaySurfaceEnabled = active;
            if (GameplaySurface != null)
                GameplaySurface.gameObject.SetActive(active);
        }
    }
}
