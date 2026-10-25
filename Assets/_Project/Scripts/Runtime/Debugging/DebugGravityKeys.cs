using System.Linq;
using GravBoots.Cameras;
using GravBoots.Level;
using GravBoots.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravBoots.Debugging
{
    /// <summary>
    /// Stand-in for the boots while testing the motor and camera (Phase 1A): keys set the
    /// player's up to any axis or to the face under the crosshair, cycle test areas and switch
    /// camera modes. Bindings live in the "Debug" map of the input actions asset.
    /// </summary>
    public class DebugGravityKeys : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] MagneticBoots boots;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] DebugOverlay overlay;
        [SerializeField] float blendDegreesPerSecond = 180f;
        [SerializeField] float crosshairRange = 200f;

        InputAction floor, ceiling, wallPosX, wallNegX, wallPosZ, wallNegZ;
        InputAction toCrosshair, toggleBlend, cycleCamera, toggleOverlay, nextSpawn, resetPlayer;
        SpawnPoint[] spawns;
        int spawnIndex;

        public bool Blend { get; private set; }
        public SpawnPoint CurrentSpawn => spawns != null && spawns.Length > 0 ? spawns[spawnIndex] : null;

        void Start()
        {
            floor = input.Find("Debug/GravityFloor");
            ceiling = input.Find("Debug/GravityCeiling");
            wallPosX = input.Find("Debug/GravityWallPosX");
            wallNegX = input.Find("Debug/GravityWallNegX");
            wallPosZ = input.Find("Debug/GravityWallPosZ");
            wallNegZ = input.Find("Debug/GravityWallNegZ");
            toCrosshair = input.Find("Debug/GravityToCrosshair");
            toggleBlend = input.Find("Debug/ToggleBlend");
            cycleCamera = input.Find("Debug/CycleCameraMode");
            toggleOverlay = input.Find("Debug/ToggleOverlay");
            nextSpawn = input.Find("Debug/NextSpawn");
            resetPlayer = input.Find("Debug/ResetPlayer");

            spawns = FindObjectsByType<SpawnPoint>()
                .OrderBy(s => s.order).ThenBy(s => s.name).ToArray();
            if (CurrentSpawn != null)
                SendToSpawn(CurrentSpawn);
        }

        void Update()
        {
            // "Floor" means standing on a floor, so up points away from it.
            if (input.ConsumePress(floor)) SetUp(Vector3.up);
            if (input.ConsumePress(ceiling)) SetUp(Vector3.down);
            if (input.ConsumePress(wallPosX)) SetUp(Vector3.left);
            if (input.ConsumePress(wallNegX)) SetUp(Vector3.right);
            if (input.ConsumePress(wallPosZ)) SetUp(Vector3.back);
            if (input.ConsumePress(wallNegZ)) SetUp(Vector3.forward);
            if (input.ConsumePress(toCrosshair)) SetUpFromCrosshair();

            if (input.ConsumePress(toggleBlend)) Blend = !Blend;
            if (input.ConsumePress(cycleCamera) && cameraRig != null)
                cameraRig.Mode = (CameraUpMode)(((int)cameraRig.Mode + 1) % 3);
            if (input.ConsumePress(toggleOverlay) && overlay != null)
                overlay.enabled = !overlay.enabled;

            if (input.ConsumePress(nextSpawn) && spawns.Length > 0)
            {
                spawnIndex = (spawnIndex + 1) % spawns.Length;
                SendToSpawn(CurrentSpawn);
            }
            if (input.ConsumePress(resetPlayer) && CurrentSpawn != null)
                SendToSpawn(CurrentSpawn);
        }

        void SetUp(Vector3 up)
        {
            // A debug override replaces whatever the boots were doing.
            if (boots != null)
                boots.ForceOff();
            if (Blend)
                motor.Gravity.BlendUp(up, blendDegreesPerSecond);
            else
                motor.Gravity.SetUp(up);
        }

        void SetUpFromCrosshair()
        {
            Camera view = Camera.main;
            if (view == null)
                return;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, crosshairRange, motor.CollisionMask, QueryTriggerInteraction.Ignore))
                SetUp(hit.normal);
        }

        void SendToSpawn(SpawnPoint spawn)
        {
            if (boots != null)
                boots.ForceOff();
            motor.Gravity.SetUp(spawn.transform.up);
            motor.Teleport(spawn.transform.position, spawn.transform.rotation);
            if (cameraRig != null)
                cameraRig.SnapBehindTarget();
        }
    }
}
