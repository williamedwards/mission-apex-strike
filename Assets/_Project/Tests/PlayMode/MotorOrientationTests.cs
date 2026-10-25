using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// Movement promises from the Phase 1A exit check, at all six orientations. Each test
    /// builds its geometry in a local frame, rotates the whole thing so the floor faces the
    /// chosen up, and steps the motor by hand with a fixed delta time.
    /// </summary>
    public class MotorOrientationTests
    {
        const float Dt = 1f / 60f;

        static readonly Vector3[] Ups =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
        };

        static IEnumerable<Vector3> Orientations() => Ups;

        readonly List<Object> created = new List<Object>();
        Transform frame;
        MotorSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MotorSettings>();
            created.Add(settings);
        }

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
            // FromToRotation is ambiguous for straight down; any valid frame will do.
            root.transform.rotation = up == Vector3.down
                ? Quaternion.Euler(180f, 0f, 0f)
                : Quaternion.FromToRotation(Vector3.up, up);
            frame = root.transform;
        }

        void Box(Vector3 localCentre, Vector3 size, Quaternion localRotation)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = localRotation;
            go.AddComponent<BoxCollider>().size = size;
        }

        void Floor() => Box(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), Quaternion.identity);

        /// <summary>A ramp rising along local +Z whose surface starts at <paramref name="foot"/>.</summary>
        void Ramp(Vector3 foot, float angle, float length = 10f)
        {
            Quaternion r = Quaternion.Euler(-angle, 0f, 0f);
            Box(foot + r * new Vector3(0f, -0.25f, length * 0.5f), new Vector3(4f, 0.5f, length), r);
        }

        PlayerMotor SpawnPlayer(Vector3 localPosition)
        {
            var go = new GameObject("Player");
            created.Add(go);
            go.transform.SetPositionAndRotation(frame.TransformPoint(localPosition), frame.rotation);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<GravityBody>().SetUp(frame.up);
            var motor = go.AddComponent<PlayerMotor>();
            motor.AutoSimulate = false;
            motor.Settings = settings;
            Physics.SyncTransforms();
            return motor;
        }

        static void Run(PlayerMotor motor, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
                motor.Tick(Dt);
        }

        float Height(PlayerMotor motor) => frame.InverseTransformPoint(motor.Position).y;

        [Test]
        public void StandsOnFlatFloor([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            PlayerMotor motor = SpawnPlayer(new Vector3(0f, 0.5f, 0f));

            Run(motor, 1f);

            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(Height(motor), Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void WalksForwardOnFloor([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            PlayerMotor motor = SpawnPlayer(Vector3.zero);
            Run(motor, 0.2f);

            motor.SetMoveInput(frame.forward);
            Run(motor, 1f);

            Vector3 local = frame.InverseTransformPoint(motor.Position);
            Assert.That(local.z, Is.GreaterThan(settings.moveSpeed * 0.8f));
            Assert.That(Mathf.Abs(local.x), Is.LessThan(0.05f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [Test]
        public void StaysPutOn30DegreeRamp([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Ramp(new Vector3(0f, 0f, -5f), 30f);
            // Above the middle of the ramp.
            PlayerMotor motor = SpawnPlayer(new Vector3(0f, 3.5f, 0f));
            Run(motor, 1f);
            Vector3 settled = motor.Position;

            Run(motor, 2f);

            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(Vector3.Distance(settled, motor.Position), Is.LessThan(0.02f));
            Assert.That(Vector3.Angle(motor.GroundNormal, frame.up), Is.EqualTo(30f).Within(1f));
        }

        [Test]
        public void WalksUp35DegreeRamp([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Ramp(new Vector3(0f, 0f, 2f), 35f);
            PlayerMotor motor = SpawnPlayer(Vector3.zero);
            Run(motor, 0.2f);

            // Short enough that we're still on the ramp, not off the top of it.
            motor.SetMoveInput(frame.forward);
            Run(motor, 1.5f);

            Assert.That(Height(motor), Is.GreaterThan(2f));
        }

        [Test]
        public void CannotClimb60DegreeRamp([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Ramp(new Vector3(0f, 0f, 2f), 60f);
            PlayerMotor motor = SpawnPlayer(Vector3.zero);
            Run(motor, 0.2f);

            motor.SetMoveInput(frame.forward);
            Run(motor, 3f);

            Assert.That(Height(motor), Is.LessThan(0.5f));
        }

        [Test]
        public void StepsUpLowLedge([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Box(new Vector3(0f, 0.15f, 4f), new Vector3(4f, 0.3f, 4f), Quaternion.identity);
            PlayerMotor motor = SpawnPlayer(Vector3.zero);
            Run(motor, 0.2f);

            motor.SetMoveInput(frame.forward);
            Run(motor, 1f);

            Assert.That(Height(motor), Is.EqualTo(0.3f).Within(0.05f));
            Assert.That(frame.InverseTransformPoint(motor.Position).z, Is.GreaterThan(3f));
        }

        [Test]
        public void JumpReachesJumpHeight([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            PlayerMotor motor = SpawnPlayer(Vector3.zero);
            Run(motor, 0.2f);

            motor.RequestJump();
            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                motor.Tick(Dt);
                peak = Mathf.Max(peak, Height(motor));
            }

            Assert.That(peak, Is.EqualTo(settings.jumpHeight).Within(0.15f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [Test]
        public void TurnsToNewUpAndLandsOnWall()
        {
            // Floor plus a wall at +X; switching up to point away from the wall makes the
            // player fall sideways onto it and stand on it.
            BuildFrame(Vector3.up);
            Floor();
            Box(new Vector3(5.5f, 5f, 0f), new Vector3(1f, 10f, 10f), Quaternion.identity);
            PlayerMotor motor = SpawnPlayer(new Vector3(0f, 0f, 0f));
            Run(motor, 0.2f);

            motor.Gravity.SetUp(Vector3.left);
            Run(motor, 2f);

            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(Vector3.Angle(motor.BodyUp, Vector3.left), Is.LessThan(1f));
            Assert.That(motor.Position.x, Is.EqualTo(5f).Within(0.05f));
        }
    }
}
