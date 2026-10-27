using System;
using GravBoots.Level;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// After every motor step, checks what the player is touching: lethal surfaces and kill
    /// volumes kill, and checkpoints and the exit are reported to whoever listens (the
    /// <see cref="LevelSession"/>).
    /// </summary>
    /// <remarks>
    /// An overlap query on the motor's step rather than trigger messages, because the motor
    /// moves the body kinematically and tests step it by hand: the result has to depend only on
    /// the pose after each step, in any orientation.
    /// </remarks>
    [RequireComponent(typeof(PlayerMotor), typeof(Health))]
    public class PlayerSensor : MonoBehaviour
    {
        [Tooltip("How far beyond the body counts as touching (the motor rests a skin width off surfaces).")]
        [SerializeField] float contactMargin = 0.06f;

        PlayerMotor motor;
        Health health;
        readonly Collider[] touching = new Collider[16];

        public event Action<Checkpoint> CheckpointReached;
        public event Action<LevelExit> ExitReached;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
        }

        void OnEnable() => motor.Stepped += OnStepped;

        void OnDisable() => motor.Stepped -= OnStepped;

        void OnStepped(float deltaTime)
        {
            if (health.IsDead)
                return;

            int count = motor.Touching(contactMargin, touching);
            for (int i = 0; i < count; i++)
            {
                Collider other = touching[i];
                if (other.CompareTag(Layers.LethalTag))
                {
                    health.Kill(DeathCause.Lethal);
                    return;
                }
                if (other.isTrigger && other.TryGetComponent(out KillVolume _))
                {
                    health.Kill(DeathCause.KillVolume);
                    return;
                }
            }
            for (int i = 0; i < count; i++)
            {
                Collider other = touching[i];
                if (!other.isTrigger)
                    continue;
                if (other.TryGetComponent(out Checkpoint checkpoint))
                    CheckpointReached?.Invoke(checkpoint);
                else if (other.TryGetComponent(out LevelExit exit))
                    ExitReached?.Invoke(exit);
            }
        }
    }
}
