using System.IO;
using GravBoots.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GravBoots.Editor
{
    /// <summary>
    /// Dresses a <see cref="Checkpoint"/> in the Tripo checkpoint pad: 2 m across, flattened to
    /// 0.12 m and sunk halfway into the surface, so it stands 6 cm proud (visual only, like the
    /// old pad: a non-magnetic collider here would make the boots let go on a wall). Two
    /// materials made from the model's texture: idle glows its painted blue, reached shifts
    /// those blues to green and glows brighter.
    /// </summary>
    public static class CheckpointModel
    {
        const string Folder = TestBedBuilder.Root + "/Art/Level/Checkpoint";
        const string ModelPath = Folder + "/Model/futuristic platform 3d model/futuristic_platform_3d_model.fbx";
        const string Generated = Folder + "/Generated";
        const string IdleMaterialPath = Generated + "/Checkpoint_Idle.mat";
        const string LitMaterialPath = Generated + "/Checkpoint_Lit.mat";
        const float Diameter = 2f;
        const float Height = 0.12f;
        /// <summary>Green, a little toward mint, like the old lit pad.</summary>
        const float LitHue = 0.4f;

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;

        /// <summary>Replaces <paramref name="checkpoint"/>'s pad with the model and points it at the two materials. Undoable.</summary>
        public static void Apply(Checkpoint checkpoint)
        {
            EnsureMaterials(out Material idle, out Material lit);
            Transform root = checkpoint.transform;
            if (root.Find("Pad") is Transform old)
                Undo.DestroyObjectImmediate(old.gameObject);

            var pad = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root);
            pad.name = "Pad";
            pad.transform.localPosition = Vector3.down * (Height * 0.5f);
            pad.transform.localRotation = Quaternion.identity;
            var renderer = pad.GetComponent<Renderer>();
            float modelHeight = renderer.localBounds.size.y;
            pad.transform.localScale = new Vector3(Diameter, Height / modelHeight, Diameter);
            renderer.sharedMaterial = checkpoint.IsLit ? lit : idle;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Undo.RegisterCreatedObjectUndo(pad, "Checkpoint model");

            var serialized = new SerializedObject(checkpoint);
            serialized.FindProperty("pad").objectReferenceValue = renderer;
            serialized.FindProperty("idleMaterial").objectReferenceValue = idle;
            serialized.FindProperty("litMaterial").objectReferenceValue = lit;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Rebuilds the generated textures and materials (after changing the model or the colours).</summary>
        [MenuItem("GravBoots/Rebuild Checkpoint Materials")]
        public static void RebuildMaterials()
        {
            AssetDatabase.DeleteAsset(Generated);
            EnsureMaterials(out _, out _);
        }

        static void EnsureMaterials(out Material idle, out Material lit)
        {
            idle = AssetDatabase.LoadAssetAtPath<Material>(IdleMaterialPath);
            lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (idle != null && lit != null)
                return;
            Directory.CreateDirectory(Generated);

            Material source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponent<Renderer>().sharedMaterial;
            var baseMap = (Texture2D)source.GetTexture("_BaseMap");
            Color[] original = ReadPixels(baseMap, out int width, out int height);
            var shifted = new Color[original.Length];
            var idleGlow = new Color[original.Length];
            var litGlow = new Color[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                bool blue = IsGlowBlue(original[i]);
                shifted[i] = blue ? ShiftHue(original[i], LitHue) : original[i];
                idleGlow[i] = blue ? original[i] : Color.black;
                litGlow[i] = blue ? shifted[i] : Color.black;
            }

            Texture2D litMap = SavePng(shifted, width, height, "checkpoint_lit_basecolor");
            Texture2D idleMask = SavePng(idleGlow, width, height, "checkpoint_idle_glow");
            Texture2D litMask = SavePng(litGlow, width, height, "checkpoint_lit_glow");

            idle = Glowing(source, baseMap, idleMask, 1.2f, IdleMaterialPath);
            lit = Glowing(source, litMap, litMask, 2.4f, LitMaterialPath);
        }

        /// <summary>The painted glow: clearly blue or cyan, not the grey metal.</summary>
        static bool IsGlowBlue(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return s > 0.25f && v > 0.25f && h > 0.45f && h < 0.72f;
        }

        static Color ShiftHue(Color c, float hue)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            Color shifted = Color.HSVToRGB(hue, s, v);
            shifted.a = c.a;
            return shifted;
        }

        /// <summary>The texture's pixels, read through a render texture so its import settings stay untouched.</summary>
        static Color[] ReadPixels(Texture2D texture, out int width, out int height)
        {
            width = texture.width;
            height = texture.height;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color[] pixels = copy.GetPixels();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        static Texture2D SavePng(Color[] pixels, int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = $"{Generated}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Glowing(Material source, Texture baseMap, Texture glow, float strength, string path)
        {
            var material = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetTexture("_BaseMap", baseMap);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", glow);
            material.SetColor("_EmissionColor", Color.white * strength);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
