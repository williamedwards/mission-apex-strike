using System;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Why the player died; the HUD and later effects can tell them apart.</summary>
    public enum DeathCause
    {
        /// <summary>Touched a surface tagged <see cref="Layers.LethalTag"/>.</summary>
        Lethal,
        /// <summary>Entered a <see cref="Level.KillVolume"/> (death pits).</summary>
        KillVolume,
        /// <summary>Left the level's bounds (the safety net under everything).</summary>
        OutOfBounds,
        /// <summary>Health ran out (lasers, from Phase 4).</summary>
        Damage,
    }

    /// <summary>The player's health. Lethal surfaces and pits kill outright; weapons deal damage.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Raised when damage is taken, with the amount.</summary>
        public event Action<float> Damaged;
        public event Action<DeathCause> Died;
        /// <summary>Raised when health is set back to full (respawn).</summary>
        public event Action Restored;

        void Awake() => Current = maxHealth;

        public void Damage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount);
            if (Current <= 0f)
                Kill(DeathCause.Damage);
        }

        public void Kill(DeathCause cause)
        {
            if (IsDead)
                return;
            Current = 0f;
            IsDead = true;
            Died?.Invoke(cause);
        }

        public void Restore()
        {
            Current = maxHealth;
            IsDead = false;
            Restored?.Invoke();
        }
    }
}
