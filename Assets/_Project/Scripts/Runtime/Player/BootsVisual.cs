using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Swaps the boot meshes to a glowing material while the boots are magnetized.</summary>
    public class BootsVisual : MonoBehaviour
    {
        [SerializeField] MagneticBoots boots;
        [SerializeField] Renderer[] bootRenderers;
        [SerializeField] Material offMaterial;
        [SerializeField] Material onMaterial;

        void OnEnable()
        {
            if (boots != null)
                boots.StateChanged += OnStateChanged;
            Apply(boots != null && boots.IsMagnetized);
        }

        void OnDisable()
        {
            if (boots != null)
                boots.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(BootsState previous, BootsState next) => Apply(next == BootsState.Magnetized);

        void Apply(bool on)
        {
            Material material = on ? onMaterial : offMaterial;
            if (material == null)
                return;
            foreach (Renderer r in bootRenderers)
                if (r != null)
                    r.sharedMaterial = material;
        }
    }
}
