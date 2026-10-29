using System.Numerics;

namespace XivHud.Windows.Core;

/// <summary>
/// Local companion motion, using Ghostty's lua/world.lua pet tuning and
/// lua/motion.lua bounded spring integration. No camera, terminal or game input.
/// </summary>
public sealed class CompanionMotion
{
    private bool initialized;
    private Vector3 previousPlayer, position, velocity;
    private float heading, speed, time, orbitPhase, calm;
    public Vector3 Position => position;
    public Vector3 Velocity => velocity;
    public float Speed => speed;

    public void Reset() => initialized = false;

    public Vector3 Update(Vector3 player, float facing, float deltaTime, int order,
        float width, float height, bool held = false, bool orbit = false, bool reduce = false)
    {
        if (!Finite(player) || !float.IsFinite(facing) || !float.IsFinite(deltaTime)) return position;
        var dt = Math.Clamp(deltaTime, 0, .1f);
        order = Math.Clamp(order, 0, 15);
        width = float.IsFinite(width) ? Math.Clamp(width, .5f, 6) : 1.2f;
        height = float.IsFinite(height) ? Math.Clamp(height, .1f, 12) : 1;
        var reset = !initialized || Vector3.DistanceSquared(player, previousPlayer) > 400;
        if (reset) { heading = facing; speed = 0; velocity = Vector3.Zero; calm = held ? 1 : 0; }
        else if (dt > 0)
        {
            var movement = new Vector2(player.X - previousPlayer.X, player.Z - previousPlayer.Z).Length() / dt;
            speed += (movement - speed) * (1 - MathF.Exp(-dt * 8));
            // Ghostty holds the frame steady while idle, including turns to read a window.
            if (speed > .6f) heading += Wrap(facing - heading) * (1 - MathF.Exp(-dt * 2.5f));
        }
        previousPlayer = player;
        time += dt;
        calm += ((held ? 1 : 0) - calm) * (1 - MathF.Exp(-dt / .25f));
        if (orbit && !held && !reduce) orbitPhase = Wrap(orbitPhase + dt * .25f);
        var level = order / 2;
        var side = order % 2 == 0 ? 1 : -1;
        var run = Math.Clamp((speed - .6f) / 4.9f, 0, 1);
        var angle = 1.45f + level * .55f + (reduce ? 0 : run * .35f);
        var maxAngle = MathF.PI - .8f;
        var extra = MathF.Max(0, MathF.Ceiling((angle - maxAngle) / .55f)) * 1.1f;
        angle = heading + side * MathF.Min(angle, maxAngle) + (orbit ? orbitPhase : 0);
        var radius = MathF.Max(2.3f, width / 2 + .9f) + extra + level * .55f;
        var phase = order * 2.399963f;
        var bob = reduce ? 0 : .02f * MathF.Sin(time * .6f + phase) * (1 - calm);
        var target = player + new Vector3(MathF.Sin(angle) * radius,
            MathF.Max(1.6f, height / 2 + .2f) + (reduce ? 0 : level * .12f) + bob, MathF.Cos(angle) * radius);
        if (reset || reduce)
        {
            initialized = true; position = target; velocity = Vector3.Zero;
            return position;
        }
        if (dt <= 0) return position;

        // Aim around the character one radian at a time, rather than cutting across it.
        var currentAngle = MathF.Atan2(position.X - player.X, position.Z - player.Z);
        var arc = Wrap(angle - currentAngle);
        if (MathF.Abs(arc) > 1)
        {
            var nextAngle = currentAngle + MathF.CopySign(1, arc);
            target.X = player.X + MathF.Sin(nextAngle) * radius;
            target.Z = player.Z + MathF.Cos(nextAngle) * radius;
        }
        var stiffness = 5 * MathF.Max(.55f, 1 - .04f * order);
        var w = 2 * MathF.Sqrt(stiffness);
        var steps = Math.Max(1, (int)MathF.Ceiling(dt * 120));
        var h = dt / steps;
        var damping = 1 / (1 + 2 * .95f * w * h);
        for (var i = 0; i < steps; i++)
        {
            velocity = (velocity + w * w * (target - position) * h) * damping;
            position += velocity * h;
        }
        // A direction change must never take a panel through the character's body.
        var offset = new Vector3(position.X - player.X, 0, position.Z - player.Z);
        var minimumRadius = width / 2 + .6f;
        if (offset.LengthSquared() < minimumRadius * minimumRadius)
        {
            var normal = offset.LengthSquared() > .0001f ? Vector3.Normalize(offset) : new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
            var edge = player + normal * minimumRadius;
            position.X = edge.X; position.Z = edge.Z;
            var inward = Vector3.Dot(velocity, normal);
            if (inward < 0) velocity -= normal * inward;
        }
        return position;
    }

    private static float Wrap(float angle) => MathF.Atan2(MathF.Sin(angle), MathF.Cos(angle));
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
