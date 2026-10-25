using System.Collections.Generic;
using GravBoots.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// The camera rig's obstacle avoidance: no sudden jumps, no sitting inside walls. The rig
    /// is stepped by hand at a fixed delta time while its target moves past geometry; these
    /// check the rig's own output, before any Cinemachine damping.
    /// </summary>
    public class CameraObstacleTests
    {
        const float Dt = 1f / 60f;
        const float WalkSpeed = 6f;

        readonly List<Object> created = new List<Object>();
        CameraSettings settings;
        Transform target;
        CameraRig rig;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<CameraSettings>();
            created.Add(settings);

            target = new GameObject("Target").transform;
            created.Add(target.gameObject);

            var rigObject = new GameObject("Rig");
            created.Add(rigObject);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(rigObject.transform, false);
            rig = rigObject.AddComponent<CameraRig>();
            rig.Configure(target, pivot, settings, ~0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        void Box(Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Box");
            created.Add(go);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
        }

        /// <summary>Moves the target at walking speed and steps the rig, recording each frame.</summary>
        List<(float distance, float shoulder, Vector3 camera, Vector3 hand)> Walk(Vector3 direction, float seconds)
        {
            var frames = new List<(float, float, Vector3, Vector3)>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                target.position += direction * (WalkSpeed * Dt);
                rig.Tick(Dt);
                frames.Add((rig.CurrentDistance, rig.ShoulderScale, rig.CameraPosition, rig.HandPosition));
            }
            return frames;
        }

        static bool Inside(Vector3 point) => Physics.CheckSphere(point, 0.05f, ~0, QueryTriggerInteraction.Ignore);

        [Test]
        public void PillarPassingBehindPullsInSmoothly()
        {
            // A 1 m pillar 2 m behind the player's path; the player strafes past it.
            Box(new Vector3(0f, 1.5f, -2f), new Vector3(1f, 3f, 1f));
            target.position = new Vector3(-5f, 0f, 0f);
            rig.SnapBehindTarget();

            var frames = Walk(Vector3.right, 10f / WalkSpeed);

            float maxPullIn = settings.pullInSpeed * Dt + 1e-4f;
            for (int i = 1; i < frames.Count; i++)
                Assert.That(frames[i - 1].distance - frames[i].distance, Is.LessThanOrEqualTo(maxPullIn), $"frame {i}");
            Assert.That(frames[frames.Count - 1].distance, Is.EqualTo(settings.cameraDistance).Within(0.3f),
                "camera should ease back out after the pillar has passed");
        }

        [Test]
        public void BackingIntoWallNeverPutsCameraInsideIt()
        {
            // Wall behind; walking backward toward it is slower than the pull-in cap, so the
            // camera should track it exactly.
            Box(new Vector3(0f, 2f, -4.5f), new Vector3(10f, 4f, 1f));
            target.position = Vector3.zero;
            rig.SnapBehindTarget();

            var frames = Walk(Vector3.back, 3.5f / WalkSpeed);

            for (int i = 0; i < frames.Count; i++)
                Assert.That(Inside(frames[i].camera), Is.False, $"camera inside wall at frame {i}");
            Assert.That(frames[frames.Count - 1].distance, Is.LessThan(settings.cameraDistance - 1f));
        }

        [Test]
        public void ShoulderTucksInWithoutJumpingWhenHuggingWall()
        {
            // Wall on the shoulder side; the player drifts toward it while walking forward.
            Box(new Vector3(1.6f, 2f, 0f), new Vector3(0.5f, 4f, 40f));
            target.position = new Vector3(0f, 0f, -10f);
            rig.SnapBehindTarget();

            var drift = (Vector3.forward + Vector3.right * 0.15f).normalized;
            var frames = Walk(drift, 1.0f);

            float maxRetract = settings.shoulderRetractPerSecond * Dt + 1e-4f;
            for (int i = 1; i < frames.Count; i++)
            {
                Assert.That(frames[i - 1].shoulder - frames[i].shoulder, Is.LessThanOrEqualTo(maxRetract), $"frame {i}");
                Assert.That(Inside(frames[i].hand), Is.False, $"shoulder inside wall at frame {i}");
            }
            Assert.That(frames[frames.Count - 1].shoulder, Is.LessThan(0.9f), "shoulder should have tucked in");
        }
    }
}
