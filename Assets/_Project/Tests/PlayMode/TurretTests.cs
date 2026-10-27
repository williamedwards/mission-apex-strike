using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Hazards;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// The placeholder turret: it shoots a player it can see, is blocked by walls, only sees in
    /// front of its mount, and its locked aim can be dodged. Built in a rotated local frame and
    /// stepped by hand, as in <see cref="GameLoopTests"/>.
    /// </summary>
    public class TurretTests
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
        Turret turret;
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

        /// <summary>A turret on the floor at <paramref name="localPosition"/>, resting aim along <paramref name="localForward"/>, base facing <paramref name="localUp"/>.</summary>
        void PlaceTurret(Vector3 localPosition, Vector3 localForward, Vector3 localUp)
        {
            var go = new GameObject("Turret");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(localForward, localUp);
            turret = go.AddComponent<Turret>();
            turret.AutoSimulate = false;
            turret.Target = motor;
            Physics.SyncTransforms();
        }

        /// <summary>Steps the player and the turret, with the stick held (the camera looks along the frame's forward).</summary>
        void Run(float seconds, Vector2 stick = default)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                motor.SetMoveInput(mapper.Map(stick, frame.forward, frame.up, motor.Gravity.Up));
                motor.Tick(Dt);
                turret.Tick(Dt);
            }
        }

        [Test]
        public void ShootsAPlayerInView([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            // Resting aim pointing away: it has to turn to find the player.
            PlaceTurret(Vector3.zero, Vector3.right, Vector3.up);
            int hits = 0;
            turret.Fired += hit => hits += hit ? 1 : 0;

            Run(0.2f);
            Assert.That(turret.State, Is.EqualTo(TurretState.Track));
            Assert.That(health.Current, Is.EqualTo(health.Max), "no shot before the warning");

            Run(turret.TimeToFirstShot + 0.1f);

            Assert.That(hits, Is.EqualTo(1));
            Assert.That(health.Current, Is.LessThan(health.Max));
        }

        [Test]
        public void WallBlocksLineOfSight([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Box(new Vector3(0f, 2f, 5f), new Vector3(6f, 4f, 0.5f));
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            PlaceTurret(Vector3.zero, Vector3.forward, Vector3.up);

            Run(3f);

            Assert.That(turret.State, Is.EqualTo(TurretState.Idle));
            Assert.That(health.Current, Is.EqualTo(health.Max));
        }

        [Test]
        public void DoesNotSeeBehindItsMount([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            // Hanging under a ledge 4 m up, base facing down: it sees the player below.
            PlaceTurret(new Vector3(0f, 4f, 0f), Vector3.forward, Vector3.down);

            Run(3f);
            Assert.That(health.Current, Is.LessThan(health.Max), "control: it sees the player from above");

            TearDown();
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            // Same spot, standing on top of the ledge: the player below is behind its mount.
            PlaceTurret(new Vector3(0f, 4f, 0f), Vector3.forward, Vector3.up);

            Run(3f);
            Assert.That(turret.State, Is.EqualTo(TurretState.Idle));
            Assert.That(health.Current, Is.EqualTo(health.Max));
        }

        [Test]
        public void SteppingAsideDuringTheLockDodges([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 12f));
            PlaceTurret(Vector3.zero, Vector3.forward, Vector3.up);
            bool? hit = null;
            turret.Fired += h => hit = h;

            // Stand still until the aim locks, then run sideways.
            float untilLock = turret.TimeToFirstShot - turret.AimLockTime;
            Run(untilLock + Dt);
            Assert.That(turret.State, Is.EqualTo(TurretState.Charge));
            Run(turret.AimLockTime + 0.05f, Vector2.right);

            Assert.That(hit, Is.False, "the shot should miss");
            Assert.That(health.Current, Is.EqualTo(health.Max));
        }

        [Test]
        public void TwoHitsKill()
        {
            BuildFrame(Vector3.up);
            Floor();
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            PlaceTurret(Vector3.zero, Vector3.forward, Vector3.up);

            Run(6f);

            Assert.That(health.IsDead, Is.True);
            Assert.That(turret.State, Is.EqualTo(TurretState.Idle), "stands down once the player is dead");
        }
    }
}
