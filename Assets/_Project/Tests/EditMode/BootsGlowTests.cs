using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class BootsGlowTests
    {
        [Test]
        public void FadesInQuicklyAndOutSlowly()
        {
            Assert.That(BootsGlow.Fade(0f, true, 0.06f, 0.12f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BootsGlow.Fade(0f, true, 0.2f, 0.12f, 0.3f), Is.EqualTo(1f), "fully on, never past");
            Assert.That(BootsGlow.Fade(1f, false, 0.15f, 0.12f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BootsGlow.Fade(0.1f, false, 0.2f, 0.12f, 0.3f), Is.EqualTo(0f), "fully off, never below");
        }

        [Test]
        public void RingAndHaloLieFlatAroundTheAxisAtTheirRadius()
        {
            Mesh ring = GlowMeshes.Torus(0.13f, 0.006f);
            foreach (Vector3 v in ring.vertices)
            {
                float fromAxis = new Vector2(v.x, v.z).magnitude;
                Assert.That(fromAxis, Is.InRange(0.13f - 0.0061f, 0.13f + 0.0061f));
                Assert.That(Mathf.Abs(v.y), Is.LessThanOrEqualTo(0.0061f));
            }

            Mesh halo = GlowMeshes.Halo(0.09f, 0.13f, 0.2f);
            Vector3[] vertices = halo.vertices;
            Color[] colors = halo.colors;
            for (int i = 0; i < vertices.Length; i++)
            {
                float r = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                float expected = Mathf.Approximately(r, 0.13f) ? 1f : 0f;
                Assert.That(colors[i].a, Is.EqualTo(expected).Within(1e-4f), "opaque only at the peak radius");
                Assert.That(vertices[i].y, Is.EqualTo(0f));
            }
            Object.DestroyImmediate(ring);
            Object.DestroyImmediate(halo);
        }
    }
}
