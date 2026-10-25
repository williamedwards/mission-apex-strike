using System.Collections.Generic;
using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// A 90° curved surface: the only way the magnetic boots change orientation (sharp joins
    /// are impassable). Builds a closed solid mesh and matching collider, with UVs in metres
    /// along the arc. The mesh is rebuilt from its settings on load and never saved.
    /// </summary>
    /// <remarks>
    /// Local frame: the curve starts at the origin, on a surface whose normal is +Y, heading
    /// along +Z, and spans <see cref="width"/> across X.
    /// <list type="bullet">
    /// <item><b>Concave</b> (inside corner, a quarter-pipe): curves up to end at
    /// (z = r, y = r) on a face whose normal is -Z, e.g. floor up into a wall ahead.</item>
    /// <item><b>Convex</b> (outside corner, a rounded edge): curves down to end at
    /// (z = r, y = -r) on a face whose normal is +Z, e.g. over the top edge of a block.</item>
    /// </list>
    /// Segment count sets how sharp each facet seam is (90° / segments); keep it well under
    /// <c>BootsSettings.followMaxAngle</c>.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class CurvedRamp : MonoBehaviour
    {
        public enum Shape { Concave, Convex }

        public Shape shape = Shape.Concave;
        [Min(0.1f)] public float radius = 2f;
        [Min(0.1f)] public float width = 4f;
        [Range(2, 64)] public int segments = 12;

        Mesh mesh;
        bool dirty;

        void OnEnable() => Rebuild();

        void OnValidate() => dirty = true;

        void Update()
        {
            if (dirty)
                Rebuild();
        }

        void OnDestroy()
        {
            if (mesh == null)
                return;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        public void Rebuild()
        {
            dirty = false;
            if (mesh == null)
                mesh = new Mesh { name = "CurvedRamp", hideFlags = HideFlags.DontSave };
            Build(mesh);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var meshCollider = GetComponent<MeshCollider>();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }

        /// <summary>Point on the arc at angle <paramref name="t"/> (radians, 0..π/2), in the (z, y) plane.</summary>
        Vector2 ArcPoint(float t) => shape == Shape.Concave
            ? new Vector2(radius * Mathf.Sin(t), radius - radius * Mathf.Cos(t))
            : new Vector2(radius * Mathf.Sin(t), radius * Mathf.Cos(t) - radius);

        /// <summary>Outward surface normal at angle <paramref name="t"/>, in the (z, y) plane.</summary>
        Vector2 ArcNormal(float t) => shape == Shape.Concave
            ? new Vector2(-Mathf.Sin(t), Mathf.Cos(t))
            : new Vector2(Mathf.Sin(t), Mathf.Cos(t));

        /// <summary>
        /// The point that closes the profile into a solid: the corner behind a quarter-pipe, or
        /// the centre of a rounded edge's arc.
        /// </summary>
        Vector2 Anchor => shape == Shape.Concave ? new Vector2(radius, 0f) : new Vector2(0f, -radius);

        void Build(Mesh target)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float halfWidth = width * 0.5f;

            Vector3 At(Vector2 zy, float x) => new Vector3(x, zy.y, zy.x);

            // The curved surface, smooth-shaded.
            int arcStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 0.5f / segments;
                Vector2 p = ArcPoint(t), n = ArcNormal(t);
                float v = t * radius;
                foreach (float x in new[] { -halfWidth, halfWidth })
                {
                    vertices.Add(At(p, x));
                    normals.Add(At(n, 0f));
                    uvs.Add(new Vector2(x + halfWidth, v));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int a = arcStart + i * 2;
                Vector3 n = (normals[a] + normals[a + 2]).normalized;
                Quad(a, a + 1, a + 3, a + 2, n);
            }

            // The two flat faces that close the solid behind the curve.
            // Normals are in (z, y). Concave: underside (-Y) and back (+Z). Convex: back (-Z) and underside (-Y).
            Vector2 start = ArcPoint(0f), end = ArcPoint(Mathf.PI * 0.5f), anchor = Anchor;
            bool concave = shape == Shape.Concave;
            FlatQuad(start, anchor, concave ? new Vector2(0f, -1f) : new Vector2(-1f, 0f));
            FlatQuad(end, anchor, concave ? new Vector2(1f, 0f) : new Vector2(0f, -1f));

            // End caps: fans from the anchor to the arc.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 n = new Vector3(side, 0f, 0f);
                int centre = AddVertex(At(anchor, side * halfWidth), n, new Vector2(anchor.x, anchor.y));
                int first = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    Vector2 p = ArcPoint(i * Mathf.PI * 0.5f / segments);
                    AddVertex(At(p, side * halfWidth), n, new Vector2(p.x, p.y));
                }
                for (int i = 0; i < segments; i++)
                    Triangle(centre, first + i, first + i + 1, n);
            }

            target.Clear();
            target.SetVertices(vertices);
            target.SetNormals(normals);
            target.SetUVs(0, uvs);
            target.SetTriangles(triangles, 0);
            target.RecalculateTangents();
            target.RecalculateBounds();

            int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                return vertices.Count - 1;
            }

            // Winds each triangle so it faces along its intended normal (Unity draws clockwise).
            void Triangle(int a, int b, int c, Vector3 facing)
            {
                Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(cross, facing) >= 0f)
                    triangles.AddRange(new[] { a, b, c });
                else
                    triangles.AddRange(new[] { a, c, b });
            }

            void Quad(int a, int b, int c, int d, Vector3 facing)
            {
                Triangle(a, b, c, facing);
                Triangle(a, c, d, facing);
            }

            void FlatQuad(Vector2 from, Vector2 to, Vector2 facingZY)
            {
                Vector3 n = At(facingZY.normalized, 0f);
                float length = Vector2.Distance(from, to);
                int a = AddVertex(At(from, -halfWidth), n, new Vector2(0f, 0f));
                int b = AddVertex(At(from, halfWidth), n, new Vector2(width, 0f));
                int c = AddVertex(At(to, halfWidth), n, new Vector2(width, length));
                int d = AddVertex(At(to, -halfWidth), n, new Vector2(0f, length));
                Quad(a, b, c, d, n);
            }
        }
    }
}
