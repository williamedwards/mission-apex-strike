using GravBoots.Player;
using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>
    /// The player's sounds: a footstep each time a foot comes down in the walk or run (read
    /// from the character's foot bones, so steps match the animation at any speed or
    /// orientation), quiet breathing, the boots clamping and releasing, their hum while
    /// magnetized, and a groan on dying. Added at runtime by <see cref="GameAudio"/>.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        const float HumFadeSeconds = 0.25f;

        SoundBank bank;
        PlayerMotor motor;
        MagneticBoots boots;
        Health health;
        Animator animator;
        Transform leftFoot, rightFoot;
        AudioSource effects, steps, breathing, hum;
        FootfallDetector leftFall, rightFall;
        int lastStep = -1;

        public void Bind(SoundBank soundBank)
        {
            bank = soundBank;
            motor = GetComponent<PlayerMotor>();
            boots = GetComponent<MagneticBoots>();
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            effects = GameAudio.Source(gameObject, false);
            steps = GameAudio.Source(gameObject, false);
            breathing = GameAudio.Source(gameObject, false, true);
            hum = GameAudio.Source(gameObject, false, true);
            GameAudio.StartLoop(breathing, bank.breathing, bank.breathing.volume);
            GameAudio.StartLoop(hum, bank.bootsHum, 0f);

            if (boots != null)
                boots.StateChanged += OnBootsChanged;
            if (health != null)
                health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (boots != null)
                boots.StateChanged -= OnBootsChanged;
            if (health != null)
                health.Died -= OnDied;
        }

        void OnBootsChanged(BootsState previous, BootsState next)
        {
            if (next == BootsState.Magnetized)
                GameAudio.Play(effects, bank.bootsOn, 0.04f);
            else if (previous == BootsState.Magnetized)
                GameAudio.Play(effects, bank.bootsOff, 0.04f);
        }

        void OnDied(DeathCause cause) => GameAudio.Play(effects, bank.deathGroan, 0.05f);

        void LateUpdate()
        {
            if (bank == null || motor == null)
                return;
            float humTarget = boots != null && boots.IsMagnetized ? bank.bootsHum.volume : 0f;
            hum.volume = Mathf.MoveTowards(hum.volume, humTarget, bank.bootsHum.volume * Time.deltaTime / HumFadeSeconds);

            bool walking = motor.IsGrounded && Vector3.ProjectOnPlane(motor.Velocity, motor.BodyUp).magnitude > 0.3f
                && (health == null || !health.IsDead);
            if (leftFoot != null)
                Step(leftFoot, ref leftFall, walking);
            if (rightFoot != null)
                Step(rightFoot, ref rightFall, walking);
        }

        /// <summary>A footstep as this foot plants.</summary>
        void Step(Transform foot, ref FootfallDetector fall, bool walking)
        {
            float height = Vector3.Dot(foot.position - motor.Position, motor.BodyUp);
            if (fall.Update(height, Time.deltaTime) && walking)
                PlayStep();
        }

        void PlayStep()
        {
            if (bank.footsteps == null || bank.footsteps.Length == 0)
                return;
            int pick = Random.Range(0, bank.footsteps.Length);
            if (bank.footsteps.Length > 1 && pick == lastStep)
                pick = (pick + 1) % bank.footsteps.Length;
            lastStep = pick;
            float volume = bank.footstepVolume * (boots != null && boots.IsMagnetized ? 1.15f : 1f);
            steps.pitch = Random.Range(0.94f, 1.06f);
            steps.PlayOneShot(bank.footsteps[pick], volume);
        }
    }
}
