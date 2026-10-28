using GravBoots.UI;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class PanoramaShotTests
    {
        [Test]
        public void StaysInsideTheRoomAndLooksDownIt([Values(-1, 1)] int side)
        {
            // Rooms in the chamber run from 12 × 14 × 6 to 14 × 37 × 12.
            foreach (var room in new[] { new Vector4(-6f, 6f, 14f, 6f), new Vector4(-7f, 7f, 37f, 12f), new Vector4(-4f, 4f, 8f, 5f) })
            {
                var shot = PanoramaShot.ForRoom(room.x, room.y, room.z, room.w, side);
                for (float t = 0f; t <= 1f; t += 0.1f)
                {
                    shot.Sample(t, out Vector3 p, out Quaternion r);
                    Assert.That(p.x, Is.InRange(room.x + PanoramaShot.Margin - 1e-3f, room.y - PanoramaShot.Margin + 1e-3f));
                    Assert.That(p.y, Is.InRange(1f - 1e-3f, room.w - PanoramaShot.Margin + 1e-3f));
                    Assert.That(p.z, Is.InRange(0f, room.z - PanoramaShot.Margin + 1e-3f));
                    Vector3 forward = r * Vector3.forward;
                    Assert.That(forward.z, Is.GreaterThan(0.5f), "looks down the room");
                    Assert.That(forward.y, Is.LessThan(0f), "looks down from above");
                }
            }
        }

        [Test]
        public void SidesAlternate()
        {
            var left = PanoramaShot.ForRoom(-6f, 6f, 20f, 8f, -1);
            var right = PanoramaShot.ForRoom(-6f, 6f, 20f, 8f, 1);
            Assert.That(left.From.x, Is.LessThan(0f));
            Assert.That(right.From.x, Is.GreaterThan(0f));
        }
    }
}
