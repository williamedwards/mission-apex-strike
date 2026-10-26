using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Level;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// The Phase 1B exit checks for the magnetic boots: orientation changes only along curved
    /// surfaces, and sharp joins are impassable. Geometry is built in a local frame
    /// (rotated for the orientation tests); the motor is stepped by hand, and stick input goes
    /// through <see cref="MoveInputMapper"/> with a camera that never moves, so a held stick
    /// keeps its meaning through corners the way it does in game.
    /// </summary>
    public class MagneticBootsTests
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
        MagneticBoots boots;
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

        GameObject Box(Vector3 localCentre, Vector3 size, bool magnetic, Quaternion? localRotation = null)
        {
            var go = new GameObject(magnetic ? "Magnetic" : "Plain");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.AddComponent<BoxCollider>().size = size;
            if (magnetic)
                go.layer = Layers.Magnetic;
            return go;
        }

        /// <summary>
        /// A 90° curve starting at <paramref name="localStart"/> on a surface facing
        /// <paramref name="localUp"/>, heading along <paramref name="localForward"/>.
        /// </summary>
        void Curve(Vector3 localStart, Vector3 localForward, Vector3 localUp, CurvedRamp.Shape shape, float radius, float width)
        {
            var go = new GameObject("Curve");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localStart;
            go.transform.localRotation = Quaternion.LookRotation(localForward, localUp);
            go.layer = Layers.Magnetic;
            var ramp = go.AddComponent<CurvedRamp>();
            ramp.shape = shape;
            ramp.radius = radius;
            ramp.width = width;
            ramp.segments = 12;
            ramp.Rebuild();
        }

        void Floor(bool magnetic) => Box(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), magnetic);

        void SpawnPlayer(Vector3 localPosition, Quaternion? localRotation = null, Vector3? localUp = null)
        {
            var go = new GameObject("Player");
            created.Add(go);
            go.layer = Layers.Player;
            Quaternion rotation = frame.rotation * (localRotation ?? Quaternion.identity);
            go.transform.SetPositionAndRotation(frame.TransformPoint(localPosition), rotation);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<GravityBody>().SetUp(frame.TransformDirection(localUp ?? Vector3.up));
            motor = go.AddComponent<PlayerMotor>();
            motor.AutoSimulate = false;
            motor.Settings = ScriptableObject.CreateInstance<MotorSettings>();
            motor.CollisionMask = ~(1 << Layers.Player);
            created.Add(motor.Settings);
            boots = go.AddComponent<MagneticBoots>();
            boots.Settings = ScriptableObject.CreateInstance<BootsSettings>();
            created.Add(boots.Settings);
            mapper = new MoveInputMapper();
            Physics.SyncTransforms();
        }

        /// <summary>Steps with the stick held; the "camera" looks along the frame's forward.</summary>
        void Run(float seconds, Vector2 stick = default)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                motor.SetMoveInput(mapper.Map(stick, frame.forward, frame.up, motor.Gravity.Up));
                motor.Tick(Dt);
            }
        }

        void Activate()
        {
            Run(0.3f);
            boots.RequestToggle();
            Run(Dt);
            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized), "boots should switch on");
        }

        Vector3 Local(Vector3 world) => frame.InverseTransformPoint(world);
        Vector3 LocalDir(Vector3 world) => frame.InverseTransformDirection(world);

        static void AssertDirection(Vector3 expected, Vector3 actual, float degrees, string what)
        {
            Assert.That(Vector3.Angle(expected, actual), Is.LessThan(degrees), $"{what}: expected {expected} but was {actual}");
        }

        [Test]
        public void SwitchesOnOnlyOnMagneticGround()
        {
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            SpawnPlayer(Vector3.zero);
            bool refused = false;
            boots.ActivationRefused += () => refused = true;

            Run(0.3f);
            boots.RequestToggle();
            Run(0.1f);

            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            Assert.That(refused, Is.True);
        }

        [Test]
        public void QuarterPipeCarriesYouUpOntoWall([ValueSource(nameof(Orientations))] Vector3 up)
        {
            // Floor curving up (radius 2) into a wall whose face is at z = 3.5.
            BuildFrame(up);
            Floor(magnetic: true);
            Curve(new Vector3(0f, 0f, 1.5f), Vector3.forward, Vector3.up, CurvedRamp.Shape.Concave, 2f, 10f);
            Box(new Vector3(0f, 6f, 4f), new Vector3(10f, 8f, 1f), magnetic: true);
            SpawnPlayer(new Vector3(0f, 0f, -1f));
            Activate();

            // Long enough to be well up the 10 m wall, not so long as to reach its top.
            Run(1.6f, Vector2.up);

            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));
            Assert.That(motor.IsGrounded, Is.True);
            AssertDirection(Vector3.back, LocalDir(motor.Gravity.Up), 2f, "up should be the wall's normal");
            AssertDirection(Vector3.back, LocalDir(motor.BodyUp), 5f, "body should stand on the wall");
            Assert.That(Local(motor.Position).y, Is.GreaterThan(4f), "should have walked up the wall");
        }

        [Test]
        public void RightAngleJoinIsImpassable()
        {
            // A magnetic wall meeting a magnetic floor at 90°, with no curve: the boots can't take it.
            BuildFrame(Vector3.up);
            Floor(magnetic: true);
            Box(new Vector3(0f, 5f, 4f), new Vector3(10f, 10f, 1f), magnetic: true);
            SpawnPlayer(Vector3.zero);
            Activate();

            Run(1.5f, Vector2.up);

            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));
            AssertDirection(Vector3.up, motor.Gravity.Up, 1f, "up");
            Assert.That(motor.Position.y, Is.LessThan(0.1f));
            Assert.That(motor.Position.z, Is.GreaterThan(3f), "should be pressed against the wall");
        }

        [Test]
        public void RoundedEdgeCarriesYouOverAndDown([ValueSource(nameof(Orientations))] Vector3 up)
        {
            // A magnetic block 4 m tall whose +Z top edge is rounded (radius 1.5); its front face is at z = 2.
            BuildFrame(up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 3.25f, -1.75f), new Vector3(6f, 1.5f, 4.5f), magnetic: true);
            Box(new Vector3(0f, 1.25f, -1f), new Vector3(6f, 2.5f, 6f), magnetic: true);
            Curve(new Vector3(0f, 4f, 0.5f), Vector3.forward, Vector3.up, CurvedRamp.Shape.Convex, 1.5f, 6f);
            SpawnPlayer(new Vector3(0f, 4.02f, -2f));
            Activate();

            Run(1.5f, Vector2.up);

            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized), "should still be attached");
            Assert.That(motor.IsGrounded, Is.True);
            AssertDirection(Vector3.forward, LocalDir(motor.Gravity.Up), 2f, "up should be the front face's normal");
            Assert.That(Local(motor.Position).y, Is.LessThan(2.5f), "should be walking down the front face");
            Assert.That(Local(motor.Position).z, Is.EqualTo(2.02f).Within(0.1f), "feet on the front face");
        }

        [Test]
        public void SharpEdgeOfBlockTopLetsGo()
        {
            // Walking off the sharp top edge of a magnetic block: no curve, so the boots let go.
            // Up was already world up, so they simply switch off and you fall.
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 2f, 0f), new Vector3(4f, 4f, 4f), magnetic: true);
            SpawnPlayer(new Vector3(0f, 4.02f, 0f));
            Activate();

            Run(2f, Vector2.up);

            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            AssertDirection(Vector3.up, motor.Gravity.Up, 0.1f, "never wrapped round the edge");
            AssertDirection(Vector3.up, motor.BodyUp, 1f, "landed upright");
            Assert.That(motor.Position.y, Is.LessThan(0.1f), "on the floor");
        }

        [Test]
        public void SharpEdgeAtTopOfWallDrops()
        {
            // On a magnetic wall 4 m tall, walking up to its sharp top edge: the boots must not
            // roll you over onto the top, they drop you.
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 2f, 3.5f), new Vector3(10f, 4f, 1f), magnetic: true);
            SpawnPlayer(new Vector3(0f, 1f, 2.98f), Quaternion.LookRotation(Vector3.up, Vector3.back), Vector3.back);
            Activate();

            bool wrapped = false;
            BootsState? sawDrop = null;
            boots.StateChanged += (from, to) => { if (to == BootsState.Dropping) sawDrop = to; };
            for (int i = 0; i < 120; i++)
            {
                Run(Dt, Vector2.up);
                if (boots.IsMagnetized && Vector3.Angle(motor.Gravity.Up, Vector3.up) < 60f)
                    wrapped = true;
            }

            Assert.That(wrapped, Is.False, "Up must never turn toward the wall's top while magnetized");
            Assert.That(sawDrop, Is.EqualTo(BootsState.Dropping), "should have let go at the edge");
        }

        [Test]
        public void DroppingFromCeilingLandsUprightKeepingHeading()
        {
            // Magnetic ceiling 4 m up; the player hangs from it facing +X.
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 4.5f, 0f), new Vector3(20f, 1f, 20f), magnetic: true);
            SpawnPlayer(new Vector3(0f, 3.98f, 0f), Quaternion.LookRotation(Vector3.right, Vector3.down), Vector3.down);
            Activate();

            boots.RequestToggle();
            Run(Dt);
            Assert.That(boots.State, Is.EqualTo(BootsState.Dropping));
            AssertDirection(Vector3.up, motor.Gravity.Up, 0.1f, "world gravity from the very first step");

            Run(2f);

            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            Assert.That(motor.IsGrounded, Is.True);
            AssertDirection(Vector3.up, motor.BodyUp, 1f, "landed upright");
            AssertDirection(Vector3.right, motor.Rotation * Vector3.forward, 2f, "heading kept");
            Assert.That(motor.Position.y, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void WalkingOntoNonMagneticSectionDrops()
        {
            // Wall magnetic up to 4 m, plain above; the player starts on it at 1 m and walks up.
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 2f, 3.5f), new Vector3(10f, 4f, 1f), magnetic: true);
            Box(new Vector3(0f, 8f, 3.5f), new Vector3(10f, 8f, 1f), magnetic: false);
            SpawnPlayer(new Vector3(0f, 1f, 2.98f), Quaternion.LookRotation(Vector3.up, Vector3.back), Vector3.back);
            Activate();

            BootsState? sawDrop = null;
            boots.StateChanged += (from, to) => { if (to == BootsState.Dropping) sawDrop = to; };
            Run(3f, Vector2.up);

            Assert.That(sawDrop, Is.EqualTo(BootsState.Dropping), "should have let go at the seam");
            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            AssertDirection(Vector3.up, motor.BodyUp, 1f, "back on the floor, upright");
            Assert.That(motor.Position.y, Is.LessThan(0.1f));
        }

        /// <summary>A jump press as the game routes it: the boots get first say.</summary>
        void PressJump()
        {
            if (!boots.HandleJump())
                motor.RequestJump();
        }

        void StandOnWall()
        {
            BuildFrame(Vector3.up);
            Floor(magnetic: false);
            Box(new Vector3(0f, 5f, 3.5f), new Vector3(10f, 10f, 1f), magnetic: true);
            // Standing on the wall at 4 m, up = wall normal.
            SpawnPlayer(new Vector3(0f, 4f, 2.98f), Quaternion.LookRotation(Vector3.up, Vector3.back), Vector3.back);
            Activate();
        }

        [Test]
        public void JumpIsBlockedWhileMagnetized()
        {
            StandOnWall();
            Vector3 before = motor.Position;

            PressJump();
            Run(1f);

            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(Vector3.Distance(before, motor.Position), Is.LessThan(0.05f), "should not have left the wall");
        }

        [Test]
        public void JumpLetsGoWhenConfigured()
        {
            StandOnWall();
            boots.Settings.jumpWhileMagnetized = MagnetizedJump.LetsGo;

            PressJump();
            Run(Dt);
            Assert.That(boots.State, Is.EqualTo(BootsState.Dropping), "jump should drop you");
            AssertDirection(Vector3.up, motor.Gravity.Up, 0.1f, "world gravity");

            Run(2f);
            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            AssertDirection(Vector3.up, motor.BodyUp, 1f, "landed upright");
        }

        [Test]
        public void JumpWorksNormallyWithBootsOff()
        {
            BuildFrame(Vector3.up);
            Floor(magnetic: true);
            SpawnPlayer(Vector3.zero);
            Run(0.3f);

            PressJump();
            float peak = 0f;
            for (int i = 0; i < 30; i++)
            {
                Run(Dt);
                peak = Mathf.Max(peak, motor.Position.y);
            }

            Assert.That(peak, Is.GreaterThan(1f));
        }
    }
}
