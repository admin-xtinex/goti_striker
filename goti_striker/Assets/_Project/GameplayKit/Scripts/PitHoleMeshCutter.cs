using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PitStriker.Gameplay;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Cuts round holes into a ground mesh wherever a pit is, so each pit's bowl (just below
    /// ground level) shows through a continuous ground surface such as a soil plane.
    ///
    /// Triangles inside a hole are dropped; triangles crossing the rim are clipped against a
    /// 64-sided circle, so the edge is round whatever the mesh density. Positions, normals, UVs
    /// and tangents on the new edge are interpolated from the original triangle. Pure mesh math:
    /// used at edit time by <see cref="GroundWithPitHoles"/>, never needed in a build.
    /// </summary>
    public static class PitHoleMeshCutter
    {
        const int CircleSegments = 64;

        public struct Hole
        {
            public Vector2 Centre;   // world XZ
            public float Radius;
        }

        /// <summary>
        /// One hole per pit under <paramref name="scope"/> (or the whole scene if null), sized to
        /// the pit's bowl mesh - a hair inside its rim so the ground laps over the edge.
        /// </summary>
        public static List<Hole> FindHoles(Transform scope)
        {
            var holes = new List<Hole>();
            var pits = scope != null
                ? scope.GetComponentsInChildren<PitZone>(true)
                : Object.FindObjectsByType<PitZone>(FindObjectsInactive.Exclude);
            foreach (var pz in pits)
            {
                float radius = 0f;
                foreach (var mf in pz.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    if (b.size.y < 0.05f) continue;   // flat lips, decals and numerals are not the bowl
                    var s = mf.transform.lossyScale;
                    radius = Mathf.Max(radius, Mathf.Max(b.extents.x * Mathf.Abs(s.x), b.extents.z * Mathf.Abs(s.z)));
                }
                if (radius <= 0f) radius = pz.RimRadius;
                holes.Add(new Hole
                {
                    Centre = new Vector2(pz.transform.position.x, pz.transform.position.z),
                    Radius = radius - 0.004f,
                });
            }
            return holes;
        }

        /// <summary>A point on a triangle: world XZ plus barycentric weights of the source corners.</summary>
        struct P
        {
            public Vector2 Xz;
            public Vector3 W;
            public P(Vector2 xz, Vector3 w) { Xz = xz; W = w; }
            public static P Lerp(P a, P b, float t) => new P(Vector2.Lerp(a.Xz, b.Xz, t), Vector3.Lerp(a.W, b.W, t));
        }

        /// <summary>
        /// Returns a new mesh: <paramref name="src"/> (placed by <paramref name="t"/>) with the holes
        /// cut. <paramref name="dropped"/> and <paramref name="clipped"/> are both 0 when no pit lies
        /// under the mesh.
        /// </summary>
        public static Mesh Cut(Mesh src, Transform t, List<Hole> holes, out int dropped, out int clipped)
        {
            dropped = clipped = 0;
            var verts = src.vertices;
            var normals = src.normals;
            var uv = src.uv;
            var uv2 = src.uv2;
            var tangents = src.tangents;
            bool hasN = normals.Length == verts.Length, hasUv = uv.Length == verts.Length;
            bool hasUv2 = uv2.Length == verts.Length, hasT = tangents.Length == verts.Length;

            var world = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var w = t.TransformPoint(verts[i]);
                world[i] = new Vector2(w.x, w.z);
            }

            // Circle polygons, counter-clockwise in XZ.
            var circles = new List<Vector2[]>();
            foreach (var h in holes)
            {
                var c = new Vector2[CircleSegments];
                for (int s = 0; s < CircleSegments; s++)
                {
                    float a = s * Mathf.PI * 2f / CircleSegments;
                    c[s] = h.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * h.Radius;
                }
                circles.Add(c);
            }

            var outV = new List<Vector3>(verts.Length);
            var outN = new List<Vector3>(verts.Length);
            var outUv = new List<Vector2>(verts.Length);
            var outUv2 = new List<Vector2>(hasUv2 ? verts.Length : 0);
            var outT = new List<Vector4>(hasT ? verts.Length : 0);
            var outIdx = new List<int>();

            // Original vertices are reused as they are; new edge vertices are appended.
            for (int i = 0; i < verts.Length; i++)
            {
                outV.Add(verts[i]);
                outN.Add(hasN ? normals[i] : Vector3.up);
                outUv.Add(hasUv ? uv[i] : Vector2.zero);
                if (hasUv2) outUv2.Add(uv2[i]);
                if (hasT) outT.Add(tangents[i]);
            }

            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                var tris = src.GetTriangles(sub);
                for (int k = 0; k < tris.Length; k += 3)
                {
                    int i0 = tris[k], i1 = tris[k + 1], i2 = tris[k + 2];
                    Vector2 a = world[i0], b = world[i1], c = world[i2];

                    int hit = -1;
                    for (int h = 0; h < holes.Count; h++)
                        if (TriangleTouchesCircle(a, b, c, holes[h])) { hit = h; break; }
                    if (hit < 0) { outIdx.Add(i0); outIdx.Add(i1); outIdx.Add(i2); continue; }

                    if (InsidePolygon(a, circles[hit]) && InsidePolygon(b, circles[hit]) && InsidePolygon(c, circles[hit]))
                    {
                        dropped++;
                        continue;
                    }

                    clipped++;
                    var tri = new List<P> { new P(a, new Vector3(1, 0, 0)), new P(b, new Vector3(0, 1, 0)), new P(c, new Vector3(0, 0, 1)) };
                    float srcArea = SignedArea(a, b, c);
                    foreach (var piece in Subtract(tri, circles[hit]))
                    {
                        int baseIndex = outV.Count;
                        foreach (var p in piece)
                        {
                            outV.Add(verts[i0] * p.W.x + verts[i1] * p.W.y + verts[i2] * p.W.z);
                            Vector3 n = hasN ? normals[i0] * p.W.x + normals[i1] * p.W.y + normals[i2] * p.W.z : Vector3.up;
                            outN.Add(n.sqrMagnitude > 0f ? n.normalized : Vector3.up);
                            outUv.Add(hasUv ? uv[i0] * p.W.x + uv[i1] * p.W.y + uv[i2] * p.W.z : Vector2.zero);
                            if (hasUv2) outUv2.Add(uv2[i0] * p.W.x + uv2[i1] * p.W.y + uv2[i2] * p.W.z);
                            if (hasT) outT.Add(tangents[i0] * p.W.x + tangents[i1] * p.W.y + tangents[i2] * p.W.z);
                        }
                        for (int f = 1; f + 1 < piece.Count; f++)
                        {
                            int x0 = baseIndex, x1 = baseIndex + f, x2 = baseIndex + f + 1;
                            // Keep the source triangle's facing.
                            if (Mathf.Sign(SignedArea(piece[0].Xz, piece[f].Xz, piece[f + 1].Xz)) != Mathf.Sign(srcArea))
                            { int tmp = x1; x1 = x2; x2 = tmp; }
                            outIdx.Add(x0); outIdx.Add(x1); outIdx.Add(x2);
                        }
                    }
                }
            }

            var mesh = new Mesh { name = src.name.Replace("_PitHoles", "") + "_PitHoles" };
            if (outV.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(outV);
            mesh.SetNormals(outN);
            mesh.SetUVs(0, outUv);
            if (hasUv2) mesh.SetUVs(1, outUv2);
            if (hasT) mesh.SetTangents(outT);
            mesh.SetTriangles(outIdx, 0);   // a ground plane is one surface: one submesh
            mesh.RecalculateBounds();
            if (!hasT && hasUv) mesh.RecalculateTangents();
            return mesh;
        }

        static bool Inside(Vector2 p, Hole h) => (p - h.Centre).sqrMagnitude <= h.Radius * h.Radius;

        static bool TriangleTouchesCircle(Vector2 a, Vector2 b, Vector2 c, Hole h)
        {
            if (Inside(a, h) || Inside(b, h) || Inside(c, h)) return true;
            if (PointInTriangle(h.Centre, a, b, c)) return true;
            float r2 = h.Radius * h.Radius;
            return SegmentDistSq(h.Centre, a, b) < r2 || SegmentDistSq(h.Centre, b, c) < r2 || SegmentDistSq(h.Centre, c, a) < r2;
        }

        /// <summary>Triangle minus convex polygon = union over edges i of (inside edges 0..i-1) and (outside edge i).</summary>
        static IEnumerable<List<P>> Subtract(List<P> tri, Vector2[] poly)
        {
            var inside = tri;
            for (int i = 0; i < poly.Length && inside.Count >= 3; i++)
            {
                Vector2 e0 = poly[i], e1 = poly[(i + 1) % poly.Length];
                var outsidePart = Clip(inside, e0, e1, keepLeft: false);
                if (outsidePart.Count >= 3 && Mathf.Abs(PolygonArea(outsidePart)) > 1e-7f) yield return outsidePart;
                inside = Clip(inside, e0, e1, keepLeft: true);
            }
        }

        /// <summary>Sutherland-Hodgman against the line e0->e1; keeps the left (inside, CCW) or right side.</summary>
        static List<P> Clip(List<P> poly, Vector2 e0, Vector2 e1, bool keepLeft)
        {
            var result = new List<P>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                P cur = poly[i], nxt = poly[(i + 1) % poly.Count];
                float dc = Side(e0, e1, cur.Xz), dn = Side(e0, e1, nxt.Xz);
                if (!keepLeft) { dc = -dc; dn = -dn; }
                bool cIn = dc >= 0f, nIn = dn >= 0f;
                if (cIn) result.Add(cur);
                if (cIn != nIn) result.Add(P.Lerp(cur, nxt, dc / (dc - dn)));
            }
            return result;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        static float SignedArea(Vector2 a, Vector2 b, Vector2 c) => Side(a, b, c) * 0.5f;

        static float PolygonArea(List<P> poly)
        {
            float s = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i].Xz, q = poly[(i + 1) % poly.Count].Xz;
                s += p.x * q.y - q.x * p.y;
            }
            return s * 0.5f;
        }

        static bool InsidePolygon(Vector2 p, Vector2[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
                if (Side(poly[i], poly[(i + 1) % poly.Length], p) < 0f) return false;
            return true;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Side(a, b, p), d2 = Side(b, c, p), d3 = Side(c, a, p);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float SegmentDistSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return (a + ab * t - p).sqrMagnitude;
        }
    }
}
