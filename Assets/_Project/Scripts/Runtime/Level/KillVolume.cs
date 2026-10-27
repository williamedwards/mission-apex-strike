using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// An invisible box that kills the player on entry: the inside of death pits. Lethal
    /// surfaces you can see use the <see cref="Layers.LethalTag"/> tag instead.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider))]
    public class KillVolume : MonoBehaviour, ISizedBox
    {
        public Vector3 size = new Vector3(4f, 2f, 4f);

        public Vector3 Size { get => size; set => size = value; }

        void OnEnable() => Rebuild();

        void OnValidate() => Rebuild();

        public void Rebuild()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
