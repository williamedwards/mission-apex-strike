using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class PlayerAnimationTests
    {
        static readonly Vector3 Times = new Vector3(0.1f, 0.3f, 0.4f);

        [Test]
        public void AirPoseFollowsTheJumpArc()
        {
            Assert.That(PlayerAnimation.AirPoseTime(8f, 8f, Times), Is.EqualTo(0.1f).Within(1e-5f), "takeoff");
            Assert.That(PlayerAnimation.AirPoseTime(4f, 8f, Times), Is.EqualTo(0.2f).Within(1e-5f), "rising");
            Assert.That(PlayerAnimation.AirPoseTime(0f, 8f, Times), Is.EqualTo(0.3f).Within(1e-5f), "peak");
            Assert.That(PlayerAnimation.AirPoseTime(-4f, 8f, Times), Is.EqualTo(0.35f).Within(1e-5f), "falling");
        }

        [Test]
        public void AirPoseHoldsAtTheEndsBeyondJumpSpeed()
        {
            Assert.That(PlayerAnimation.AirPoseTime(20f, 8f, Times), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(PlayerAnimation.AirPoseTime(-30f, 8f, Times), Is.EqualTo(0.4f).Within(1e-5f));
        }
    }
}
