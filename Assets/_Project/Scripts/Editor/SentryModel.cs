using System.Collections.Generic;
using System.IO;
using GravBoots.Hazards;
using UnityEditor;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// Dresses a <see cref="Sentry"/> in the Tripo sentry model, as two prefabs: SentryBody (the
    /// legs and hub, solid with a convex hull of the whole robot) and SentryHead (the dome and
    /// eye, turned to aim about a pivot at the eye). Scaled ×2 so the eye, and so the firing
    /// plane, stays at 1 m. Sentries hold prefab instances, so changing the model or these
    /// prefabs updates every sentry.
    /// </summary>
    public static class SentryModel
    {
        const string Folder = TestBedBuilder.Root + "/Art/Hazards/Sentry";
        const string ModelPath = Folder + "/Model/spherical+robot+3d+model/tripo_convert_31c24895-8f26-458c-ab37-e10d0a981d81.fbx";
        const string BodyPrefabPath = Folder + "/SentryBody.prefab";
        const string HeadPrefabPath = Folder + "/SentryHead.prefab";
        const string HullPath = Folder + "/SentryHull.asset";
        const string EyeMaterialPath = Folder + "/Sentry_Eye.mat";
        const string RubbleMaterialPath = Folder + "/Sentry_Rubble.mat";
        const float Scale = 2f;
        /// <summary>The head's pivot above the sentry's surface: the eye, and the firing plane.</summary>
        const float EyeHeight = 1f;
        /// <summary>
        /// Where a stomp lands, for <c>Sentry.headTop</c>. The dome is 1.53 m tall at its flat
        /// cap and falls to about 1.3 m 0.8 m out, so this takes landings over most of it.
        /// </summary>
        const float HeadTop = 1.45f;
        // Tripo's segmentation: the dome, the eye lens and four brackets under the dome.
        static readonly HashSet<string> HeadParts = new HashSet<string>
        {
            "tripo_part_2", "tripo_part_18", "tripo_part_36", "tripo_part_37", "tripo_part_38", "tripo_part_39",
        };
        const string EyePart = "tripo_part_18";

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;

        /// <summary>
        /// Replaces <paramref name="sentry"/>'s Body and Head (box or model) with instances of the
        /// model prefabs, and points the sentry at them. Undoable, for upgrading hand-edited chambers.
        /// </summary>
        public static void Apply(Sentry sentry, Material eyeOff)
        {
            EnsurePrefabs();
            Transform root = sentry.transform;
            foreach (string old in new[] { "Body", "Head" })
                if (root.Find(old) is Transform part)
                    Undo.DestroyObjectImmediate(part.gameObject);

            Transform body = Place(BodyPrefabPath, "Body", root, Vector3.zero);
            Transform head = Place(HeadPrefabPath, "Head", root, Vector3.up * EyeHeight);

            var serialized = new SerializedObject(sentry);
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("eye").objectReferenceValue = head.Find("Model/" + EyePart).GetComponent<Renderer>();
            serialized.FindProperty("eyeOff").objectReferenceValue = eyeOff;
            serialized.FindProperty("rubble").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(RubbleMaterialPath);
            serialized.FindProperty("eyeHeight").floatValue = EyeHeight;
            serialized.FindProperty("headTop").floatValue = HeadTop;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Whether <paramref name="sentry"/> still wears the old box body.</summary>
        public static bool HasBoxBody(Sentry sentry) =>
            sentry.transform.Find("Body") is Transform body && body.GetComponent<BoxCollider>() != null;

        /// <summary>Rebuilds the prefabs, hull and materials from the model (after changing the split or scale).</summary>
        [MenuItem("GravBoots/Rebuild Sentry Model Prefabs")]
        public static void RebuildPrefabs()
        {
            foreach (string path in new[] { BodyPrefabPath, HeadPrefabPath, HullPath })
                AssetDatabase.DeleteAsset(path);
            EnsurePrefabs();
        }

        static Transform Place(string prefabPath, string name, Transform parent, Vector3 localPosition)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Sentry model");
            return instance.transform;
        }

        static void EnsurePrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BodyPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(HeadPrefabPath) != null)
                return;
            Directory.CreateDirectory(Folder);
            Material eye = EyeMaterial();
            EnsureRubbleMaterial();

            var body = new GameObject("SentryBody").transform;
            Transform bodyModel = Holder(body, Vector3.zero);
            var head = new GameObject("SentryHead").transform;
            head.position = Vector3.up * EyeHeight;
            Transform headModel = Holder(head, Vector3.down * EyeHeight);

            // The file is Z-up; its root's rotation stands it upright. Place it as the body's model
            // (scaled, upright), then move each part across keeping its pose.
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(bodyModel, false);
            foreach (Transform part in new List<Transform>(model.GetComponentsInChildren<Transform>()))
            {
                if (part == model.transform || part.GetComponent<MeshRenderer>() == null)
                    continue;
                part.SetParent(HeadParts.Contains(part.name) ? headModel : bodyModel, true);
                if (part.name == EyePart)
                    part.GetComponent<Renderer>().sharedMaterial = eye;
            }
            Object.DestroyImmediate(model);

            var hull = body.gameObject.AddComponent<MeshCollider>();
            hull.sharedMesh = Hull(new[] { bodyModel, headModel }, body);
            hull.convex = true;
            // The head turns, so it has no collider of its own; the hull covers it.
            head.position = Vector3.zero;

            PrefabUtility.SaveAsPrefabAsset(body.gameObject, BodyPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(head.gameObject, HeadPrefabPath);
            Object.DestroyImmediate(body.gameObject);
            Object.DestroyImmediate(head.gameObject);
        }

        static Transform Holder(Transform parent, Vector3 localPosition)
        {
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.localScale = Vector3.one * Scale;
            return holder;
        }

        /// <summary>
        /// Every vertex of the model in the body's frame, saved as a mesh for a convex collider:
        /// the physics hull of the whole robot, so it stays solid as the head turns.
        /// </summary>
        static Mesh Hull(Transform[] holders, Transform body)
        {
            var points = new List<Vector3>();
            foreach (Transform holder in holders)
                foreach (MeshFilter filter in holder.GetComponentsInChildren<MeshFilter>())
                    foreach (Vector3 v in filter.sharedMesh.vertices)
                        points.Add(body.InverseTransformPoint(filter.transform.TransformPoint(v)));
            var indices = new int[points.Count - points.Count % 3];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            var mesh = new Mesh { name = "SentryHull", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(points);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, HullPath);
            return mesh;
        }

        /// <summary>The model's own lens texture, lit from within in red.</summary>
        static Material EyeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
            if (material != null)
                return material;
            Material source = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is GameObject go && go.name == EyePart)
                    source = go.GetComponent<Renderer>().sharedMaterial;
            material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture lens = material.GetTexture("_BaseMap");
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", lens);
            material.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.2f) * 2.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(material, EyeMaterialPath);
            return material;
        }

        static void EnsureRubbleMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(RubbleMaterialPath) != null)
                return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.36f, 0.37f, 0.39f));
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Metallic", 0.6f);
            AssetDatabase.CreateAsset(material, RubbleMaterialPath);
        }
    }
}
