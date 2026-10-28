using System.Collections.Generic;
using GravBoots.Gravity;
using GravBoots.Hazards;
using GravBoots.Level;
using GravBoots.Player;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    /// <summary>
    /// Phase 3: moving platforms. The player rides them in any orientation (magnetized too),
    /// keeps their velocity on leaving, and they run from the level clock, which a respawn
    /// rewinds. Built in a rotated local frame; the clock, motor and session are stepped by hand.
    /// </summary>
    public class MoverTests
    {
        const float Dt = 1f / 60f;

        static readonly Vector3[] Ups =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
        };

        static IEnumerable<Vector3> Orientations() => Ups;

        readonly List<Object> created = new List<Object>();
        Transform frame;
        LevelClock clock;
        PlayerMotor motor;
        MagneticBoots boots;
        Health health;
        LevelSession session;
        MoveInputMapper mapper;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Level Clock");
            created.Add(go);
            clock = go.AddComponent<LevelClock>();
            clock.AutoSimulate = false;
        }

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

        GameObject Box(Transform parent, Vector3 localCentre, Vector3 size, bool magnetic = false)
        {
            var go = new GameObject(magnetic ? "Magnetic" : "Box");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<BoxCollider>().size = size;
            if (magnetic)
                go.layer = Layers.Magnetic;
            return go;
        }

        /// <summary>
        /// A mover whose platform is a 4 × 0.3 × 4 deck (top face at the platform's origin),
        /// travelling through <paramref name="localPoints"/> (with optional rotations).
        /// </summary>
        KinematicMover Mover(Vector3[] localPoints, Quaternion[] localRotations = null, MoverMode mode = MoverMode.PingPong,
            float speed = 2f, float wait = 0f, bool magnetic = false)
        {
            var root = new GameObject("Mover");
            root.transform.SetParent(frame, false);
            var mover = root.AddComponent<KinematicMover>();
            var platform = new GameObject("Platform").transform;
            platform.SetParent(root.transform, false);
            Box(platform, new Vector3(0f, -0.15f, 0f), new Vector3(4f, 0.3f, 4f), magnetic);
            mover.platform = platform;
            mover.points = new Transform[localPoints.Length];
            for (int i = 0; i < localPoints.Length; i++)
            {
                var point = new GameObject($"Point {i + 1}").transform;
                point.SetParent(root.transform, false);
                point.localPosition = localPoints[i];
                point.localRotation = localRotations != null ? localRotations[i] : Quaternion.identity;
                mover.points[i] = point;
            }
            mover.mode = mode;
            mover.speed = speed;
            mover.wait = wait;
            mover.CachePath();
            clock.SetTime(0f);
            return mover;
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

        /// <summary>
        /// Steps the clock (movers), then the motor, then the session, as in play. The stick only
        /// counts while the session is playing, as the controller is off otherwise.
        /// </summary>
        void Run(float seconds, Vector2 stick = default, System.Action eachStep = null)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                clock.Tick(Dt);
                if (session == null || session.State == SessionState.Playing)
                    motor.SetMoveInput(mapper.Map(stick, frame.forward, frame.up, motor.Gravity.Up));
                motor.Tick(Dt);
                session?.Tick(Dt);
                eachStep?.Invoke();
            }
        }

        Vector3 Local(Vector3 world) => frame.InverseTransformPoint(world);

        /// <summary>The player's position on the platform, in the platform's own frame.</summary>
        static Vector3 OnPlatform(KinematicMover mover, PlayerMotor player) => mover.platform.InverseTransformPoint(player.Position);

        [Test]
        public void RidesAPlatformMovingSideways([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            KinematicMover mover = Mover(new[] { Vector3.zero, new Vector3(10f, 0f, 0f) });
            SpawnPlayer(Vector3.zero);
            Run(0.3f);
            Vector3 start = OnPlatform(mover, motor);

            Run(4f);

            Assert.That(Local(mover.platform.position).x, Is.GreaterThan(5f), "the platform moved");
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(Vector3.Distance(OnPlatform(mover, motor), start), Is.LessThan(0.1f), "and the player stayed put on it");
        }

        [Test]
        public void StandingStillOnAMovingPlatformDoesNotAnimateAsWalking([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Mover(new[] { Vector3.zero, new Vector3(10f, 0f, 0f) }, speed: 4f);
            SpawnPlayer(Vector3.zero);
            Run(0.3f);
            float fastest = 0f, carried = 0f;

            Run(2f, eachStep: () =>
            {
                fastest = Mathf.Max(fastest, PlayerAnimation.GroundSpeed(motor));
                carried = Mathf.Max(carried, motor.PlatformVelocity.magnitude);
            });

            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(carried, Is.GreaterThan(3f), "the platform carried the player");
            Assert.That(fastest, Is.LessThan(0.2f), "the walk/run blend reads the player as standing");
        }

        [Test]
        public void RidesAnElevatorUpAndDown([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            KinematicMover mover = Mover(new[] { Vector3.zero, new Vector3(0f, 5f, 0f) }, speed: 2.5f, wait: 0.5f);
            SpawnPlayer(Vector3.zero);
            Run(0.3f);
            float highest = 0f;
            bool alwaysOn = true;

            Run(5f, default, () =>
            {
                highest = Mathf.Max(highest, Local(motor.Position).y);
                alwaysOn &= motor.IsGrounded && Mathf.Abs(OnPlatform(mover, motor).y) < 0.1f;
            });

            Assert.That(highest, Is.GreaterThan(4.5f), "went up");
            Assert.That(Local(mover.platform.position).y, Is.LessThan(0.5f), "and came back down");
            Assert.That(alwaysOn, Is.True, "standing on it the whole way, up and down");
        }

        [Test]
        public void StaysMagnetizedOnAMovingPanel([ValueSource(nameof(Orientations))] Vector3 up)
        {
            // In every frame, including hanging from a ceiling panel that carries you across.
            BuildFrame(up);
            KinematicMover mover = Mover(new[] { Vector3.zero, new Vector3(0f, 0f, 10f) }, speed: 3f, magnetic: true);
            SpawnPlayer(Vector3.zero);
            Run(0.2f);
            boots.RequestToggle();
            Run(Dt);
            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));

            Run(7f);

            Assert.That(boots.State, Is.EqualTo(BootsState.Magnetized));
            Assert.That(Vector3.Angle(motor.Gravity.Up, frame.up), Is.LessThan(1f));
            Assert.That(OnPlatform(mover, motor).magnitude, Is.LessThan(0.15f));
        }

        [Test]
        public void TurntableTurnsTheRider([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            KinematicMover mover = Mover(new[] { Vector3.zero, Vector3.zero },
                new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) }, speed: 1f, wait: 5f);
            SpawnPlayer(new Vector3(1f, 0f, 0f));
            Run(0.3f);
            Vector3 facing = frame.InverseTransformDirection(motor.Rotation * Vector3.forward);

            // One trip: 5 s waiting, then about 1.6 s turning.
            Run(5f + 2f);

            Vector3 turned = frame.InverseTransformDirection(motor.Rotation * Vector3.forward);
            Assert.That(Vector3.Angle(facing, turned), Is.EqualTo(90f).Within(3f), "facing turned with it");
            Assert.That(Vector3.Distance(Local(motor.Position), new Vector3(0f, 0f, -1f)), Is.LessThan(0.15f), "carried round");
            Assert.That(Vector3.Angle(motor.BodyUp, frame.up), Is.LessThan(1f));
        }

        [Test]
        public void APlatformComingDownStepsThePlayerAside([ValueSource(nameof(Orientations))] Vector3 up)
        {
            // Standing under an elevator as it descends: never squeezed through the floor.
            BuildFrame(up);
            Box(frame, new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            KinematicMover mover = Mover(new[] { new Vector3(0f, 5f, 0f), new Vector3(0f, 0.3f, 0f) }, speed: 2f, wait: 0.5f);
            SpawnPlayer(new Vector3(0.5f, 0f, 0.5f));
            float lowest = float.MaxValue;

            // 0.5 s pause, then 2.35 s down: at the bottom from 2.85 s to 3.35 s.
            Run(3.1f, default, () => lowest = Mathf.Min(lowest, Local(motor.Position).y));

            Assert.That(Local(mover.platform.position).y, Is.LessThan(0.35f), "the platform came all the way down");
            Assert.That(lowest, Is.GreaterThan(-0.1f), "never pushed into the floor");
            Assert.That(motor.IsGrounded, Is.True);
            Vector3 local = Local(motor.Position);
            Assert.That(Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)), Is.GreaterThan(2f), "stepped out from under it");
        }

        [Test]
        public void JumpingOffKeepsThePlatformsVelocity([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            KinematicMover mover = Mover(new[] { Vector3.zero, new Vector3(40f, 0f, 0f) }, speed: 4f);
            SpawnPlayer(Vector3.zero);
            // Half way: the platform is at full speed.
            Run(5f);
            float platformSpeed = frame.InverseTransformDirection(motor.PlatformVelocity).x;
            Assert.That(platformSpeed, Is.GreaterThan(5f));

            motor.RequestJump();
            Run(Dt);

            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(frame.InverseTransformDirection(motor.Velocity).x, Is.EqualTo(platformSpeed).Within(0.5f));
        }

        [Test]
        public void OnStepPlatformWaitsForTheRider()
        {
            BuildFrame(Vector3.up);
            // Ground up to the platform's near edge (z = -2).
            Box(frame, new Vector3(0f, -0.5f, -5.5f), new Vector3(6f, 1f, 7f));
            KinematicMover mover = Mover(new[] { Vector3.zero, new Vector3(0f, 0f, 10f) }, mode: MoverMode.OnStep);
            SpawnPlayer(new Vector3(0f, 0f, -6f));

            Run(2f);
            Assert.That(Local(mover.platform.position).z, Is.EqualTo(0f).Within(1e-3f), "waits");

            Run(1f, Vector2.up);
            Assert.That(mover.Triggered, Is.True);
            Run(6f);
            Assert.That(Local(mover.platform.position).z, Is.EqualTo(10f).Within(1e-3f), "went once, and stays");
        }

        [Test]
        public void RewindingTheClockPutsMoversBack()
        {
            BuildFrame(Vector3.up);
            KinematicMover pingPong = Mover(new[] { Vector3.zero, new Vector3(10f, 0f, 0f) });
            for (int i = 0; i < 180; i++)
                clock.Tick(Dt);
            Vector3 atThree = pingPong.platform.position;
            for (int i = 0; i < 120; i++)
                clock.Tick(Dt);

            clock.SetTime(3f);

            Assert.That(Vector3.Distance(pingPong.platform.position, atThree), Is.LessThan(1e-3f));
        }

        [Test]
        public void RespawnRewindsTheClockToTheCheckpoint()
        {
            BuildFrame(Vector3.up);
            Box(frame, new Vector3(0f, -0.5f, -6f), new Vector3(6f, 1f, 6f));
            KinematicMover mover = Mover(new[] { new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) });
            var spawn = new GameObject("Spawn").AddComponent<SpawnPoint>();
            spawn.transform.SetParent(frame, false);
            spawn.transform.localPosition = new Vector3(0f, 0f, -6f);
            SpawnPlayer(new Vector3(0f, 0f, -6f));
            var go = new GameObject("Session");
            created.Add(go);
            session = go.AddComponent<LevelSession>();
            session.RespawnDelay = 0f;
            Physics.SyncTransforms();
            session.Begin(motor);
            Vector3 atStart = mover.platform.position;

            Run(3f);
            Assert.That(Vector3.Distance(mover.platform.position, atStart), Is.GreaterThan(1f));
            health.Kill(DeathCause.Lethal);

            Assert.That(clock.Time, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(mover.platform.position, atStart), Is.LessThan(1e-3f));
        }

        [Test]
        public void SentryOnATrackStillLocksOnAndKills([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Box(frame, new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            KinematicMover track = Mover(new[] { new Vector3(-6f, 0f, 0f), new Vector3(6f, 0f, 0f) }, speed: 3f);
            // Swap the deck for a sentry riding the track.
            Object.DestroyImmediate(track.platform.GetChild(0).gameObject);
            var sentry = track.platform.gameObject.AddComponent<Sentry>();
            sentry.AutoSimulate = false;
            SpawnPlayer(new Vector3(0f, 0f, 10f));
            sentry.Target = motor;

            for (int i = 0; i < 120 && !health.IsDead; i++)
            {
                clock.Tick(Dt);
                motor.Tick(Dt);
                sentry.Tick(Dt);
            }

            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void ADestroyedSentryStopsOnItsTrack()
        {
            BuildFrame(Vector3.up);
            Box(frame, new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            KinematicMover track = Mover(new[] { new Vector3(-6f, 0f, 0f), new Vector3(6f, 0f, 0f) }, speed: 3f);
            Object.DestroyImmediate(track.platform.GetChild(0).gameObject);
            var sentry = track.platform.gameObject.AddComponent<Sentry>();
            sentry.AutoSimulate = false;
            for (int i = 0; i < 60; i++)
                clock.Tick(Dt);

            sentry.Disable();
            Vector3 where = sentry.transform.position;
            for (int i = 0; i < 120; i++)
                clock.Tick(Dt);

            Assert.That(Vector3.Distance(sentry.transform.position, where), Is.LessThan(1e-3f));
        }
    
        // ---------------------------------------------------------------- tube elevators

        SpawnPoint SpawnAt(Vector3 localPosition)
        {
            var spawn = new GameObject("Spawn").AddComponent<SpawnPoint>();
            spawn.transform.SetParent(frame, false);
            spawn.transform.localPosition = localPosition;
            return spawn;
        }

        /// <summary>Starts a session (instant respawns) and puts the player on <paramref name="start"/>.</summary>
        void BeginSession(SpawnPoint start)
        {
            var go = new GameObject("Session");
            created.Add(go);
            session = go.AddComponent<LevelSession>();
            session.RespawnDelay = 0f;
            Physics.SyncTransforms();
            session.Begin(motor);
            session.SetCheckpoint(start, false);
            session.RespawnAtCheckpoint();
        }

        /// <summary>A start elevator: the platform rises 4 m to the frame's floor level, spawns at both ends.</summary>
        Elevator StartElevator()
        {
            KinematicMover mover = Mover(new[] { new Vector3(0f, -4f, 0f), Vector3.zero }, mode: MoverMode.OnStep, speed: 2f, wait: 0f);
            var elevator = mover.gameObject.AddComponent<Elevator>();
            elevator.role = ElevatorRole.Start;
            elevator.mover = mover;
            elevator.boardingSpawn = SpawnAt(new Vector3(0f, -4f + 0.02f, 0f));
            elevator.arrivalSpawn = SpawnAt(new Vector3(0f, 0.02f, 0f));
            return elevator;
        }

        /// <summary>Steps until the session hands over control (or gives up after <paramref name="limit"/> seconds).</summary>
        float RideUntilPlaying(Vector2 stick, float limit = 5f)
        {
            float t = 0f;
            while (session.State == SessionState.Arriving && t < limit)
            {
                Run(Dt, stick);
                t += Dt;
            }
            return t;
        }

        [Test]
        public void StartElevatorRidesUpThenHandsOverControl([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Elevator elevator = StartElevator();
            SpawnPlayer(new Vector3(0f, -4f, 0f));
            BeginSession(elevator.boardingSpawn);

            Assert.That(session.State, Is.EqualTo(SessionState.Arriving), "the run starts riding up");
            Run(0.5f, Vector2.up);
            Assert.That(session.State, Is.EqualTo(SessionState.Arriving));
            Vector3 onDeck = OnPlatform(elevator.mover, motor);
            Assert.That(new Vector2(onDeck.x, onDeck.z).magnitude, Is.LessThan(0.1f), "the stick does nothing while riding");

            float ride = RideUntilPlaying(Vector2.up);

            Assert.That(session.State, Is.EqualTo(SessionState.Playing), $"control comes back at the top (after {ride:0.00} s)");
            Assert.That(Local(motor.Position).y, Is.EqualTo(0f).Within(0.05f), "at floor level");
            Assert.That(session.Checkpoint, Is.EqualTo(elevator.arrivalSpawn), "respawns are at the top from now on");
            Vector3 before = Local(motor.Position);
            Run(0.25f, Vector2.up);
            Assert.That(Local(motor.Position).z - before.z, Is.GreaterThan(0.3f), "and the stick works again");
        }

        [Test]
        public void DyingAfterTheStartRideRespawnsAtTheTopWithoutRidingAgain([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Elevator elevator = StartElevator();
            SpawnPlayer(new Vector3(0f, -4f, 0f));
            BeginSession(elevator.boardingSpawn);
            RideUntilPlaying(default);
            Run(0.5f);

            health.Kill(DeathCause.Lethal);
            Run(Dt);

            Assert.That(session.State, Is.EqualTo(SessionState.Playing), "no second ride");
            Assert.That(Local(elevator.mover.platform.position).y, Is.EqualTo(0f).Within(1e-3f), "the platform stays up");
            Assert.That(Local(motor.Position).y, Is.EqualTo(0f).Within(0.05f), "back on it at the top");
        }

        [Test]
        public void ExitElevatorEndsTheRunAndCarriesThePlayerOut([ValueSource(nameof(Orientations))] Vector3 up)
        {
            BuildFrame(up);
            Box(frame, new Vector3(0f, -0.5f, -4f), new Vector3(4f, 1f, 8f));
            KinematicMover mover = Mover(new[] { new Vector3(0f, 0f, 2f), new Vector3(0f, 5f, 2f) }, mode: MoverMode.OnStep, speed: 2f, wait: 0f);
            var elevator = mover.gameObject.AddComponent<Elevator>();
            elevator.role = ElevatorRole.Exit;
            elevator.mover = mover;
            var exit = new GameObject("Exit", typeof(BoxCollider)).AddComponent<LevelExit>();
            exit.transform.SetParent(mover.platform, false);
            var trigger = exit.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.4f, 2f, 1.4f);
            trigger.center = new Vector3(0f, 1f, 0f);
            SpawnPlayer(new Vector3(0f, 0f, -4f));
            BeginSession(SpawnAt(new Vector3(0f, 0.02f, -4f)));

            for (float t = 0f; session.State == SessionState.Playing && t < 3f; t += Dt)
                Run(Dt, Vector2.up);
            Assert.That(session.State, Is.EqualTo(SessionState.Complete), "stepping on ends the run");

            Run(4f);

            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.GroundCollider.transform.IsChildOf(mover.platform), Is.True, "riding the platform");
            Assert.That(Local(motor.Position).y, Is.EqualTo(5f).Within(0.05f), "carried to the top");
        }
}
}
