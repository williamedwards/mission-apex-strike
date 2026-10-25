using System.Text;
using GravBoots.Cameras;
using GravBoots.Player;
using UnityEngine;

namespace GravBoots.Debugging
{
    /// <summary>Text readout of the motor and camera state, plus vector rays in the Scene view.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerController controller;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] DebugGravityKeys keys;
        [SerializeField] CameraPopProbe popProbe;
        [SerializeField] bool showHelp = true;

        readonly StringBuilder text = new StringBuilder();
        GUIStyle style;

        void Update()
        {
            if (motor == null)
                return;
            Vector3 centre = motor.transform.position + motor.BodyUp * 0.9f;
            Debug.DrawRay(centre, motor.Gravity.Up * 1.5f, Color.green);
            Debug.DrawRay(centre, motor.BodyUp * 1.2f, Color.cyan);
            Debug.DrawRay(centre, motor.MoveInput * 1.5f, Color.yellow);
            if (motor.IsGrounded)
                Debug.DrawRay(motor.transform.position, motor.GroundNormal, Color.magenta);
        }

        void OnGUI()
        {
            if (motor == null)
                return;
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                padding = new RectOffset(8, 8, 6, 6),
            };

            Vector3 up = motor.Gravity.Up;
            float vertical = Vector3.Dot(motor.Velocity, up);
            float lateral = Vector3.ProjectOnPlane(motor.Velocity, up).magnitude;

            text.Clear();
            text.AppendLine($"<b>Gravity up</b>  {Fmt(up)}{(motor.Gravity.IsBlending ? "  (blending)" : "")}");
            text.AppendLine($"<b>Body up</b>     {Fmt(motor.BodyUp)}  Δ {Vector3.Angle(up, motor.BodyUp):0}°");
            text.AppendLine(motor.IsGrounded
                ? $"<b>Grounded</b>    slope {Vector3.Angle(motor.GroundNormal, up):0}°  on {motor.GroundCollider?.name}"
                : $"<b>Airborne</b>{(motor.OnSteepSurface ? "    (on steep surface)" : "")}");
            text.AppendLine($"<b>Speed</b>       lateral {lateral:0.0}  vertical {vertical:0.0}");
            if (cameraRig != null)
                text.AppendLine($"<b>Camera</b>      {cameraRig.Mode}  up Δ {Vector3.Angle(cameraRig.Up, motor.BodyUp):0}°");
            if (popProbe != null)
                text.AppendLine($"<b>Camera pops</b> {popProbe.PopCount}  largest {popProbe.LargestPop:0.00} m  clipped frames {popProbe.ClippedFrames}  last: {popProbe.LastCause}");
            if (controller != null)
                text.AppendLine($"<b>Move map</b>    {(controller.Mapper.IsHoldingMapping ? "held through transition" : "camera")}");
            if (keys != null)
                text.AppendLine($"<b>Area</b>        {keys.CurrentSpawn?.label}   gravity change: {(keys.Blend ? "blend" : "instant")}");
            if (showHelp)
            {
                text.AppendLine();
                text.AppendLine("1 floor  2 ceiling  3/4 ±X wall  5/6 ±Z wall  G / MMB face under crosshair");
                text.AppendLine("B instant/blend  C camera mode  N next area  R reset  F3 overlay  Esc cursor");
            }

            var content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect(10, 10, size.x, size.y), content, style);

            // Crosshair for the "face under crosshair" key.
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.Box(new Rect(cx - 2, cy - 2, 4, 4), GUIContent.none);
        }

        static string Fmt(Vector3 v) => $"({v.x,5:0.00}, {v.y,5:0.00}, {v.z,5:0.00})";
    }
}
