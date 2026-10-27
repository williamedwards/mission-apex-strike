using GravBoots.Level;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class MoverPathTests
    {
        const float Length = 10f, Speed = 2f, Wait = 1f;
        // Travel takes 5 s; a ping-pong period is 2 × (5 + 1) = 12 s.

        [Test]
        public void PingPongWaitsTravelsWaitsAndReturns()
        {
            Assert.That(MoverPath.Distance(0.5f, Length, Speed, Wait, MoverMode.PingPong), Is.EqualTo(0f));
            Assert.That(MoverPath.Distance(1f + 2.5f, Length, Speed, Wait, MoverMode.PingPong), Is.EqualTo(5f).Within(1e-3f));
            Assert.That(MoverPath.Distance(6.5f, Length, Speed, Wait, MoverMode.PingPong), Is.EqualTo(Length));
            Assert.That(MoverPath.Distance(7f + 2.5f, Length, Speed, Wait, MoverMode.PingPong), Is.EqualTo(5f).Within(1e-3f));
            Assert.That(MoverPath.Distance(12.5f, Length, Speed, Wait, MoverMode.PingPong), Is.EqualTo(0f));
        }

        [Test]
        public void PingPongEasesInAndOut()
        {
            float early = MoverPath.Distance(1f + 0.25f, Length, Speed, Wait, MoverMode.PingPong);
            Assert.That(early, Is.LessThan(0.25f * Speed), "starts slower than the average speed");
            float middle = MoverPath.Distance(1f + 2.75f, Length, Speed, Wait, MoverMode.PingPong)
                - MoverPath.Distance(1f + 2.25f, Length, Speed, Wait, MoverMode.PingPong);
            Assert.That(middle, Is.GreaterThan(0.5f * Speed), "faster than average in the middle");
        }

        [Test]
        public void LoopRunsAtConstantSpeedAfterItsPause()
        {
            Assert.That(MoverPath.Distance(0.5f, Length, Speed, Wait, MoverMode.Loop), Is.EqualTo(0f));
            Assert.That(MoverPath.Distance(2f, Length, Speed, Wait, MoverMode.Loop), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(MoverPath.Distance(3f, Length, Speed, Wait, MoverMode.Loop), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(MoverPath.Distance(6.5f, Length, Speed, Wait, MoverMode.Loop), Is.EqualTo(0f), "back at the start, pausing");
        }

        [Test]
        public void OnStepTravelsOnceAndStays()
        {
            Assert.That(MoverPath.Distance(0f, Length, Speed, Wait, MoverMode.OnStep), Is.EqualTo(0f));
            Assert.That(MoverPath.Distance(2.5f, Length, Speed, Wait, MoverMode.OnStep), Is.EqualTo(5f).Within(1e-3f));
            Assert.That(MoverPath.Distance(60f, Length, Speed, Wait, MoverMode.OnStep), Is.EqualTo(Length));
        }

        [Test]
        public void SampleInterpolatesAlongSegments()
        {
            var positions = new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 6f) };
            var rotations = new[] { Quaternion.identity, Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) };
            float[] cumulative = MoverPath.Cumulative(positions, rotations, false);
            Assert.That(cumulative[2], Is.EqualTo(10f).Within(1e-4f));

            MoverPath.Sample(positions, rotations, cumulative, 2f, out Vector3 p, out _);
            Assert.That(Vector3.Distance(p, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-4f));
            MoverPath.Sample(positions, rotations, cumulative, 7f, out p, out Quaternion r);
            Assert.That(Vector3.Distance(p, new Vector3(4f, 0f, 3f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(r, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.1f));
        }

        [Test]
        public void ClosedPathIncludesTheWayBack()
        {
            var positions = new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f) };
            var rotations = new[] { Quaternion.identity, Quaternion.identity, Quaternion.identity };
            float[] cumulative = MoverPath.Cumulative(positions, rotations, true);
            Assert.That(cumulative[cumulative.Length - 1], Is.EqualTo(12f).Within(1e-4f));
            MoverPath.Sample(positions, rotations, cumulative, 9.5f, out Vector3 p, out _);
            Assert.That(Vector3.Distance(p, new Vector3(2f, 0f, 1.5f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TurningOnTheSpotHasALength()
        {
            var positions = new[] { Vector3.zero, Vector3.zero };
            var rotations = new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) };
            float[] cumulative = MoverPath.Cumulative(positions, rotations, false);
            Assert.That(cumulative[1], Is.EqualTo(Mathf.PI * 0.5f).Within(1e-3f));
        }
    }
}
