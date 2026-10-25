using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>A place the player can start or be sent to; its up is the player's up.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public string label;
        [Tooltip("Debug spawn cycling visits points in this order.")]
        public int order;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(transform.position + transform.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + transform.up * 0.9f, transform.forward);
        }
    }
}
