using System.IO;
using UnityEditor;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// The look of magnetic curves (quarter-pipes and rounded edges), drawn in code so it fits
    /// any radius and width: dark grey panels with glowing blue chevrons pointing both ways along
    /// the curve, so they read as "this surface carries you round the bend" from either side.
    /// <see cref="Level.CurvedRamp"/>'s UVs are in metres (u across, v along the arc), so the
    /// tile repeats every <see cref="TileMetres"/> and the chevrons always follow the arc.
    /// </summary>
    public static class CurvePanel
    {
        const string Folder = TestBedBuilder.Root + "/Art/Level/Curve";
        const string BasePath = Folder + "/curve_panel_basecolor.png";
        const string GlowPath = Folder + "/curve_panel_glow.png";
        const string MaterialPath = TestBedBuilder.Root + "/Materials/Curve_Panel.mat";
        const int Size = 512;
        const float TileMetres = 1.5f;

        // Halfway between a dark panel and the magnetic panels' average (0.84, 0.89, 0.94).
        static readonly Color Panel = new Color(0.52f, 0.553f, 0.587f);
        static readonly Color Seam = new Color(0.26f, 0.276f, 0.293f);
        static readonly Color Glow = new Color(0.25f, 0.7f, 1f);

        public static Material Ensure()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath) == null)
                DrawTextures();
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
            var glowMap = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath);

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Curve_Panel" };
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.4f);
            material.SetFloat("_Metallic", 0.3f);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", glowMap);
            material.SetColor("_EmissionColor", Color.white * 1.6f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var tiling = Vector2.one / TileMetres;
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_EmissionMap", tiling);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>
        /// Redraws the textures in place (same files, so the material and every curve using it
        /// keep their links) after changing the colours or the pattern.
        /// </summary>
        [MenuItem("GravBoots/Rebuild Curve Panel")]
        public static void Rebuild()
        {
            DrawTextures();
            Ensure();
        }

        static void DrawTextures()
        {
            Directory.CreateDirectory(Folder);
            Draw(out Color[] basePixels, out Color[] glowPixels);
            Save(basePixels, BasePath);
            Save(glowPixels, GlowPath);
        }

        /// <summary>
        /// One tile: a panel with seams at its edges and a groove down the middle, and two
        /// chevrons on the centre line, one pointing along +v (toward the curve's end) and one
        /// along -v (back toward its start).
        /// </summary>
        static void Draw(out Color[] basePixels, out Color[] glowPixels)
        {
            basePixels = new Color[Size * Size];
            glowPixels = new Color[Size * Size];
            var noise = new System.Random(7);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float grain = ((float)noise.NextDouble() - 0.5f) * 0.025f;
                Color c = Panel + new Color(grain, grain, grain);

                // Seams where tiles meet, and a faint groove along the middle.
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c = Color.Lerp(Seam, c, Smooth(0.006f, 0.014f, edge));
                c = Color.Lerp(c * 0.85f, c, Smooth(0.004f, 0.01f, Mathf.Abs(u - 0.5f) - 0.12f));

                float chevron = Mathf.Max(Chevron(u, v, 0.32f, -1f), Chevron(u, v, 0.68f, 1f));
                // A dark recess round each chevron, then the glowing stroke.
                c = Color.Lerp(c, Seam, Smooth(0f, 1f, Chevron(u, v, 0.32f, -1f, 0.05f)) * 0.6f);
                c = Color.Lerp(c, Seam, Smooth(0f, 1f, Chevron(u, v, 0.68f, 1f, 0.05f)) * 0.6f);
                c = Color.Lerp(c, Glow, chevron);
                c.a = 1f;
                basePixels[y * Size + x] = c;
                Color g = Glow * chevron;
                g.a = 1f;
                glowPixels[y * Size + x] = g;
            }
        }

        /// <summary>
        /// Coverage (0–1) of a chevron centred across the tile at height <paramref name="centre"/>,
        /// pointing toward +v (<paramref name="direction"/> 1) or -v (-1).
        /// </summary>
        static float Chevron(float u, float v, float centre, float direction, float extra = 0f)
        {
            const float halfSpan = 0.2f, depth = 0.12f, stroke = 0.035f;
            Vector2 apex = new Vector2(0.5f, centre + direction * depth * 0.5f);
            Vector2 left = new Vector2(0.5f - halfSpan, centre - direction * depth * 0.5f);
            Vector2 right = new Vector2(0.5f + halfSpan, centre - direction * depth * 0.5f);
            var p = new Vector2(u, v);
            float d = Mathf.Min(SegmentDistance(p, left, apex), SegmentDistance(p, right, apex));
            float half = stroke * 0.5f + extra;
            return 1f - Smooth(half - 0.004f, half + 0.004f, d);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        static Texture2D Save(Color[] pixels, string path)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
