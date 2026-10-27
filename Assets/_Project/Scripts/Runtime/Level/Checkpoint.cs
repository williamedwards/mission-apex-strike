using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// A spawn point the player activates by walking through its trigger. Respawning puts the
    /// player back on it, upright to its up, so a checkpoint on a wall or ceiling respawns you
    /// there magnetized (see <see cref="LevelSession"/>).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : SpawnPoint
    {
        [SerializeField] Renderer pad;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material litMaterial;

        public bool IsLit { get; private set; }

        void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(3f, 2f, 3f);
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (pad != null && idleMaterial != null && litMaterial != null)
                pad.sharedMaterial = lit ? litMaterial : idleMaterial;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + transform.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + transform.up * 0.9f, transform.forward);
            var trigger = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireCube(trigger.center, trigger.size);
        }
    }
}
