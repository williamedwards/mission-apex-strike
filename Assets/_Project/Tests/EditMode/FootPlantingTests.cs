using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class FootPlantingTests
    {
        static readonly Vector3[] Ups =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
        };

        [Test]
        public void AFootIsDownByHeightAlongTheBodysUp([ValueSource(nameof(Ups))] Vector3 up)
        {
            Vector3 basePosition = new Vector3(3f, -2f, 5f);
            Vector3 side = Vector3.Cross(up, up == Vector3.forward || up == Vector3.back ? Vector3.up : Vector3.forward);
            Assert.That(FootPlanting.IsDown(basePosition + up * 0.145f + side * 0.4f, basePosition, up, 0.17f), Is.True, "standing ankle");
            Assert.That(FootPlanting.IsDown(basePosition + up * 0.3f, basePosition, up, 0.17f), Is.False, "foot mid-swing");
        }

        [Test]
        public void APinnedFootLetsGoOnlyWhenPulledTooFar()
        {
            Assert.That(FootPlanting.Overstretched(Vector3.zero, new Vector3(0.2f, 0f, 0f), 0.25f), Is.False);
            Assert.That(FootPlanting.Overstretched(Vector3.zero, new Vector3(0.2f, 0f, 0.2f), 0.25f), Is.True);
        }
    }
}
