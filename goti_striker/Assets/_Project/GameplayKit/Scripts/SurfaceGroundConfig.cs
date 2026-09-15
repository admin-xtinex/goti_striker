using System.Collections.Generic;
using UnityEngine;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Separates visual ground from physics ground. Default: both enabled (empty-scene safe).
    /// The play surface is a set of colliders under <see cref="SurfaceCollisionRoot"/> rather than
    /// one box, so the pit openings can be left clear for marbles to drop into the basins.
    /// </summary>
    public class SurfaceGroundConfig : MonoBehaviour
    {
        public const string LayerMarble = "Marble";
        public const string LayerGameplayGround = "GameplayGround";
        public const string LayerObstacle = "Obstacle";
        public const string LayerPitTrigger = "PitTrigger";

        [Header("Surface")]
        public bool ShowGameplaySurfaceVisual = true;
        public bool EnableGameplaySurfaceCollider = true;
        public float GroundVerticalOffset = 0f;

        [Header("Refs")]
        public Transform VisualSurface;
        [Tooltip("Parent of the play-surface colliders. Preferred over the single GameplayCollider ref.")]
        public Transform SurfaceCollisionRoot;
        [Tooltip("Parent of the boundary wall colliders. Always solid, even in final-game mode.")]
        public Transform BoundaryCollisionRoot;
        [Tooltip("Legacy single-collider ref. Used as a fallback when SurfaceCollisionRoot is unset.")]
        public Collider GameplayCollider;
        public Transform GameplaySurfaceRoot;

        [Header("Spawn")]
        [Tooltip("Extra clearance above surface + marble radius.")]
        public float SpawnSafetyOffset = 0.05f;

        [Header("Host map integration")]
        [Tooltip("Turn on when dropping the kit onto a map that has its own ground collider.\n" +
                 "A map floor level with the kit surface seals the pit basins, so marbles rest on " +
                 "top instead of settling in. This makes the kit's own surface authoritative by " +
                 "disabling marble collisions with the map's layers.")]
        public bool IgnoreHostMapCollision = false;

        [Tooltip("Layers the host map's ground uses (often Default). Marble-vs-these collisions are " +
                 "switched off at runtime so only the kit's surface, walls and pit basins matter.")]
        public LayerMask HostMapLayers = 0;

        public static SurfaceGroundConfig Instance { get; private set; }

        private readonly List<Collider> _surfaceColliders = new List<Collider>();

        private void Awake()
        {
            Instance = this;
            Apply();
            ApplyHostMapPolicy();
        }

        /// <summary>
        /// Makes the kit's surface authoritative over a host map's ground.
        /// Physics.IgnoreLayerCollision is a global setting, so this only runs when explicitly
        /// enabled, and it stays in effect for the rest of the play session.
        /// </summary>
        public void ApplyHostMapPolicy()
        {
            if (!IgnoreHostMapCollision) return;
            int marble = LayerMask.NameToLayer(LayerMarble);
            if (marble < 0) return;

            for (int layer = 0; layer < 32; layer++)
            {
                if ((HostMapLayers.value & (1 << layer)) == 0) continue;
                UnityEngine.Physics.IgnoreLayerCollision(marble, layer, true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>All colliders forming the play surface, newest-first from the root when present.</summary>
        public List<Collider> GetSurfaceColliders()
        {
            _surfaceColliders.Clear();
            if (SurfaceCollisionRoot != null)
                SurfaceCollisionRoot.GetComponentsInChildren(true, _surfaceColliders);
            else if (GameplayCollider != null)
                _surfaceColliders.Add(GameplayCollider);
            return _surfaceColliders;
        }

        public void Apply()
        {
            if (VisualSurface != null)
                VisualSurface.gameObject.SetActive(ShowGameplaySurfaceVisual);

            foreach (var c in GetSurfaceColliders())
            {
                if (c == null) continue;
                c.enabled = EnableGameplaySurfaceCollider;
                c.isTrigger = false;
            }

            if (GameplaySurfaceRoot != null)
            {
                var lp = GameplaySurfaceRoot.localPosition;
                lp.y = GroundVerticalOffset;
                GameplaySurfaceRoot.localPosition = lp;
            }
        }

        /// <summary>World Y of the top of the play surface, or origin Y if no surface collider exists.</summary>
        public float GetSurfaceTopWorldY()
        {
            float top = float.NegativeInfinity;
            foreach (var c in GetSurfaceColliders())
                if (c != null && c.enabled && c.bounds.max.y > top) top = c.bounds.max.y;

            if (!float.IsNegativeInfinity(top)) return top;
            if (GameplayOrigin.Instance != null) return GameplayOrigin.Instance.transform.position.y;
            return transform.position.y;
        }

        public Vector3 GetSafeSpawnWorld(Vector3 localOrWorldHint, float marbleRadius, bool hintIsLocal)
        {
            Vector3 world = hintIsLocal && GameplayOrigin.Instance != null
                ? GameplayOrigin.Instance.LocalToWorld(localOrWorldHint)
                : localOrWorldHint;

            float top = GetSurfaceTopWorldY();
            world.y = top + marbleRadius + SpawnSafetyOffset;
            return world;
        }
    }
}
