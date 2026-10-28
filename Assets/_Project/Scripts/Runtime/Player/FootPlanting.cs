using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// While magnetized, a foot that comes down stays exactly where it landed (pinned with the
    /// Animator's foot IK) until the walk lifts it again, so magnetized feet grip instead of
    /// sliding when speeding up, slowing down or turning. A foot pulled too far from where the
    /// animation wants it lets go and steps. Pins are kept relative to the ground's transform,
    /// so they ride moving platforms, and heights are measured along the body's up, so it works
    /// on walls and ceilings. Sits on the Animator's object (it receives OnAnimatorIK).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FootPlanting : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] MagneticBoots boots;
        [SerializeField] Animator animator;
        [Tooltip("An animated foot (ankle) lower than this above the body's base, in metres, counts as down.")]
        [SerializeField] float plantHeight = 0.18f;
        [Tooltip("A pinned foot further than this from its animated position lets go and steps.")]
        [SerializeField] float maxStretch = 0.25f;
        [Tooltip("Seconds to blend a foot onto or off its pin.")]
        [SerializeField] float blendTime = 0.08f;

        readonly Foot left = new Foot(AvatarIKGoal.LeftFoot);
        readonly Foot right = new Foot(AvatarIKGoal.RightFoot);

        class Foot
        {
            public readonly AvatarIKGoal Goal;
            public bool Pinned;
            public Transform Frame;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Weight;

            public Foot(AvatarIKGoal goal) => Goal = goal;
        }

        public void Bind(PlayerMotor playerMotor, MagneticBoots playerBoots, Animator playerAnimator)
        {
            motor = playerMotor;
            boots = playerBoots;
            animator = playerAnimator;
        }

        /// <summary>Whether the left (or right) foot is pinned now; for tests and debugging.</summary>
        public bool IsPinned(bool leftFoot) => (leftFoot ? left : right).Pinned;

        void OnDisable()
        {
            // Animator IK weights reset each frame, so the feet return to the animation on their own.
            foreach (Foot foot in new[] { left, right })
            {
                foot.Pinned = false;
                foot.Weight = 0f;
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!enabled || motor == null || animator == null)
                return;
            bool active = boots != null && boots.IsMagnetized && motor.IsGrounded;
            Transform ground = motor.GroundCollider != null ? motor.GroundCollider.transform : null;
            Plant(left, active, ground);
            Plant(right, active, ground);
        }

        void Plant(Foot foot, bool active, Transform ground)
        {
            Vector3 animated = animator.GetIKPosition(foot.Goal);
            Quaternion animatedRotation = animator.GetIKRotation(foot.Goal);
            bool down = active && IsDown(animated, motor.Position, motor.BodyUp, plantHeight);

            if (foot.Pinned)
            {
                foot.Position = foot.Frame != null ? foot.Frame.TransformPoint(foot.LocalPosition) : foot.LocalPosition;
                foot.Rotation = foot.Frame != null ? foot.Frame.rotation * foot.LocalRotation : foot.LocalRotation;
                if (!down || foot.Frame != ground || Overstretched(foot.Position, animated, maxStretch))
                    foot.Pinned = false;
            }
            else if (down)
            {
                foot.Pinned = true;
                foot.Frame = ground;
                foot.Position = animated;
                foot.Rotation = animatedRotation;
                foot.LocalPosition = ground != null ? ground.InverseTransformPoint(animated) : animated;
                foot.LocalRotation = ground != null ? Quaternion.Inverse(ground.rotation) * animatedRotation : animatedRotation;
            }

            float step = blendTime > 0f ? Time.deltaTime / blendTime : 1f;
            foot.Weight = Mathf.MoveTowards(foot.Weight, foot.Pinned ? 1f : 0f, step);
            animator.SetIKPositionWeight(foot.Goal, foot.Weight);
            animator.SetIKRotationWeight(foot.Goal, foot.Weight);
            if (foot.Weight > 0f)
            {
                // Blending off a released pin eases from where it was, not from a jump.
                animator.SetIKPosition(foot.Goal, foot.Position);
                animator.SetIKRotation(foot.Goal, foot.Rotation);
            }
        }

        /// <summary>Whether a foot at <paramref name="foot"/> is down: within <paramref name="height"/> of the base along <paramref name="up"/>.</summary>
        public static bool IsDown(Vector3 foot, Vector3 basePosition, Vector3 up, float height) =>
            Vector3.Dot(foot - basePosition, up) < height;

        /// <summary>Whether a pinned foot has been pulled too far from where the animation has it.</summary>
        public static bool Overstretched(Vector3 pinned, Vector3 animated, float maxStretch) =>
            (pinned - animated).sqrMagnitude > maxStretch * maxStretch;
    }
}
