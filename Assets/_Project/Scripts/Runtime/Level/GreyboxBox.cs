using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// A box of any size whose UVs are in metres, so a 1 m grid texture tiles evenly whatever
    /// the box's size. The mesh is rebuilt from <see cref="size"/> on load and never saved.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class GreyboxBox : MonoBehaviour, ISizedBox
    {
        public Vector3 size = Vector3.one;

        public Vector3 Size { get => size; set => size = value; }

        Mesh mesh;
        bool dirty;

        void OnEnable() => Rebuild();

        void OnValidate() => dirty = true;

        void Update()
        {
            // Scaling a box with the Scale tool would stretch its 1 m grid, so in the editor
            // fold any scale into the box's size instead, once the drag is released (folding
            // mid-drag would compound, as the tool scales relative to the drag's start).
            if (!Application.isPlaying && GUIUtility.hotControl == 0 && transform.localScale != Vector3.one)
            {
                Vector3 s = transform.localScale;
                size = Vector3.Scale(size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                transform.localScale = Vector3.one;
                dirty = true;
            }
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
                mesh = new Mesh { name = "GreyboxBox", hideFlags = HideFlags.DontSave };
            BuildBox(mesh, size);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var box = GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }

        static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        static void BuildBox(Mesh target, Vector3 size)
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            Vector3 half = size * 0.5f;

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                // Pick in-plane axes so every face's texture runs the right way up.
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                float halfU = Mathf.Abs(Vector3.Dot(half, u));
                float halfV = Mathf.Abs(Vector3.Dot(half, v));
                Vector3 centre = Vector3.Scale(n, half);

                for (int c = 0; c < 4; c++)
                {
                    float su = c == 0 || c == 3 ? -1f : 1f;
                    float sv = c < 2 ? -1f : 1f;
                    int i = f * 4 + c;
                    vertices[i] = centre + u * (su * halfU) + v * (sv * halfV);
                    normals[i] = n;
                    uvs[i] = new Vector2((su + 1f) * halfU, (sv + 1f) * halfV);
                }

                int t = f * 6, b = f * 4;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }

            target.Clear();
            target.vertices = vertices;
            target.normals = normals;
            target.uv = uvs;
            target.triangles = triangles;
            target.RecalculateTangents();
            target.RecalculateBounds();
        }
    }
}
