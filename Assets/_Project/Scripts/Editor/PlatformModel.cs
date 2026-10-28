using GravBoots.Level;
using UnityEditor;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// Dresses moving platform decks in the Tripo platform models: light panels for magnetic decks,
    /// dark plates for the rest. The deck's <see cref="GreyboxBox"/> stays the collider; a
    /// <see cref="DeckSkin"/> fits the model to it.
    /// </summary>
    public static class PlatformModel
    {
        const string Folder = TestBedBuilder.Root + "/Art/Level/Platform";
        const string LightPath = Folder + "/Model_Light_Platform/futuristic platform 3d model(2)/futuristic_platform_3d_model.fbx";
        const string DarkPath = Folder + "/Model_Dark_Platform/futuristic platform 3d model(1)/futuristic_platform_3d_model.fbx";

        public static bool Available =>
            AssetDatabase.LoadAssetAtPath<GameObject>(LightPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(DarkPath) != null;

        /// <summary>Puts the right model on every deck box under <paramref name="mover"/>'s platform. Undoable.</summary>
        public static int Apply(KinematicMover mover)
        {
            if (mover == null || mover.platform == null)
                return 0;
            int count = 0;
            foreach (GreyboxBox deck in mover.platform.GetComponentsInChildren<GreyboxBox>())
            {
                Apply(deck);
                count++;
            }
            return count;
        }

        public static void Apply(GreyboxBox deck)
        {
            if (deck.transform.Find("Model") is Transform old)
                Undo.DestroyObjectImmediate(old.gameObject);
            bool magnetic = Layers.IsMagnetic(deck.GetComponent<Collider>());
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(magnetic ? LightPath : DarkPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, deck.transform);
            model.name = "Model";
            model.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(model, "Platform model");

            if (!deck.TryGetComponent(out DeckSkin skin))
                skin = Undo.AddComponent<DeckSkin>(deck.gameObject);
            Undo.RecordObject(skin, "Platform model");
            skin.Bind(model.transform, model.GetComponent<Renderer>().localBounds.size.y);
            EditorUtility.SetDirty(skin);
            if (deck.TryGetComponent(out MeshRenderer own))
            {
                Undo.RecordObject(own, "Platform model");
                own.enabled = false;
            }
        }
    }
}
