namespace GravBoots
{
    /// <summary>
    /// Script execution order for the simulation (see docs/PLAN.md §1.6):
    /// level clock → movers and hazards → player motor → camera rig → Cinemachine brain.
    /// </summary>
    public static class ExecutionOrder
    {
        public const int LevelClock = -300;
        public const int Movers = -200;
        public const int Input = -150;
        public const int Motor = -100;
        public const int CameraRig = -50;
        /// <summary>After the Cinemachine brain (default order 0) has placed the camera.</summary>
        public const int AfterCamera = 100;
    }
}
