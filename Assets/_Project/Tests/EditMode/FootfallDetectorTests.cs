using GravBoots.Audio;
using NUnit.Framework;

namespace GravBoots.Tests
{
    public class FootfallDetectorTests
    {
        const float Frame = 1f / 60f;

        // Left ankle heights (m above the body's base) at 60 Hz from the clips, from mid-swing
        // to just after the heel settles; the landing frame is noted for each.
        static readonly float[] Run = { 0.40f, 0.33f, 0.27f, 0.23f, 0.210f, 0.196f, 0.181f, 0.171f, 0.163f, 0.154f, 0.147f, 0.143f, 0.139f, 0.141f, 0.150f };
        const int RunLanding = 11; // 0.143: the frame the descent drops to 0.24 m/s
        static readonly float[] PurposefulWalk = { 0.31f, 0.28f, 0.25f, 0.23f, 0.213f, 0.196f, 0.180f, 0.168f, 0.162f, 0.161f, 0.165f, 0.18f };
        const int WalkLanding = 9; // 0.161, settled

        static int FirstStep(float[] heights, int stride = 1)
        {
            var fall = new FootfallDetector();
            for (int i = 0; i < heights.Length; i += stride)
                if (fall.Update(heights[i], Frame * stride))
                    return i;
            return -1;
        }

        [Test]
        public void StepsWhenTheHeelSettlesNotWhenItPassesLowHeight()
        {
            Assert.That(FirstStep(Run), Is.EqualTo(RunLanding));
            Assert.That(FirstStep(PurposefulWalk), Is.EqualTo(WalkLanding));
        }

        [Test]
        public void LowFrameRatesStepWithinAFrameOfLanding()
        {
            // At 30 fps the samples skip the landing frame; the step comes by the next one.
            Assert.That(FirstStep(Run, 2), Is.InRange(RunLanding, RunLanding + 3));
            Assert.That(FirstStep(PurposefulWalk, 2), Is.InRange(WalkLanding, WalkLanding + 2));
        }

        [Test]
        public void StepsOncePerLiftAndNotBeforeTheFirstLift()
        {
            var fall = new FootfallDetector();
            int steps = 0;
            // Standing, a foot resting low never steps.
            for (int i = 0; i < 30; i++)
                steps += fall.Update(0.15f, Frame) ? 1 : 0;
            Assert.That(steps, Is.EqualTo(0));
            // Two full strides: two steps, however long the foot rests between them.
            for (int stride = 0; stride < 2; stride++)
            {
                foreach (float h in Run)
                    steps += fall.Update(h, Frame) ? 1 : 0;
                for (int i = 0; i < 20; i++)
                    steps += fall.Update(0.141f, Frame) ? 1 : 0;
            }
            Assert.That(steps, Is.EqualTo(2));
        }
    }
}
