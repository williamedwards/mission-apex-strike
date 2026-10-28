using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// Fits a model over a <see cref="GreyboxBox"/> deck: the box stays the collider (magnetic or
    /// not) with its own look hidden, and the model is stretched to the box's width and depth and
    /// flattened to its thickness, flush with its top. Follows the box when it's resized in the
    /// editor.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GreyboxBox))]
    public class DeckSkin : MonoBehaviour
    {
        [SerializeField] Transform model;
        [Tooltip("The model's own height (its width and depth are 1).")]
        [SerializeField] float modelHeight = 0.13f;

        GreyboxBox box;
        Vector3 fittedSize;

        public void Bind(Transform deckModel, float height)
        {
            model = deckModel;
            modelHeight = height;
            Fit();
        }

        void OnEnable()
        {
            box = GetComponent<GreyboxBox>();
            Fit();
        }

        void Update()
        {
            if (!Application.isPlaying && box != null && box.size != fittedSize)
                Fit();
        }

        void Fit()
        {
            if (box == null)
                box = GetComponent<GreyboxBox>();
            if (model == null || box == null || modelHeight <= 0f)
                return;
            Vector3 size = box.size;
            fittedSize = size;
            // Only write what differs, so loading a scene doesn't mark it modified.
            if (TryGetComponent(out MeshRenderer own) && own.enabled)
                own.enabled = false;
            // The model's origin is the middle of its underside.
            Vector3 position = Vector3.down * (size.y * 0.5f);
            Vector3 scale = new Vector3(size.x, size.y / modelHeight, size.z);
            if (model.localPosition != position)
                model.localPosition = position;
            if (model.localScale != scale)
                model.localScale = scale;
        }
    }
}
