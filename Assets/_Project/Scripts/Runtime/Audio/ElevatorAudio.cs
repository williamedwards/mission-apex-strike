using GravBoots.Level;
using UnityEngine;

namespace GravBoots.Audio
{
    /// <summary>
    /// An elevator's sounds, on its platform: the motor loop while it moves, and a clunk as it
    /// stops at the top. Added at runtime by <see cref="GameAudio"/>.
    /// </summary>
    public class ElevatorAudio : MonoBehaviour
    {
        const float FadeSeconds = 0.3f;

        SoundBank bank;
        Elevator elevator;
        AudioSource motor, effects;
        bool wasMoving;

        public void Bind(SoundBank soundBank)
        {
            bank = soundBank;
            elevator = GetComponent<Elevator>();
            GameObject host = elevator.mover != null && elevator.mover.platform != null
                ? elevator.mover.platform.gameObject : gameObject;
            motor = GameAudio.Source(host, true, true);
            effects = GameAudio.Source(host, true);
            GameAudio.StartLoop(motor, bank.elevatorLoop, 0f);
        }

        void Update()
        {
            if (elevator == null || elevator.mover == null)
                return;
            KinematicMover mover = elevator.mover;
            bool moving = mover.Triggered && !mover.AtEnd;
            float target = moving ? bank.elevatorLoop.volume : 0f;
            motor.volume = Mathf.MoveTowards(motor.volume, target, bank.elevatorLoop.volume * Time.deltaTime / FadeSeconds);
            if (wasMoving && !moving && mover.AtEnd)
                GameAudio.Play(effects, bank.elevatorStop);
            wasMoving = moving;
        }
    }
}
