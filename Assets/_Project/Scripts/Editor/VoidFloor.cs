using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GravBoots.Editor
{
    /// <summary>
    /// Turns a lethal surface (tag <see cref="Layers.LethalTag"/>) into a shadowed void: the
    /// surface goes flat black, and a "Void" box above it (VoidFog shader) darkens everything in
    /// it the lower it is, so a pit fades into darkness rather than ending in a floor. Gameplay
    /// is untouched: the surface stays lethal.
    /// </summary>
    public static class VoidFloor
    {
        const string FloorMaterialPath = TestBedBuilder.Root + "/Materials/Void_Floor.mat";
        const string FogMaterialPath = TestBedBuilder.Root + "/Materials/Void_Fog.mat";
        const string ShaderPath = TestBedBuilder.Root + "/Shaders/VoidFog.shader";
        const int IgnoreRaycastLayer = 2;
        /// <summary>How tall the darkness is: half the clear space above the surface, within these limits.</summary>
        const float MinHeight = 1.5f, MaxHeight = 5f;
        /// <summary>Reaches past the surface's edges, so walls standing on them darken too.</summary>
        const float Margin = 0.6f;

        public static Material FloorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Void_Floor" };
            material.SetColor("_BaseColor", Color.black);
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
            return material;
        }

        static Material FogMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
            if (material != null)
                return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)) { name = "Void_Fog" };
            AssetDatabase.CreateAsset(material, FogMaterialPath);
            return material;
        }

        /// <summary>Every lethal surface in the open scenes. Returns how many.</summary>
        public static int ApplyAll()
        {
            int count = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.CompareTag(Layers.LethalTag))
                    continue;
                Apply(renderer);
                count++;
            }
            return count;
        }

        /// <summary>Blacks out <paramref name="surface"/> and puts the darkness above it. Undoable.</summary>
        public static void Apply(Renderer surface)
        {
            Undo.RecordObject(surface, "Void floor");
            surface.sharedMaterial = FloorMaterial();
            surface.shadowCastingMode = ShadowCastingMode.Off;

            if (surface.transform.Find("Void") is Transform old)
                Undo.DestroyObjectImmediate(old.gameObject);

            // Sized in world space from the surface's top face, along its up.
            Bounds bounds = surface.bounds;
            Vector3 up = surface.transform.up;
            Vector3 top = bounds.center + up * Vector3.Dot(bounds.extents, Abs(up));
            float clear = Physics.Raycast(top + up * 0.05f, up, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance : 40f;
            float height = Mathf.Clamp(clear * 0.5f, MinHeight, MaxHeight);

            // A child of the surface, so it moves with it.
            var fog = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fog.name = "Void";
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            fog.layer = IgnoreRaycastLayer;
            fog.transform.SetParent(surface.transform, false);
            fog.transform.SetPositionAndRotation(top + up * (height * 0.5f - 0.01f), surface.transform.rotation);
            // Width and depth in the surface's own frame (its lossy scale times its box).
            Vector3 size = LocalSize(surface);
            fog.transform.localScale = Div(new Vector3(size.x + 2f * Margin, height, size.z + 2f * Margin), surface.transform.lossyScale);
            var renderer = fog.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = FogMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Undo.RegisterCreatedObjectUndo(fog, "Void floor");
        }

        static Vector3 LocalSize(Renderer surface)
        {
            Vector3 local = surface.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
                ? filter.sharedMesh.bounds.size : Vector3.one;
            return Vector3.Scale(local, surface.transform.lossyScale);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }
}
