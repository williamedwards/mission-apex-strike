namespace GravBoots.UI
{
    public enum ControlHint { None, Move, Jump, Magnetize, Demagnetize }

    /// <summary>What the player is doing this frame, as far as the control hints care.</summary>
    public struct HintSituation
    {
        /// <summary>The player has control (not arriving, dying or finished).</summary>
        public bool InControl;
        public bool Grounded;
        /// <summary>Move input is held.</summary>
        public bool Steering;
        /// <summary>Own speed across the ground (m/s), not counting a platform being ridden.</summary>
        public float Speed;
        public bool BootsOff;
        public bool Magnetized;
        /// <summary>Standing on a magnetic surface (where the boots can switch on).</summary>
        public bool OnMagneticGround;
        /// <summary>A magnetic curve is within reach.</summary>
        public bool NearMagneticRamp;
        /// <summary>A ledge low enough to jump onto is just ahead.</summary>
        public bool LedgeAhead;
        /// <summary>The player left the ground in a jump this frame.</summary>
        public bool Jumped;
    }

    /// <summary>
    /// Which control hint to show. Move shows until the player has walked a little; Jump shows
    /// once after that until the first jump, and again whenever the player stands at a ledge
    /// they could jump. Magnetize shows as soon as the player first stands on a magnetic surface
    /// with the boots off (until they first magnetize), and after that following a pause on a
    /// magnetic surface or near a magnetic curve. Demagnetize shows after a pause while
    /// magnetized. Progress (walked, jumped, magnetized) lasts across chambers and respawns.
    /// </summary>
    public class HintRules
    {
        /// <summary>Standing still this long counts as a pause.</summary>
        public const float PauseSeconds = 1.5f;
        /// <summary>Stopped at a ledge (standing or pushing into it) this long brings up Jump.</summary>
        public const float LedgeSeconds = 1f;
        /// <summary>Walking this far (with move input) retires the Move hint.</summary>
        public const float LearnMoveDistance = 2f;
        /// <summary>How long the first Jump hint stays up after Move is learned, unless the player jumps.</summary>
        public const float JumpIntroSeconds = 5f;
        const float StillSpeed = 0.3f;

        float walked, stillTime, ledgeTime, jumpIntroLeft = JumpIntroSeconds;
        bool learnedJump, learnedMagnetize, wasMagnetized;

        public bool LearnedMove => walked >= LearnMoveDistance;
        public bool LearnedJump => learnedJump;
        public bool LearnedMagnetize => learnedMagnetize;

        public ControlHint Update(in HintSituation s, float deltaTime)
        {
            if (!s.InControl)
            {
                stillTime = ledgeTime = 0f;
                return ControlHint.None;
            }

            if (s.Jumped)
                learnedJump = true;
            if (s.Magnetized)
                learnedMagnetize = true;
            bool still = !s.Steering && s.Speed < StillSpeed;
            // Switching the boots is acting, not pausing: count afresh from there.
            if (s.Magnetized != wasMagnetized)
                stillTime = 0f;
            wasMagnetized = s.Magnetized;
            stillTime = still && s.Grounded ? stillTime + deltaTime : 0f;
            if (s.Steering && s.Grounded)
                walked += s.Speed * deltaTime;
            bool atLedge = s.LedgeAhead && s.Grounded && s.BootsOff && s.Speed < StillSpeed;
            ledgeTime = atLedge ? ledgeTime + deltaTime : 0f;

            if (s.Magnetized && stillTime >= PauseSeconds)
                return ControlHint.Demagnetize;
            bool canMagnetize = s.BootsOff && s.Grounded && s.OnMagneticGround;
            if (canMagnetize && !learnedMagnetize)
                return ControlHint.Magnetize;
            if (s.BootsOff && (s.OnMagneticGround || s.NearMagneticRamp) && stillTime >= PauseSeconds)
                return ControlHint.Magnetize;
            if (!LearnedMove)
                return ControlHint.Move;
            if (ledgeTime >= LedgeSeconds)
                return ControlHint.Jump;
            if (!learnedJump && jumpIntroLeft > 0f && s.BootsOff)
            {
                jumpIntroLeft -= deltaTime;
                return ControlHint.Jump;
            }
            return ControlHint.None;
        }
    }
}
