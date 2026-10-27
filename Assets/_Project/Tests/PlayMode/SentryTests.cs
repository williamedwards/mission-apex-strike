using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Hazards;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// The plane sentry: it kills in its plane, can't see out of it, is blocked by walls, only
    /// notices you in its searchlight, and dies for good when dropped on. Built in a rotated
    /// local frame (gravity along the frame's down) and stepped by hand.
    /// </summary>
    public class SentryTests
    {
        const float Dt = 1f / 60f;

        static readonly Vector3[] Ups =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
        };

        static IEnumerable<Vector3> Orientations() => Ups;

        readonly List<Object> created = new List<Object>();
        Transform frame;
        PlayerMotor motor;
        Health health;
        Sentry sentry;
        MoveInputMapper mapper;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        void BuildFrame(Vector3 up)
        {
            var root = new GameObject("TestFrame");
            created.Add(root);
            root.transform.rotation = up == Vector3.down
                ? Quaternion.Euler(180f, 0f, 0f)
                : Quaternion.FromToRotation(Vector3.up, up);
            frame = root.transform;
        }

        void Box(Vector3 localCentre, Vector3 size)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<BoxCollider>().size = size;
        }

        void Floor() => Box(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

        void SpawnPlayer(Vector3 localPosition)
        {
            var go = new GameObject("Player");
            created.Add(go);
            go.layer = Layers.Player;
            go.transform.SetPositionAndRotation(frame.TransformPoint(localPosition), frame.rotation);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<GravityBody>().SetUp(frame.up);
            motor = go.AddComponent<PlayerMotor>();
            motor.AutoSimulate = false;
            motor.Settings = ScriptableObject.CreateInstance<MotorSettings>();
            motor.CollisionMask = ~(1 << Layers.Player);
            created.Add(motor.Settings);
            health = go.AddComponent<Health>();
            mapper = new MoveInputMapper();
            Physics.SyncTransforms();
        }

        /// <summary>A sentry on the floor at <paramref name="localPosition"/>, first looking along <paramref name="localForward"/>, with a solid body up to 1.3 m.</summary>
        void PlaceSentry(Vector3 localPosition, Vector3 localForward, float sweepSpeed = 0f)
        {
            var go = new GameObject("Sentry");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(localForward, Vector3.up);
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.AddComponent<BoxCollider>().size = new Vector3(0.9f, 1.3f, 0.9f);
            sentry = go.AddComponent<Sentry>();
            sentry.AutoSimulate = false;
            sentry.Target = motor;
            sentry.SweepDegreesPerSecond = sweepSpeed;
            Physics.SyncTransforms();
        }

        void Run(float seconds, Vector2 stick = default)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                motor.SetMoveInput(mapper.Map(stick, frame.forward, frame.up, motor.Gravity.Up));
                motor.Tick(Dt);
                sentry.Tick(Dt);
            }
        }

        float TimeToShot => sentry.LockTime + sentry.ChargeTime;

        [Test]
        public void KillsAPlayerInItsPlaneAndSight([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 8f));
            PlaceSentry(Vector3.zero, Vector3.forward);
            DeathCause? cause = null;
            health.Died += c => cause = c;

            Run(0.1f);
            Assert.That(sentry.State, Is.EqualTo(SentryState.Lock));
            Assert.That(health.IsDead, Is.False, "warning before the shot");
            Run(TimeToShot + 0.1f);

            Assert.That(health.IsDead, Is.True);
            Assert.That(cause, Is.EqualTo(DeathCause.Laser));
        }

        [Test]
        public void CannotSeeAPlayerAboveItsPlane([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            // A platform 2.5 m up puts the player's body wholly above the 1 m plane.
            Box(new Vector3(0f, 2.25f, 8f), new Vector3(4f, 0.5f, 4f));
            SpawnPlayer(new Vector3(0f, 2.5f, 8f));
            PlaceSentry(Vector3.zero, Vector3.forward);

            Run(3f);

            Assert.That(sentry.State, Is.EqualTo(SentryState.Sweep));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void WallBlocksItsSight([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Box(new Vector3(0f, 1.5f, 4f), new Vector3(6f, 3f, 0.5f));
            SpawnPlayer(new Vector3(0f, 0f, 8f));
            PlaceSentry(Vector3.zero, Vector3.forward);

            Run(3f);

            Assert.That(sentry.State, Is.EqualTo(SentryState.Sweep));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void OnlyNoticesThePlayerInItsSearchlight([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, -8f));
            // Looking away and not sweeping: the player behind it is never noticed.
            PlaceSentry(Vector3.zero, Vector3.forward);
            Run(2f);
            Assert.That(sentry.State, Is.EqualTo(SentryState.Sweep));

            // Sweeping round at 90°/s, it finds them within the half-turn.
            sentry.SweepDegreesPerSecond = 90f;
            Run(2.2f);
            Assert.That(sentry.State, Is.Not.EqualTo(SentryState.Sweep));
        }

        [Test]
        public void SteppingAsideDuringTheAimLockDodges([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            PlaceSentry(Vector3.zero, Vector3.forward);
            bool? hit = null;
            sentry.Fired += h => hit = h;

            Run(TimeToShot - sentry.AimLockTime + Dt);
            Assert.That(sentry.State, Is.EqualTo(SentryState.Charge));
            Run(sentry.AimLockTime + 0.05f, Vector2.right);

            Assert.That(hit, Is.False);
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void DroppingOntoItDestroysItForGood([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            PlaceSentry(Vector3.zero, Vector3.forward, 60f);
            // Falling from 4 m above its head.
            SpawnPlayer(new Vector3(0f, 5.3f, 0f));
            sentry.Target = motor;
            bool destroyed = false;
            sentry.Destroyed += () => destroyed = true;

            Run(1f);

            Assert.That(destroyed, Is.True);
            Assert.That(sentry.IsDisabled, Is.True);
            Assert.That(health.IsDead, Is.False);

            // Back on the floor in front of it: it stays dead.
            motor.Teleport(frame.TransformPoint(new Vector3(0f, 0f, 6f)), frame.rotation);
            Run(3f);
            Assert.That(sentry.State, Is.EqualTo(SentryState.Disabled));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void WalkingOntoItIsNotAStomp([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            // A ledge level with its head, leading onto it from behind.
            Box(new Vector3(0f, 0.65f, -3f), new Vector3(2f, 1.3f, 4f));
            PlaceSentry(Vector3.zero, Vector3.forward);
            SpawnPlayer(new Vector3(0f, 1.3f, -2.5f));
            sentry.Target = motor;

            Run(0.6f, Vector2.up);

            Assert.That(sentry.IsDisabled, Is.False);
        }
    }
}
