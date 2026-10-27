using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>The chamber's goal: walking into its trigger completes the chamber.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("Scene to load after completing (must be in the build settings). Empty replays this chamber.")]
        public string nextScene;

        void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(3f, 2f, 3f);
        }

        void OnDrawGizmos()
        {
            var trigger = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(trigger.center, trigger.size);
        }
    }
}
