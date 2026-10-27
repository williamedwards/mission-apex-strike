using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>A box sized by a field rather than by scale; the editor's face handles resize it.</summary>
    public interface ISizedBox
    {
        Vector3 Size { get; set; }
        void Rebuild();
    }
}
