using GravBoots.Cameras;
using Unity.Cinemachine;
using UnityEngine;

namespace GravBoots.Debugging
{
    /// <summary>
    /// Flags sudden camera jumps: frames where the camera moves faster than
    /// <see cref="popSpeed"/> relative to the orbit pivot. Measuring in pivot space ignores
    /// smooth player movement and mouse look, so what's left is the rig itself popping,
    /// usually obstacle avoidance. Each pop is logged with what caused it.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.AfterCamera)]
    public class CameraPopProbe : MonoBehaviour
    {
        [SerializeField] CameraRig cameraRig;
        [Tooltip("Camera speed relative to the pivot (m/s) that counts as a pop. Measured as a speed so the result doesn't depend on frame rate. The rig's own pull-in is capped at CameraSettings.pullInSpeed.")]
        [SerializeField] float popSpeed = 20f;
        [Tooltip("Ignore movements smaller than this (metres), whatever their speed.")]
        [SerializeField] float minimumJump = 0.1f;
        [SerializeField] bool logPops = true;
        [Tooltip("Layers that count as solid when checking whether the camera is inside geometry.")]
        [SerializeField] LayerMask solidMask = ~0;

        Transform view;
        CinemachineThirdPersonFollow follow;
        Vector3 lastOffset, lastHand;
        float lastDistance, lastShoulder;
        bool hasLast;

        public int PopCount { get; private set; }
        public float LargestPop { get; private set; }
        public string LastCause { get; private set; } = "";
        /// <summary>Frames where the camera itself was inside geometry (the cost of smoothing).</summary>
        public int ClippedFrames { get; private set; }

        public void ResetStats()
        {
            PopCount = 0;
            LargestPop = 0f;
            ClippedFrames = 0;
            LastCause = "";
        }

        // Runs after the rig and the Cinemachine brain, so it sees this frame's final camera
        // and the time step that produced it.
        void LateUpdate()
        {
            if (cameraRig == null || cameraRig.Pivot == null)
                return;
            if (view == null)
                view = Camera.main != null ? Camera.main.transform : null;
            if (follow == null)
                follow = FindAnyObjectByType<CinemachineThirdPersonFollow>();
            if (view == null || follow == null)
                return;

            Transform pivot = cameraRig.Pivot;
            Quaternion toLocal = Quaternion.Inverse(pivot.rotation);
            Vector3 offset = toLocal * (view.position - pivot.position);
            follow.GetRigPositions(out _, out _, out Vector3 handWorld);
            Vector3 hand = toLocal * (handWorld - pivot.position);

            bool cut = Time.frameCount - cameraRig.LastSnapFrame <= 10;
            if (hasLast && !cut)
            {
                float jump = Vector3.Distance(offset, lastOffset);
                float speed = jump / Mathf.Max(Time.deltaTime, 1e-4f);
                if (jump > minimumJump && speed > popSpeed)
                {
                    float handJump = Vector3.Distance(hand, lastHand);
                    string obstacle = follow.CurrentObstacle != null ? follow.CurrentObstacle.name : "none";
                    LastCause = handJump > jump * 0.5f
                        ? $"shoulder ({handJump:0.00} m), obstacle {obstacle}"
                        : $"camera, obstacle {obstacle}";
                    PopCount++;
                    LargestPop = Mathf.Max(LargestPop, jump);
                    if (logPops)
                        Debug.Log($"[CameraPop] {jump:0.00} m in one frame ({speed:0} m/s): {LastCause} | distance {lastDistance:0.00} → {cameraRig.CurrentDistance:0.00}, shoulder {lastShoulder:0.00} → {cameraRig.ShoulderScale:0.00}", this);
                }
            }
            if (!cut && Physics.CheckSphere(view.position, 0.05f, solidMask, QueryTriggerInteraction.Ignore))
                ClippedFrames++;

            lastOffset = offset;
            lastHand = hand;
            lastDistance = cameraRig.CurrentDistance;
            lastShoulder = cameraRig.ShoulderScale;
            hasLast = true;
        }
    }
}
