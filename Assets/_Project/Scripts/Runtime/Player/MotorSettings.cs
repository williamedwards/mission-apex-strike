using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>Tuning values for <see cref="PlayerMotor"/>; editable while playing.</summary>
    [CreateAssetMenu(menuName = "GravBoots/Motor Settings", fileName = "MotorSettings")]
    public class MotorSettings : ScriptableObject
    {
        [Header("Ground movement")]
        public float moveSpeed = 6f;
        public float groundAcceleration = 60f;
        [Tooltip("Max angle (degrees) between the ground normal and Up that still counts as walkable.")]
        public float slopeLimit = 50f;
        public float stepHeight = 0.35f;
        [Tooltip("How far below the feet the motor looks for ground to stay snapped to.")]
        public float groundSnapDistance = 0.3f;

        [Header("Air")]
        public float airAcceleration = 20f;
        public float gravity = 25f;
        public float maxFallSpeed = 40f;
        public float jumpHeight = 1.3f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        [Header("Orientation")]
        [Tooltip("How fast the body rotates to match a new Up (degrees per second).")]
        public float alignDegreesPerSecond = 720f;
        [Tooltip("How fast the body turns to face the move direction (degrees per second).")]
        public float turnDegreesPerSecond = 720f;

        [Header("Collision")]
        public float skinWidth = 0.02f;
        public int maxSlideIterations = 4;

        public float JumpSpeed => Mathf.Sqrt(2f * gravity * jumpHeight);
    }
}
