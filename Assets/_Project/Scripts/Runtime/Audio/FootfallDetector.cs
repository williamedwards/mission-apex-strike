namespace GravBoots.Audio
{
    /// <summary>
    /// Finds the moment one foot plants, from its height above the body's base each frame. A
    /// foot must lift past <see cref="LiftHeight"/> first; it has then planted once it is back
    /// below <see cref="LowHeight"/> and has (nearly) stopped coming down. Waiting for it to stop,
    /// rather than for it to pass a height, puts the sound on the heel settling: in the walk and
    /// run clips the ankle passes 0.2 m some 0.05-0.1 s before it lands.
    /// </summary>
    public struct FootfallDetector
    {
        public const float LowHeight = 0.2f, LiftHeight = 0.26f;
        /// <summary>Descent speed (m/s) below which the foot counts as landed.</summary>
        public const float SettleSpeed = 0.3f;

        bool lifted, hasLast;
        float lastHeight;

        /// <summary>Feeds this frame's foot height; true on the frame the foot plants.</summary>
        public bool Update(float height, float deltaTime)
        {
            float descent = hasLast && deltaTime > 0f ? (lastHeight - height) / deltaTime : float.MaxValue;
            lastHeight = height;
            hasLast = true;
            if (!lifted)
            {
                lifted = height > LiftHeight;
                return false;
            }
            if (height < LowHeight && descent < SettleSpeed)
            {
                lifted = false;
                return true;
            }
            return false;
        }
    }
}
