using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Level;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// Phase 2: death, checkpoints and the game loop. Geometry is built in a local frame
    /// rotated for each orientation, as in <see cref="MagneticBootsTests"/>; the motor and the
    /// session are stepped by hand.
    /// </summary>
    public class GameLoopTests
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
        Health health;
        LevelSession session;
        MoveInputMapper mapper;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
            session = null;
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

        GameObject Box(Vector3 localCentre, Vector3 size, bool magnetic = false, bool lethal = false)
        {
            var go = new GameObject(lethal ? "Lethal" : magnetic ? "Magnetic" : "Plain");
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<BoxCollider>().size = size;
            if (magnetic)
                go.layer = Layers.Magnetic;
            if (lethal)
                go.tag = Layers.LethalTag;
            return go;
        }

        void Floor(bool magnetic = false, float size = 40f) => Box(new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), magnetic);

        T Placed<T>(string name, Vector3 localPosition) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(frame, false);
            go.transform.localPosition = localPosition;
            return go.AddComponent<T>();
        }

        SpawnPoint Spawn(Vector3 localPosition, int order = 0)
        {
            var spawn = Placed<SpawnPoint>("Spawn", localPosition);
            spawn.order = order;
            return spawn;
        }

        Checkpoint CheckpointAt(Vector3 localPosition, int order = 1)
        {
            var checkpoint = Placed<Checkpoint>("Checkpoint", localPosition);
            checkpoint.order = order;
            var trigger = checkpoint.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(4f, 2f, 1f);
            return checkpoint;
        }

        KillVolume KillVolumeAt(Vector3 localCentre, Vector3 size)
        {
            var volume = Placed<KillVolume>("Kill Volume", localCentre);
            volume.size = size;
            volume.Rebuild();
            return volume;
        }

        LevelExit ExitAt(Vector3 localPosition)
        {
            var exit = Placed<LevelExit>("Exit", localPosition);
            var trigger = exit.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(4f, 2f, 1f);
            return exit;
        }

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
            boots = go.AddComponent<MagneticBoots>();
            boots.Settings = ScriptableObject.CreateInstance<BootsSettings>();
            created.Add(boots.Settings);
            health = go.AddComponent<Health>();
            go.AddComponent<PlayerSensor>();
            mapper = new MoveInputMapper();
            Physics.SyncTransforms();
        }

        /// <summary>Starts a session on the player; respawns are immediate unless a delay is given.</summary>
        void StartSession(float respawnDelay = 0f)
        {
            var go = new GameObject("Session");
            created.Add(go);
            session = go.AddComponent<LevelSession>();
            session.RespawnDelay = respawnDelay;
            Physics.SyncTransforms();
            session.Begin(motor);
        }

        /// <summary>Steps with the stick held; the "camera" looks along the frame's forward.</summary>
        void Run(float seconds, Vector2 stick = default)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                if (session == null || session.State == SessionState.Playing)
                    motor.SetMoveInput(mapper.Map(stick, frame.forward, frame.up, motor.Gravity.Up));
                motor.Tick(Dt);
                session?.Tick(Dt);
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

        static void AssertDirection(Vector3 expected, Vector3 actual, float degrees, string what)
        {
            Assert.That(Vector3.Angle(expected, actual), Is.LessThan(degrees), $"{what}: expected {expected} but was {actual}");
        }

        [Test]
        public void SteppingOntoLethalFloorKills([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Box(new Vector3(0f, -0.5f, 4f), new Vector3(4f, 1.02f, 2f), lethal: true);
            SpawnPlayer(Vector3.zero);
            DeathCause? cause = null;
            health.Died += c => cause = c;

            Run(0.2f);
            Assert.That(health.IsDead, Is.False, "alive on the safe floor");
            Run(1.5f, Vector2.up);

            Assert.That(health.IsDead, Is.True);
            Assert.That(cause, Is.EqualTo(DeathCause.Lethal));
        }

        [Test]
        public void WalkingIntoKillVolumeKills([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            KillVolumeAt(new Vector3(0f, 1f, 4f), new Vector3(4f, 2f, 1f));
            SpawnPlayer(Vector3.zero);
            DeathCause? cause = null;
            health.Died += c => cause = c;

            Run(1.5f, Vector2.up);

            Assert.That(health.IsDead, Is.True);
            Assert.That(cause, Is.EqualTo(DeathCause.KillVolume));
        }

        [Test]
        public void WalkingThroughCheckpointMakesItTheRespawnPoint([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Spawn(Vector3.zero);
            Checkpoint checkpoint = CheckpointAt(new Vector3(0f, 0f, 3f));
            SpawnPlayer(Vector3.zero);
            StartSession();
            Assert.That(checkpoint.IsLit, Is.False);

            Run(1.5f, Vector2.up);

            Assert.That(session.Checkpoint, Is.SameAs(checkpoint));
            Assert.That(checkpoint.IsLit, Is.True);
        }

        [Test]
        public void RespawnRestoresCheckpointOrientationAndBoots([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor(magnetic: true);
            Spawn(new Vector3(0f, 0f, -3f));
            CheckpointAt(new Vector3(0f, 0f, 1f));
            SpawnPlayer(new Vector3(0f, 0f, -3f));
            StartSession();
            // Starting on anything but a floor, the start spawn has already put the boots on.
            if (up == Vector3.up)
                Activate();
            else
                Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized), "a spawn on a wall or ceiling starts magnetized");
            // As far as 1.5 s at full speed, at the slower magnetized pace.
            Run(1.5f * motor.Settings.moveSpeed / boots.Settings.magnetizedMoveSpeed, Vector2.up);
            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));
            Assert.That(Local(motor.Position).z, Is.GreaterThan(3f), "should have walked past the checkpoint");

            health.Kill(DeathCause.Lethal);

            Assert.That(session.Deaths, Is.EqualTo(1));
            Assert.That(health.IsDead, Is.False, "respawned");
            Vector3 local = Local(motor.Position);
            Assert.That(new Vector2(local.x, local.z - 1f).magnitude, Is.LessThan(0.1f), $"on the checkpoint, was {local}");
            AssertDirection(frame.up, motor.Gravity.Up, 1f, "up");
            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));

            Run(0.5f);
            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized), "boots should hold after respawning");
            Assert.That(motor.IsGrounded, Is.True);
            AssertDirection(frame.up, motor.BodyUp, 5f, "body up");
            Assert.That(Local(motor.Position).y, Is.InRange(-0.05f, 0.1f), "standing on the floor");
        }

        [Test]
        public void RespawnKeepsBootsOffWhenCheckpointReachedWithBootsOff()
        {
            BuildFrame(Vector3.up);
            Floor(magnetic: true);
            Spawn(new Vector3(0f, 0f, -3f));
            CheckpointAt(new Vector3(0f, 0f, 1f));
            SpawnPlayer(new Vector3(0f, 0f, -3f));
            StartSession();
            Run(1.5f, Vector2.up);

            health.Kill(DeathCause.Lethal);

            Assert.That(boots.State, Is.EqualTo(BootsState.Off));
            Assert.That(Local(motor.Position).z, Is.EqualTo(1f).Within(0.1f));
        }

        [Test]
        public void FallingOutOfTheLevelKills()
        {
            BuildFrame(Vector3.up);
            Floor(size: 4f);
            Spawn(Vector3.zero);
            SpawnPlayer(Vector3.zero);
            StartSession();
            DeathCause? cause = null;
            session.PlayerDied += c => cause = c;

            Run(0.8f, Vector2.up);
            Assert.That(motor.IsGrounded, Is.False, "should have walked off the edge");
            Run(3f);

            Assert.That(cause, Is.EqualTo(DeathCause.OutOfBounds));
            Assert.That(session.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void DeathToRespawnTakesTheRespawnDelay()
        {
            BuildFrame(Vector3.up);
            Floor();
            Spawn(Vector3.zero);
            SpawnPlayer(new Vector3(0f, 0f, 0f));
            StartSession(respawnDelay: 1.2f);
            Run(0.8f, Vector2.up);

            health.Kill(DeathCause.Lethal);
            Assert.That(session.State, Is.EqualTo(SessionState.Dying));
            float waited = 0f;
            while (session.State == SessionState.Dying && waited < 2f)
            {
                Run(Dt);
                waited += Dt;
            }

            Assert.That(session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(waited, Is.EqualTo(1.2f).Within(Dt * 1.5f), "the delay plus at most a frame");
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(Local(motor.Position).z, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void ReachingTheExitCompletesTheChamber([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Floor();
            Spawn(Vector3.zero);
            ExitAt(new Vector3(0f, 0f, 3f));
            SpawnPlayer(Vector3.zero);
            StartSession();
            bool completed = false;
            session.Completed += () => completed = true;

            Run(1.5f, Vector2.up);

            Assert.That(completed, Is.True);
            Assert.That(session.State, Is.EqualTo(SessionState.Complete));
            float time = session.ElapsedTime;
            Assert.That(time, Is.GreaterThan(0f));
            Run(0.5f);
            Assert.That(session.ElapsedTime, Is.EqualTo(time), "the clock stops at the exit");
        }

        [Test]
        public void RestartReturnsToCheckpointWithoutCountingADeath()
        {
            BuildFrame(Vector3.up);
            Floor();
            Spawn(Vector3.zero);
            SpawnPlayer(Vector3.zero);
            StartSession();
            Run(1f, Vector2.up);

            session.RespawnAtCheckpoint();

            Assert.That(session.Deaths, Is.EqualTo(0));
            Assert.That(Local(motor.Position).z, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void DamageKillsWhenHealthRunsOut()
        {
            BuildFrame(Vector3.up);
            Floor();
            SpawnPlayer(Vector3.zero);
            DeathCause? cause = null;
            health.Died += c => cause = c;

            health.Damage(60f);
            Assert.That(health.IsDead, Is.False);
            health.Damage(60f);

            Assert.That(health.IsDead, Is.True);
            Assert.That(cause, Is.EqualTo(DeathCause.Damage));
        }
    }
}
