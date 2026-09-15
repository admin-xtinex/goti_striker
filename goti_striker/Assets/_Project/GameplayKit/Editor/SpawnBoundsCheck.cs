#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PitStriker.Physics;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Shared spawn-vs-ground validation for the placement window and the batch verifier.
    /// Measures in collider local space, so a Y-rotated kit is tested against the real
    /// oriented box instead of its (much larger) world AABB.
    /// A spawn is accepted if the kit collider covers it OR a host map surface sits under it,
    /// so the kit validates the same whether it brings its own ground or borrows the map's.
    /// </summary>
    public static class SpawnBoundsCheck
    {
        public const float EdgePad = 0.05f;
        const float ProbeUp = 2f;
        const float ProbeDown = 6f;
        const float FallbackRadius = 0.16f;

        public struct SpawnPoint
        {
            public string Name;
            public Vector3 World;
            public float Radius;
            public bool IsLiveMarble;
        }

        public static List<SpawnPoint> Collect(GameplayKitRoot kit)
        {
            var pts = new List<SpawnPoint>();
            if (kit == null) return pts;

            var origin = kit.GetComponentInChildren<GameplayOrigin>(true);
            if (origin != null)
            {
                pts.Add(new SpawnPoint { Name = "StartTee", World = origin.GetStartWorld(), Radius = FallbackRadius });
                pts.Add(new SpawnPoint { Name = "TeeAfterPit1", World = origin.LocalToWorld(origin.TeeAfterPit1Local), Radius = FallbackRadius });
                pts.Add(new SpawnPoint { Name = "TeeAfterPit2", World = origin.LocalToWorld(origin.TeeAfterPit2Local), Radius = FallbackRadius });
            }

            foreach (var m in kit.GetComponentsInChildren<MarbleController>(true))
            {
                pts.Add(new SpawnPoint
                {
                    Name = m.name,
                    World = m.transform.position,
                    Radius = m.WorldRadius > 0f ? m.WorldRadius : FallbackRadius,
                    IsLiveMarble = true
                });
            }

            return pts;
        }

        /// <summary>XZ containment in the collider's own space. Rotation- and scale-safe for BoxCollider.</summary>
        public static bool InsideColliderXZ(Collider col, Vector3 world, float pad, out string detail)
        {
            if (col is BoxCollider box)
            {
                Vector3 l = box.transform.InverseTransformPoint(world);
                Vector3 min = box.center - box.size * 0.5f;
                Vector3 max = box.center + box.size * 0.5f;
                detail = $"local x {l.x:F2} in {min.x:F1}..{max.x:F1}, z {l.z:F2} in {min.z:F1}..{max.z:F1}";
                return l.x >= min.x + pad && l.x <= max.x - pad
                    && l.z >= min.z + pad && l.z <= max.z - pad;
            }

            Bounds b = col.bounds;
            detail = $"world x {world.x:F2} in {b.min.x:F1}..{b.max.x:F1}, z {world.z:F2} in {b.min.z:F1}..{b.max.z:F1}";
            return world.x >= b.min.x + pad && world.x <= b.max.x - pad
                && world.z >= b.min.z + pad && world.z <= b.max.z - pad;
        }

        /// <summary>Solid (non-trigger) collider directly under the spawn — the host-map ground path.</summary>
        public static bool HasGroundBelow(Vector3 world, Collider ignore, out string detail)
        {
            detail = "none";
            var hits = UnityEngine.Physics.RaycastAll(world + Vector3.up * ProbeUp, Vector3.down, ProbeUp + ProbeDown);
            float bestY = float.NegativeInfinity;
            Collider best = null;

            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (ignore != null && h.collider == ignore) continue;
                if (h.point.y > world.y + 0.01f) continue;
                if (h.point.y > bestY) { bestY = h.point.y; best = h.collider; }
            }

            if (best == null) return false;
            detail = $"{best.name} @ y={bestY:F2}";
            return true;
        }

        /// <summary>
        /// Appends a line per spawn point. Returns false if any spawn would drop through,
        /// or if no spawn points exist at all (a vacuous pass is itself a regression).
        /// </summary>
        public static bool Evaluate(GameplayKitRoot kit, Collider kitCollider, StringBuilder sb)
        {
            // Colliders may have just been instantiated/moved; the host-ground probe needs current transforms.
            UnityEngine.Physics.SyncTransforms();

            var points = Collect(kit);
            if (points.Count == 0)
            {
                sb.AppendLine("- INVALID no spawn points found (GameplayOrigin missing?) — nothing was verified");
                return false;
            }

            // The surface is tiled (gaps at the pit openings), so a spawn only has to sit over
            // one tile. Fall back to the single legacy collider when there is no config.
            var cfg = kit.GetComponentInChildren<SurfaceGroundConfig>(true);
            var surfaces = new List<Collider>();
            if (cfg != null) surfaces.AddRange(cfg.GetSurfaceColliders());
            if (surfaces.Count == 0 && kitCollider != null) surfaces.Add(kitCollider);

            bool allOk = true;

            foreach (var p in points)
            {
                float pad = Mathf.Max(EdgePad, p.Radius);
                string how = null;

                Collider hitTile = null;
                string boxDetail = null;
                foreach (var s in surfaces)
                {
                    if (s == null || !s.enabled || s.isTrigger) continue;
                    if (InsideColliderXZ(s, p.World, pad, out var d)) { hitTile = s; boxDetail = d; break; }
                }
                bool kitGroundLive = hitTile != null;

                if (kitGroundLive)
                    how = $"kit surface '{hitTile.name}' ({boxDetail})";
                else if (HasGroundBelow(p.World, null, out var hostDetail))
                    how = "host map ground (" + hostDetail + ")";

                if (how == null)
                {
                    allOk = false;
                    string why = surfaces.Count == 0
                        ? "no surface colliders and no host ground beneath"
                        : "outside every surface tile and no host ground beneath";
                    sb.AppendLine($"- INVALID {p.Name} @ {Fmt(p.World)} — {why}");
                    continue;
                }

                if (p.IsLiveMarble && kitGroundLive)
                {
                    float top = hitTile.bounds.max.y;
                    float bottom = p.World.y - p.Radius;
                    if (bottom < top - 0.01f)
                    {
                        allOk = false;
                        sb.AppendLine($"- INVALID {p.Name} sits below surface (bottom={bottom:F3} top={top:F3})");
                        continue;
                    }
                }

                sb.AppendLine($"- OK {p.Name} @ {Fmt(p.World)} → {how}");
            }

            return allOk;
        }

        static string Fmt(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
#endif
