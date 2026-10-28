using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// Small procedural meshes for additive glow effects. All lie around the local Y axis (flat
    /// in XZ) and fade through vertex alpha, so a plain particle shader gives soft edges without
    /// textures.
    /// </summary>
    public static class GlowMeshes
    {
        /// <summary>A thin solid ring (torus) of <paramref name="radius"/>, tube radius <paramref name="tube"/>.</summary>
        public static Mesh Torus(float radius, float tube, int segments = 48, int sides = 8)
        {
            var vertices = new Vector3[(segments + 1) * (sides + 1)];
            var normals = new Vector3[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * sides * 6];
            for (int i = 0, v = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++, v++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    Vector3 normal = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices[v] = outward * radius + normal * tube;
                    normals[v] = normal;
                    colors[v] = Color.white;
                }
            }
            for (int i = 0, t = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = a + 1; triangles[t++] = b + 1;
                }
            return Build("GlowTorus", vertices, normals, colors, triangles);
        }

        /// <summary>
        /// A flat band, transparent at <paramref name="inner"/> and <paramref name="outer"/> and
        /// opaque at <paramref name="peak"/>: a soft halo around a ring.
        /// </summary>
        public static Mesh Halo(float inner, float peak, float outer, int segments = 48)
        {
            float[] radii = { inner, peak, outer };
            float[] alphas = { 0f, 1f, 0f };
            var vertices = new Vector3[(segments + 1) * 3];
            var colors = new Color[vertices.Length];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < 3; k++)
                {
                    vertices[i * 3 + k] = outward * radii[k];
                    colors[i * 3 + k] = new Color(1f, 1f, 1f, alphas[k]);
                }
            }
            var triangles = new int[segments * 2 * 6];
            for (int i = 0, t = 0; i < segments; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = i * 3 + k, b = a + 3;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = a + 1; triangles[t++] = b + 1;
                }
            return Build("GlowHalo", vertices, Flat(vertices.Length), colors, triangles);
        }

        /// <summary>A flat disc, opaque at the centre and fading to nothing at <paramref name="radius"/>.</summary>
        public static Mesh Disc(float radius, int segments = 32)
        {
            var vertices = new Vector3[segments + 2];
            var colors = new Color[vertices.Length];
            colors[0] = Color.white;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                colors[i + 1] = new Color(1f, 1f, 1f, 0f);
            }
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
            return Build("GlowDisc", vertices, Flat(vertices.Length), colors, triangles);
        }

        static Vector3[] Flat(int count)
        {
            var normals = new Vector3[count];
            for (int i = 0; i < count; i++)
                normals[i] = Vector3.up;
            return normals;
        }

        static Mesh Build(string name, Vector3[] vertices, Vector3[] normals, Color[] colors, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
