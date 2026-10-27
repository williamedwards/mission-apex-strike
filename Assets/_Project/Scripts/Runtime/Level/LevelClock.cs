using System.Collections.Generic;
using UnityEngine;

namespace GravBoots.Level
{
    /// <summary>
    /// The chamber's clock: every moving element runs from its time, not its own timers, so a
    /// puzzle's timing is the same every attempt (docs/PLAN.md §1.6). It ticks before the
    /// motor; respawning sets it back to when the checkpoint was reached.
    /// </summary>
    /// <remarks>
    /// Created on demand by the first mover (<see cref="Ensure"/>). Tests turn
    /// <see cref="AutoSimulate"/> off and call <see cref="Tick"/> before stepping the motor.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder.LevelClock)]
    public class LevelClock : MonoBehaviour
    {
        [SerializeField] bool autoSimulate = true;

        readonly List<KinematicMover> movers = new List<KinematicMover>();

        public static LevelClock Instance { get; private set; }
        public float Time { get; private set; }
        public bool AutoSimulate { get => autoSimulate; set => autoSimulate = value; }

        /// <summary>The scene's clock, made if there isn't one yet.</summary>
        public static LevelClock Ensure()
        {
            if (Instance == null)
                Instance = FindAnyObjectByType<LevelClock>();
            if (Instance == null)
                Instance = new GameObject("Level Clock").AddComponent<LevelClock>();
            return Instance;
        }

        void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void FixedUpdate()
        {
            if (autoSimulate)
                Tick(UnityEngine.Time.fixedDeltaTime);
        }

        public void Register(KinematicMover mover)
        {
            if (!movers.Contains(mover))
                movers.Add(mover);
            mover.Evaluate(Time);
        }

        public void Unregister(KinematicMover mover) => movers.Remove(mover);

        public void Tick(float deltaTime)
        {
            Time += deltaTime;
            Apply();
        }

        /// <summary>Jumps to <paramref name="time"/> (respawn): movers go back to where they were then.</summary>
        public void SetTime(float time)
        {
            Time = time;
            foreach (KinematicMover mover in movers)
                mover.Rewind(time);
            Apply();
        }

        void Apply()
        {
            foreach (KinematicMover mover in movers)
                mover.Evaluate(Time);
            // Moved colliders must be where queries see them before the motor steps.
            Physics.SyncTransforms();
        }
    }
}
