using System;
using System.Linq;
using GravBoots.Cameras;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravBoots.Level
{
    public enum SessionState { Playing, Dying, Complete, Arriving }

    /// <summary>
    /// One run of a chamber: starts the player at the first spawn, tracks the active checkpoint,
    /// turns death into a quick respawn, and finishes at the exit. Also the safety net: leaving
    /// the level's bounds kills.
    /// </summary>
    /// <remarks>
    /// Respawning restores the checkpoint's pose and gravity frame, and the boots: magnetized if
    /// the checkpoint was reached magnetized or faces away from world up, and the surface under
    /// it is magnetic. The <see cref="LevelClock"/> goes back to when the checkpoint was
    /// reached, so moving elements are in the same phase every attempt. Death to control again is
    /// <see cref="respawnDelay"/> plus a frame: long enough to see the fall, short enough that
    /// failing stays cheap.
    /// Spawning on a start <see cref="Elevator"/>'s boarding spawn holds the controls
    /// (<see cref="SessionState.Arriving"/>) until its platform stops at the top.
    /// <see cref="Tick"/> and <see cref="Begin"/> are public so tests can drive it without frames.
    /// </remarks>
    public class LevelSession : MonoBehaviour
    {
        [SerializeField] PlayerMotor player;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Seconds from death to respawn; the screen fades out over this time.")]
        [SerializeField] float respawnDelay = 1.2f;
        [Tooltip("How far outside the level's geometry the player can go before the safety net kills.")]
        [SerializeField] float boundsMargin = 25f;
        [Tooltip("Where the run starts. Empty: the first spawn point in order.")]
        [SerializeField] SpawnPoint startSpawn;

        MagneticBoots boots;
        Health health;
        PlayerSensor sensor;
        PlayerController controller;
        SpawnPoint[] spawns = Array.Empty<SpawnPoint>();
        Bounds bounds;
        bool begun;
        bool restoreMagnetized;
        float checkpointClockTime;
        float dyingTime;
        LevelExit exitReached;
        Elevator[] elevators = Array.Empty<Elevator>();
        /// <summary>The start elevator carrying the player in, while <see cref="SessionState.Arriving"/>.</summary>
        Elevator arriving;

        public SessionState State { get; private set; }
        public PlayerMotor Player => player;
        public SpawnPoint Checkpoint { get; private set; }
        /// <summary>Spawn points in order (checkpoints included); debug cycling walks this list.</summary>
        public SpawnPoint[] Spawns => spawns;
        public float ElapsedTime { get; private set; }
        public int Deaths { get; private set; }
        public Bounds LevelBounds => bounds;
        public float RespawnDelay { get => respawnDelay; set => respawnDelay = value; }
        /// <summary>0 → 1 over the respawn delay while dying (the HUD's fade).</summary>
        public float DyingProgress => State == SessionState.Dying && respawnDelay > 0f ? Mathf.Clamp01(dyingTime / respawnDelay) : 0f;

        public event Action<SpawnPoint> CheckpointChanged;
        public event Action<DeathCause> PlayerDied;
        public event Action Respawned;
        public event Action Completed;

        void Start()
        {
            if (!begun && player != null)
                Begin(player);
        }

        void OnDisable() => Unsubscribe();

        void Update()
        {
            if (!begun)
                return;
            Tick(Time.deltaTime);
            if (input == null)
                return;
            if (input.RestartChamberPressed || (State == SessionState.Complete && input.JumpPressed))
                ReloadOrAdvance();
            else if (input.RestartPressed && State == SessionState.Playing)
                RespawnAtCheckpoint();
        }

        /// <summary>Starts the run: finds the spawns and the level's bounds, and puts the player on the start spawn (the first, unless one is set).</summary>
        public void Begin(PlayerMotor playerMotor)
        {
            Unsubscribe();
            player = playerMotor;
            boots = player.GetComponent<MagneticBoots>();
            health = player.GetComponent<Health>();
            sensor = player.GetComponent<PlayerSensor>();
            controller = player.GetComponent<PlayerController>();
            if (input == null)
                input = player.GetComponent<PlayerInputReader>();

            player.Stepped += OnStepped;
            if (health != null)
                health.Died += OnDied;
            if (sensor != null)
            {
                sensor.CheckpointReached += OnCheckpointReached;
                sensor.ExitReached += OnExitReached;
            }
            begun = true;

            spawns = FindObjectsByType<SpawnPoint>().OrderBy(s => s.order).ThenBy(s => s.name).ToArray();
            elevators = FindObjectsByType<Elevator>();
            foreach (Checkpoint checkpoint in spawns.OfType<Checkpoint>())
                checkpoint.SetLit(false);
            bounds = MeasureLevel();
            ElapsedTime = 0f;
            Deaths = 0;
            SetCheckpoint(startSpawn != null ? startSpawn : spawns.FirstOrDefault(), false);
            RespawnAtCheckpoint();
        }

        /// <summary>Advances the death timer and the run clock.</summary>
        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case SessionState.Playing:
                    ElapsedTime += deltaTime;
                    break;
                case SessionState.Dying:
                    dyingTime += deltaTime;
                    if (dyingTime >= respawnDelay)
                        RespawnAtCheckpoint();
                    break;
                case SessionState.Arriving:
                    if (arriving == null || arriving.HasArrived)
                        Arrive();
                    break;
            }
        }

        /// <summary>Makes <paramref name="spawn"/> the respawn point (debug cycling, and checkpoints reached).</summary>
        public void SetCheckpoint(SpawnPoint spawn, bool reachedMagnetized)
        {
            if (Checkpoint is Checkpoint previous && previous != spawn)
                previous.SetLit(false);
            Checkpoint = spawn;
            restoreMagnetized = reachedMagnetized;
            checkpointClockTime = LevelClock.Instance != null ? LevelClock.Instance.Time : 0f;
            if (spawn is Checkpoint lit)
                lit.SetLit(true);
            CheckpointChanged?.Invoke(spawn);
        }

        /// <summary>Puts the player back on the checkpoint, alive, and hands control back.</summary>
        public void RespawnAtCheckpoint()
        {
            State = SessionState.Playing;
            dyingTime = 0f;
            health?.Restore();
            if (controller != null)
                controller.enabled = true;
            if (Checkpoint == null)
                return;
            // Movers first: the surface under the spawn may be one of them.
            if (LevelClock.Instance != null)
                LevelClock.Instance.SetTime(checkpointClockTime);

            Transform spawn = Checkpoint.transform;
            Vector3 up = spawn.up;
            Vector3 position = spawn.position;
            Collider surface = SurfaceUnder(spawn, out Vector3 surfacePoint);
            if (surface != null)
                position = surfacePoint + up * 0.02f;

            boots?.ForceOff();
            player.Gravity.SetUp(up);
            player.SetMoveInput(Vector3.zero);
            player.Teleport(position, spawn.rotation);
            bool magnetize = restoreMagnetized || Vector3.Angle(up, Vector3.up) > 1f;
            if (boots != null && magnetize && Layers.IsMagnetic(surface))
                boots.Attach();
            if (cameraRig != null)
                cameraRig.SnapBehindTarget();

            // On a start elevator's platform: ride up with the controls held until it stops.
            arriving = elevators.FirstOrDefault(e => e != null && e.role == ElevatorRole.Start && e.boardingSpawn == Checkpoint);
            if (arriving != null)
            {
                State = SessionState.Arriving;
                if (controller != null)
                    controller.enabled = false;
            }
            Respawned?.Invoke();
        }

        /// <summary>The start elevator has stopped at the top: hand over control, and respawn up here from now on.</summary>
        void Arrive()
        {
            State = SessionState.Playing;
            if (controller != null)
                controller.enabled = true;
            if (arriving != null && arriving.arrivalSpawn != null)
                SetCheckpoint(arriving.arrivalSpawn, false);
            arriving = null;
        }

        void OnStepped(float deltaTime)
        {
            if (State == SessionState.Playing && health != null && !health.IsDead && !bounds.Contains(player.Position))
                health.Kill(DeathCause.OutOfBounds);
        }

        void OnDied(DeathCause cause)
        {
            if (State != SessionState.Playing)
                return;
            Deaths++;
            State = SessionState.Dying;
            dyingTime = 0f;
            if (controller != null)
                controller.enabled = false;
            player.SetMoveInput(Vector3.zero);
            PlayerDied?.Invoke(cause);
            if (respawnDelay <= 0f)
                RespawnAtCheckpoint();
        }

        void OnCheckpointReached(Checkpoint checkpoint)
        {
            if (State != SessionState.Playing || checkpoint == Checkpoint)
                return;
            SetCheckpoint(checkpoint, boots != null && boots.IsMagnetized);
        }

        void OnExitReached(LevelExit exit)
        {
            if (State != SessionState.Playing)
                return;
            State = SessionState.Complete;
            exitReached = exit;
            if (controller != null)
                controller.enabled = false;
            // Watch the player leave (an exit elevator carries them up out of the chamber).
            if (cameraRig != null)
                cameraRig.Hold();
            player.SetMoveInput(Vector3.zero);
            Completed?.Invoke();
        }

        void ReloadOrAdvance()
        {
            string next = State == SessionState.Complete && exitReached != null ? exitReached.nextScene : null;
            SceneManager.LoadScene(string.IsNullOrEmpty(next) ? SceneManager.GetActiveScene().name : next);
        }

        /// <summary>The solid surface just below a spawn point, along its down.</summary>
        Collider SurfaceUnder(Transform spawn, out Vector3 point)
        {
            point = spawn.position;
            if (!Physics.Raycast(spawn.position + spawn.up * 0.5f, -spawn.up, out RaycastHit hit, 1.5f,
                    player.CollisionMask, QueryTriggerInteraction.Ignore))
                return null;
            point = hit.point;
            return hit.collider;
        }

        Bounds MeasureLevel()
        {
            var measured = new Bounds(player.Position, Vector3.zero);
            foreach (Collider c in FindObjectsByType<Collider>())
            {
                if (!c.isTrigger && c.enabled && c.gameObject.layer != Layers.Player)
                    measured.Encapsulate(c.bounds);
            }
            measured.Expand(boundsMargin * 2f);
            return measured;
        }

        void Unsubscribe()
        {
            if (!begun)
                return;
            begun = false;
            if (player != null)
                player.Stepped -= OnStepped;
            if (health != null)
                health.Died -= OnDied;
            if (sensor != null)
            {
                sensor.CheckpointReached -= OnCheckpointReached;
                sensor.ExitReached -= OnExitReached;
            }
        }
    }
}
