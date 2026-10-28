using GravBoots.UI;
using NUnit.Framework;

namespace GravBoots.Tests
{
    public class HintRulesTests
    {
        const float Frame = 1f / 60f;

        static HintSituation Standing(bool bootsOff = true) => new HintSituation
        {
            InControl = true, Grounded = true, BootsOff = bootsOff, Magnetized = !bootsOff,
        };

        static HintSituation Walking() => new HintSituation
        {
            InControl = true, Grounded = true, BootsOff = true, Steering = true, Speed = 4f,
        };

        /// <summary>Feeds the same situation for <paramref name="seconds"/>; returns the last hint.</summary>
        static ControlHint Hold(HintRules rules, HintSituation s, float seconds)
        {
            ControlHint hint = ControlHint.None;
            for (float t = 0f; t < seconds; t += Frame)
                hint = rules.Update(s, Frame);
            return hint;
        }

        static HintRules Walked()
        {
            var rules = new HintRules();
            Hold(rules, Walking(), 1f);
            return rules;
        }

        [Test]
        public void MoveShowsUntilThePlayerHasWalkedALittle()
        {
            var rules = new HintRules();
            Assert.That(Hold(rules, Standing(), 3f), Is.EqualTo(ControlHint.Move));
            Assert.That(Hold(rules, Walking(), 0.25f), Is.EqualTo(ControlHint.Move));
            Hold(rules, Walking(), 0.5f);
            Assert.That(rules.LearnedMove, Is.True);
        }

        [Test]
        public void NothingShowsWithoutControl()
        {
            var rules = new HintRules();
            var arriving = Standing();
            arriving.InControl = false;
            Assert.That(Hold(rules, arriving, 3f), Is.EqualTo(ControlHint.None));
        }

        [Test]
        public void JumpIntroFollowsMoveAndEndsWithAJumpOrInTime()
        {
            var rules = Walked();
            Assert.That(rules.Update(Walking(), Frame), Is.EqualTo(ControlHint.Jump));
            var jump = Walking();
            jump.Jumped = true;
            rules.Update(jump, Frame);
            Assert.That(rules.Update(Walking(), Frame), Is.EqualTo(ControlHint.None));

            var unhurried = Walked();
            Assert.That(Hold(unhurried, Walking(), HintRules.JumpIntroSeconds + 0.5f), Is.EqualTo(ControlHint.None));
        }

        [Test]
        public void JumpShowsAtALedgeEvenAfterItIsLearned()
        {
            var rules = Walked();
            var jump = Walking();
            jump.Jumped = true;
            rules.Update(jump, Frame);

            var pushing = Walking();
            pushing.Speed = 0f;
            pushing.LedgeAhead = true;
            Assert.That(Hold(rules, pushing, HintRules.LedgeSeconds * 0.5f), Is.EqualTo(ControlHint.None));
            Assert.That(Hold(rules, pushing, HintRules.LedgeSeconds), Is.EqualTo(ControlHint.Jump));
            Assert.That(rules.Update(Walking(), Frame), Is.EqualTo(ControlHint.None));
        }

        [Test]
        public void MagnetizeShowsAfterAPauseNearAMagneticRamp()
        {
            var rules = Walked();
            Hold(rules, Walking(), HintRules.JumpIntroSeconds + 0.5f);
            var nearRamp = Standing();
            nearRamp.NearMagneticRamp = true;
            Assert.That(Hold(rules, nearRamp, HintRules.PauseSeconds * 0.5f), Is.EqualTo(ControlHint.None));
            Assert.That(Hold(rules, nearRamp, HintRules.PauseSeconds), Is.EqualTo(ControlHint.Magnetize));
            // Walking on hides it; far from a ramp a pause shows nothing.
            Assert.That(rules.Update(Walking(), Frame), Is.EqualTo(ControlHint.None));
            Assert.That(Hold(rules, Standing(), HintRules.PauseSeconds * 2f), Is.EqualTo(ControlHint.None));
        }

        [Test]
        public void FirstMagneticSurfaceShowsMagnetizeAtOnceUntilUsed()
        {
            var rules = Walked();
            var crossing = Walking();
            crossing.OnMagneticGround = true;
            Assert.That(rules.Update(crossing, Frame), Is.EqualTo(ControlHint.Magnetize));
            // Off the patch it goes; back on, it returns until the boots have been on once.
            Assert.That(rules.Update(Walking(), Frame), Is.Not.EqualTo(ControlHint.Magnetize));
            Assert.That(rules.Update(crossing, Frame), Is.EqualTo(ControlHint.Magnetize));
            var on = Standing(bootsOff: false);
            on.OnMagneticGround = true;
            rules.Update(on, Frame);
            Assert.That(rules.LearnedMagnetize, Is.True);
            // Learned: walking across a patch shows nothing; pausing on one brings it back.
            Assert.That(rules.Update(crossing, Frame), Is.Not.EqualTo(ControlHint.Magnetize));
            var paused = Standing();
            paused.OnMagneticGround = true;
            Assert.That(Hold(rules, paused, HintRules.PauseSeconds * 1.2f), Is.EqualTo(ControlHint.Magnetize));
        }

        [Test]
        public void DemagnetizeShowsAfterAPauseWhileMagnetized()
        {
            var rules = Walked();
            var stuck = Standing(bootsOff: false);
            stuck.NearMagneticRamp = true;
            Assert.That(Hold(rules, stuck, HintRules.PauseSeconds * 1.2f), Is.EqualTo(ControlHint.Demagnetize));
            var walkingStuck = stuck;
            walkingStuck.Steering = true;
            walkingStuck.Speed = 3f;
            Assert.That(rules.Update(walkingStuck, Frame), Is.Not.EqualTo(ControlHint.Demagnetize));
        }

        [Test]
        public void APauseCountsFromWhenTheBootsSwitch()
        {
            var rules = Walked();
            var nearRamp = Standing();
            nearRamp.NearMagneticRamp = true;
            Assert.That(Hold(rules, nearRamp, HintRules.PauseSeconds * 1.2f), Is.EqualTo(ControlHint.Magnetize));
            // Magnetizing without moving: Demagnetize waits for a fresh pause.
            var stuck = Standing(bootsOff: false);
            Assert.That(Hold(rules, stuck, HintRules.PauseSeconds * 0.5f), Is.Not.EqualTo(ControlHint.Demagnetize));
            Assert.That(Hold(rules, stuck, HintRules.PauseSeconds), Is.EqualTo(ControlHint.Demagnetize));
        }

        [Test]
        public void ARideIsNotAPauseUnlessThePlayerIsStill()
        {
            // Speed is the player's own, so standing on a moving platform still counts as a pause.
            var rules = Walked();
            var riding = Standing(bootsOff: false);
            Assert.That(Hold(rules, riding, HintRules.PauseSeconds * 1.2f), Is.EqualTo(ControlHint.Demagnetize));
        }
    }
}
