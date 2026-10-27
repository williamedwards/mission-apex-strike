using GravBoots.Hazards;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class BeamMathTests
    {
        [Test]
        public void ParallelSegmentsAreTheirGapApart()
        {
            float d = BeamMath.SegmentDistance(Vector3.zero, Vector3.right * 4f, Vector3.up * 2f, Vector3.up * 2f + Vector3.right * 4f);
            Assert.That(d, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void CrossingSegmentsTouch()
        {
            float d = BeamMath.SegmentDistance(Vector3.left, Vector3.right, Vector3.back, Vector3.forward);
            Assert.That(d, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DistanceIsToTheNearestEnd()
        {
            float d = BeamMath.SegmentDistance(Vector3.zero, Vector3.right, new Vector3(3f, 0f, -1f), new Vector3(3f, 0f, 1f));
            Assert.That(d, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void BeamHitsCapsuleInAnyOrientation([Values(0, 1, 2, 3, 4, 5)] int axis)
        {
            Vector3[] ups = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            Vector3 up = ups[axis];
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) > 0.5f ? Vector3.forward : Vector3.up).normalized;
            Vector3 chest = up * 1f;

            // A beam passing across the chest hits; the same beam above the head, or beside the body, misses.
            Assert.That(BeamMath.HitsCapsule(chest - side * 5f, chest + side * 5f, 0.05f, Vector3.zero, up, 1.8f, 0.35f), Is.True);
            Vector3 aboveHead = up * 2.3f;
            Assert.That(BeamMath.HitsCapsule(aboveHead - side * 5f, aboveHead + side * 5f, 0.05f, Vector3.zero, up, 1.8f, 0.35f), Is.False);
            Vector3 beside = side * 0.6f;
            Assert.That(BeamMath.HitsCapsule(beside - up * 3f, beside + up * 3f, 0.05f, Vector3.zero, up, 1.8f, 0.35f), Is.False);
        }

        [Test]
        public void StandingOnTheSameSurfaceIsInTheSlab()
        {
            bool inSlab = BeamMath.CapsuleInSlab(new Vector3(5f, 0f, 0f), Vector3.up, 1.8f, 0.35f,
                Vector3.up, Vector3.up, 0.15f, out Vector3 aim);
            Assert.That(inSlab, Is.True);
            Assert.That(Vector3.Distance(aim, new Vector3(5f, 1f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void StandingOnAWallIsInTheSlabOnlyNearItsLevel()
        {
            // The plane is 1 m up; the player stands sideways on a wall at x = 3, body along -X.
            bool low = BeamMath.CapsuleInSlab(new Vector3(3f, 1.2f, 0f), Vector3.left, 1.8f, 0.35f,
                Vector3.up, Vector3.up, 0.15f, out _);
            bool high = BeamMath.CapsuleInSlab(new Vector3(3f, 2f, 0f), Vector3.left, 1.8f, 0.35f,
                Vector3.up, Vector3.up, 0.15f, out _);
            Assert.That(low, Is.True);
            Assert.That(high, Is.False);
        }

        [Test]
        public void HangingFromTheCeilingAboveIsOutOfTheSlab()
        {
            bool inSlab = BeamMath.CapsuleInSlab(new Vector3(0f, 6f, 4f), Vector3.down, 1.8f, 0.35f,
                Vector3.up, Vector3.up, 0.15f, out _);
            Assert.That(inSlab, Is.False);
        }

        [Test]
        public void BeamStoppingShortMisses()
        {
            Vector3 chest = Vector3.up;
            Assert.That(BeamMath.HitsCapsule(chest + Vector3.back * 6f, chest + Vector3.back * 1f, 0.05f, Vector3.zero, Vector3.up, 1.8f, 0.35f), Is.False);
        }
    }
}
