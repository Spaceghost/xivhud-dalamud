using System.Numerics;
using XivHud.Windows.Core;

namespace XivHud.Windows.Tests;

public class CompanionMotionTests
{
    private static Vector3 Step(CompanionMotion motion, Vector3 player, float facing = 0,
        float dt = 1f / 60, int order = 0, bool held = true, bool orbit = false, bool reduce = false)
        => motion.Update(player, facing, dt, order, 1.2f, .8f, held, orbit, reduce);

    [Fact] public void RunningTrailsBehindAndSettlesAfterStopping()
    {
        var motion = new CompanionMotion();
        Step(motion, Vector3.Zero);
        var player = Vector3.Zero;
        for (var i = 0; i < 120; i++) { player.Z += .1f; Step(motion, player); }
        Assert.True(motion.Position.Z < player.Z - 1);
        var stopped = motion.Position;
        for (var i = 0; i < 360; i++) Step(motion, player);
        Assert.True(motion.Position.Z > stopped.Z + 1);
        Assert.InRange(new Vector2(motion.Position.X - player.X, motion.Position.Z - player.Z).Length(), 2.29f, 2.31f);
        Assert.InRange(motion.Velocity.Length(), 0, .01f);
    }

    [Fact] public void TurningWhileMovingTakesTheWindowAroundThePlayer()
    {
        var motion = new CompanionMotion();
        Step(motion, Vector3.Zero);
        var previous = motion.Position;
        for (var i = 0; i < 300; i++)
        {
            var player = new Vector3(0, 0, i * .02f);
            var position = Step(motion, player, MathF.PI);
            Assert.True(Vector3.Distance(previous, position) < .15f);
            Assert.True(new Vector2(position.X - player.X, position.Z - player.Z).Length() >= 1.199f);
            previous = position;
        }
        Assert.True(motion.Position.X < -1);
    }

    [Fact] public void IdleFacingChangesDoNotMakeTheWindowRunAway()
    {
        var motion = new CompanionMotion();
        var initial = Step(motion, Vector3.Zero);
        for (var i = 0; i < 180; i++) Step(motion, Vector3.Zero, MathF.PI);
        Assert.Equal(initial, motion.Position);
    }

    [Fact] public void FollowOrderUsesOppositeSidesAndStaggeredSprings()
    {
        var first = new CompanionMotion(); var second = new CompanionMotion(); var later = new CompanionMotion();
        var a = Step(first, Vector3.Zero); var b = Step(second, Vector3.Zero, order: 1);
        Step(later, Vector3.Zero, order: 2);
        Assert.True(a.X > 0 && b.X < 0);
        Assert.True(Vector3.Distance(a, b) > 4);
        for (var i = 0; i < 120; i++)
        {
            var player = new Vector3(0, 0, i * .1f);
            Step(first, player); Step(later, player, order: 2);
        }
        Assert.True(later.Position.Z < first.Position.Z);
    }

    [Fact] public void ExplicitOrbitCirclesButPausesWhileReading()
    {
        var motion = new CompanionMotion();
        var initial = Step(motion, Vector3.Zero, held: false, orbit: true);
        for (var i = 0; i < 360; i++) Step(motion, Vector3.Zero, held: false, orbit: true);
        Assert.True(Vector3.Distance(initial, motion.Position) > 2);
        for (var i = 0; i < 360; i++) Step(motion, Vector3.Zero, orbit: true);
        var settled = motion.Position;
        for (var i = 0; i < 120; i++) Step(motion, Vector3.Zero, orbit: true);
        Assert.InRange(Vector3.Distance(settled, motion.Position), 0, .001f);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(144)]
    public void FollowingIsConsistentAcrossFrameRates(int fps)
    {
        static Vector3 Run(int frames)
        {
            var motion = new CompanionMotion(); Step(motion, Vector3.Zero, dt: 1f / frames);
            for (var i = 1; i <= frames * 3; i++) Step(motion, new(0, 0, i * 6f / frames), dt: 1f / frames);
            return motion.Position;
        }
        Assert.InRange(Vector3.Distance(Run(fps), Run(120)), 0, .17f);
    }

    [Fact] public void TeleportsAndZoneResetsDoNotLeaveAnOldSpringFlyingAcrossTheMap()
    {
        var motion = new CompanionMotion(); Step(motion, Vector3.Zero);
        var destination = new Vector3(500, 30, -700);
        Assert.InRange(Vector3.Distance(Step(motion, destination), destination), 2, 4);
        Assert.Equal(Vector3.Zero, motion.Velocity);
        motion.Reset();
        Assert.InRange(Vector3.Distance(Step(motion, Vector3.Zero), Vector3.Zero), 2, 4);
    }

    [Fact] public void ReducedMotionRemovesTrailingBobbingAndAutomaticOrbit()
    {
        var motion = new CompanionMotion();
        var initial = Step(motion, Vector3.Zero, held: false, orbit: true, reduce: true);
        var player = new Vector3(0, 0, 6);
        for (var i = 0; i < 120; i++) Step(motion, player, held: false, orbit: true, reduce: true);
        Assert.InRange(Vector3.Distance(initial + player, motion.Position), 0, .001f);
        Assert.Equal(Vector3.Zero, motion.Velocity);
    }

    [Fact] public void HitchesAndInvalidInputsRemainBounded()
    {
        var motion = new CompanionMotion(); var initial = Step(motion, Vector3.Zero);
        Assert.Equal(initial, Step(motion, new(float.NaN)));
        Assert.Equal(initial, Step(motion, Vector3.Zero, dt: float.NaN));
        for (var i = 0; i < 100; i++) Step(motion, new(0,0,3), MathF.PI, dt: 10);
        Assert.True(float.IsFinite(motion.Position.X));
        Assert.InRange(Vector3.Distance(motion.Position, new(0,0,3)), 1.2f, 4);
    }
}
