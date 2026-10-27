using GravBoots.Level;
using UnityEngine;

namespace GravBoots.Player
{
    /// <summary>
    /// Shows where a drop would land: a ring projected straight down (world gravity) from the
    /// player, plus a guide line, whenever the player is on a wall or ceiling or already
    /// falling. Red over lethal surfaces (<see cref="Layers.LethalTag"/>) and when the drop
    /// passes through a <see cref="KillVolume"/> (a death pit). Core to the drop
    /// puzzles, not polish (docs/PLAN.md §1.3).
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.AfterCamera)]
    public class LandingMarker : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] MagneticBoots boots;
        [SerializeField] Renderer ring;
        [SerializeField] LineRenderer guide;
        [SerializeField] Color safeColor = new Color(0.35f, 0.8f, 1f);
        [SerializeField] Color lethalColor = new Color(1f, 0.2f, 0.15f);
        [Tooltip("Only show the marker once the player's up is at least this far from world up.")]
        [SerializeField] float minimumTilt = 20f;
        [SerializeField] float maxDistance = 200f;

        readonly RaycastHit[] triggerHits = new RaycastHit[8];
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public bool IsShowing { get; private set; }
        public bool IsLethal { get; private set; }
        public Vector3 LandingPoint { get; private set; }

        void LateUpdate()
        {
            RaycastHit hit = default;
            IsShowing = ShouldShow() && Project(out hit);
            if (ring != null)
                ring.enabled = IsShowing;
            if (guide != null)
                guide.enabled = IsShowing;
            if (!IsShowing)
                return;

            IsLethal = hit.collider.CompareTag(Layers.LethalTag) || CrossesKillVolume(hit.distance);
            LandingPoint = hit.point;
            Color color = IsLethal ? lethalColor : safeColor;

            if (ring != null)
            {
                ring.transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f,
                    Quaternion.FromToRotation(Vector3.up, hit.normal));
                block ??= new MaterialPropertyBlock();
                block.SetColor(BaseColor, color);
                ring.SetPropertyBlock(block);
            }
            if (guide != null)
            {
                guide.SetPosition(0, Centre());
                guide.SetPosition(1, hit.point);
                guide.startColor = new Color(color.r, color.g, color.b, 0f);
                guide.endColor = color;
            }
        }

        bool ShouldShow()
        {
            if (motor == null || boots == null)
                return false;
            switch (boots.State)
            {
                case BootsState.Magnetized:
                    return Vector3.Angle(motor.Gravity.Up, Vector3.up) >= minimumTilt;
                case BootsState.Dropping:
                    return !motor.IsGrounded;
                default:
                    return false;
            }
        }

        bool CrossesKillVolume(float distance)
        {
            int count = Physics.SphereCastNonAlloc(Centre(), 0.3f, Vector3.down, triggerHits, distance,
                motor.CollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (triggerHits[i].collider.isTrigger && triggerHits[i].collider.TryGetComponent(out KillVolume _))
                    return true;
            }
            return false;
        }

        Vector3 Centre() => motor.transform.position + motor.transform.up * 0.9f;

        bool Project(out RaycastHit hit)
        {
            // The same sphere the player's body sweeps when falling, so the ring matches the landing.
            return Physics.SphereCast(Centre(), 0.3f, Vector3.down, out hit, maxDistance,
                motor.CollisionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
