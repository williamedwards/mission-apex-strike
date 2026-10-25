using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class MoveInputMapperTests
    {
        static void AssertDirection(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Angle(expected, actual), Is.LessThan(0.5f), $"expected {expected} but was {actual}");
        }

        [Test]
        public void MapsRelativeToCameraOnFloor()
        {
            var mapper = new MoveInputMapper();
            Vector3 move = mapper.Map(Vector2.up, new Vector3(0f, -0.3f, 1f), Vector3.up, Vector3.up);
            AssertDirection(Vector3.forward, move);
            move = mapper.Map(Vector2.right, new Vector3(0f, -0.3f, 1f), Vector3.up, Vector3.up);
            AssertDirection(Vector3.right, move);
        }

        [Test]
        public void HeldForwardIntoWallBecomesUpTheWall()
        {
            var mapper = new MoveInputMapper();
            mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.up);

            // The player has turned onto the wall in front (its normal points back at us);
            // the camera hasn't caught up and still looks along +Z with world up.
            Vector3 move = mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.back);

            AssertDirection(Vector3.up, move);
            Assert.That(mapper.IsHoldingMapping, Is.True);
        }

        [Test]
        public void ReleasingStickReturnsToCameraMapping()
        {
            var mapper = new MoveInputMapper();
            mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.up);
            mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.back);
            mapper.Map(Vector2.zero, Vector3.forward, Vector3.up, Vector3.back);

            // Camera has now rolled onto the wall: looking up it, with up = wall normal.
            Vector3 move = mapper.Map(Vector2.right, Vector3.up, Vector3.back, Vector3.back);
            Assert.That(mapper.IsHoldingMapping, Is.False);
            AssertDirection(Vector3.Cross(Vector3.back, Vector3.up), move);
        }

        [Test]
        public void TurningStickPastBreakAngleEndsHold()
        {
            var mapper = new MoveInputMapper();
            mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.up);
            mapper.Map(Vector2.up, Vector3.forward, Vector3.up, Vector3.back);

            mapper.Map(Vector2.right, Vector3.forward, Vector3.up, Vector3.back);
            Assert.That(mapper.IsHoldingMapping, Is.False);
        }
    }
}
