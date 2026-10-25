using GravBoots.Cameras;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Feeds camera-relative input into the <see cref="PlayerMotor"/>.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] CameraRig cameraRig;

        readonly MoveInputMapper mapper = new MoveInputMapper();
        PlayerMotor motor;

        public MoveInputMapper Mapper => mapper;
        public CameraRig CameraRig => cameraRig;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();
        }

        void Update()
        {
            if (input == null)
                return;

            Transform view = cameraRig != null ? cameraRig.ViewTransform : transform;
            Vector3 cameraUp = cameraRig != null ? cameraRig.Up : view.up;
            motor.SetMoveInput(mapper.Map(input.Move, view.forward, cameraUp, motor.Gravity.Up));

            if (input.JumpPressed)
                motor.RequestJump();
        }
    }
}
