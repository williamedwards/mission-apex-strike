using GravBoots.Gravity;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class SurfaceAlignmentTests
    {
        const float Tolerance = 1e-4f;

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance),
                $"expected {expected} but was {actual}");
        }

        [Test]
        public void FloorKeepsForward()
        {
            Quaternion r = SurfaceAlignment.AlignToSurface(Vector3.forward, Vector3.up);
            AssertVector(Vector3.up, r * Vector3.up);
            AssertVector(Vector3.forward, r * Vector3.forward);
        }

        [Test]
        public void WalkingIntoWallFacesUpTheWall()
        {
            // Wall in front of the player: its normal points back at them.
            Quaternion r = SurfaceAlignment.AlignToSurface(Vector3.forward, Vector3.back);
            AssertVector(Vector3.back, r * Vector3.up);
            AssertVector(Vector3.up, r * Vector3.forward);
        }

        [Test]
        public void CeilingFlipsUpAndKeepsHeading()
        {
            Quaternion r = SurfaceAlignment.AlignToSurface(Vector3.right, Vector3.down);
            AssertVector(Vector3.down, r * Vector3.up);
            AssertVector(Vector3.right, r * Vector3.forward);
        }

        static Quaternion RightUntil(Quaternion start, float degreesPerStep, int maxSteps)
        {
            Quaternion r = start;
            for (int i = 0; i < maxSteps; i++)
                r = SurfaceAlignment.RotateUpToward(r, Vector3.up, degreesPerStep);
            return r;
        }

        [Test]
        public void DropFromCeilingRollsAndKeepsHeading()
        {
            Quaternion onCeiling = Quaternion.LookRotation(Vector3.right, Vector3.down);
            Quaternion r = RightUntil(onCeiling, 12f, 15);
            AssertVector(Vector3.up, r * Vector3.up);
            AssertVector(Vector3.right, r * Vector3.forward);
        }

        [Test]
        public void DropFromCeilingStaysHeadingLevelMidFlip()
        {
            // A roll keeps the forward axis fixed for the whole flip; a pitch would swing it
            // through vertical and reverse it.
            Quaternion onCeiling = Quaternion.LookRotation(Vector3.right, Vector3.down);
            Quaternion r = RightUntil(onCeiling, 12f, 7);
            AssertVector(Vector3.right, r * Vector3.forward);
        }

        [Test]
        public void DropFromWallWhileWalkingSidewaysKeepsHeading()
        {
            Quaternion onWall = Quaternion.LookRotation(Vector3.right, Vector3.back);
            Quaternion r = RightUntil(onWall, 12f, 10);
            AssertVector(Vector3.up, r * Vector3.up);
            AssertVector(Vector3.right, r * Vector3.forward);
        }

        [Test]
        public void RotateUpTowardRespectsMaxDegrees()
        {
            Quaternion onWall = Quaternion.LookRotation(Vector3.up, Vector3.back);
            Quaternion r = SurfaceAlignment.RotateUpToward(onWall, Vector3.up, 30f);
            Assert.That(Vector3.Angle(r * Vector3.up, Vector3.up), Is.EqualTo(60f).Within(1e-3f));
        }

        [Test]
        public void RotateUpTowardIsNoOpWhenAligned()
        {
            Quaternion upright = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Assert.That(SurfaceAlignment.RotateUpToward(upright, Vector3.up, 30f), Is.EqualTo(upright));
        }
    }
}
