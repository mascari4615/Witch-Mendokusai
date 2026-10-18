using System;
using System.Numerics;

namespace Dynamics
{
    public struct SpringState
    {
        public Vector3 Position;
        public Vector3 Velocity;
    }

    public struct SpringSettings
    {
        public float FrequencyHz;
        public float DampingRatio;
        public float Radius;
        public Vector3 Acceleration;
    }

    public struct CapsuleObstacle
    {
        public Vector3 Start;
        public Vector3 End;
        public float Radius;
    }

    public static class SpringParticle
    {
        public static SpringState Step(SpringState state, Vector3 target, Vector3 anchor, float length, SpringSettings settings, CapsuleObstacle[] obstacles, float seconds)
        {
            Check(state.Position); Check(state.Velocity); Check(target); Check(anchor); Check(settings.Acceleration);
            Range(settings.FrequencyHz, .1f, 30); Range(settings.DampingRatio, 0, 4); Range(settings.Radius, 0, 100);
            Range(length, .00001f, 10000); Range(seconds, 0, 1f / 30);
            if (obstacles == null || obstacles.Length > 256) throw new ArgumentException("Invalid obstacle collection");
            foreach (CapsuleObstacle obstacle in obstacles) { Check(obstacle.Start); Check(obstacle.End); Range(obstacle.Radius, 0, 10000); }
            float omega = 2 * (float)Math.PI * settings.FrequencyHz;
            Vector3 equilibrium = target + settings.Acceleration / (omega * omega);
            Vector3 position = Integrate(state.Position - equilibrium, state.Velocity, omega, settings.DampingRatio, seconds) + equilibrium;
            Vector3 fallback = Direction(target - anchor, Vector3.UnitY);
            position = anchor + Direction(position - anchor, fallback) * length;
            for (int iteration = 0; iteration < 32; iteration++)
            {
                bool moved = false;
                foreach (CapsuleObstacle obstacle in obstacles)
                {
                    Vector3 center = Closest(position, obstacle.Start, obstacle.End);
                    float radius = obstacle.Radius + settings.Radius;
                    if (Vector3.DistanceSquared(position, center) >= radius * radius - .00000001f) continue;
                    position = Outside(anchor, length, position, center, radius, fallback);
                    moved = true;
                }
                if (moved == false) break;
            }
            foreach (CapsuleObstacle obstacle in obstacles)
                if (Vector3.Distance(position, Closest(position, obstacle.Start, obstacle.End)) + .00001f < obstacle.Radius + settings.Radius)
                    throw new ArgumentException("Length and collision constraints could not be satisfied");
            Vector3 velocity = seconds > 0 ? (position - state.Position) / seconds : state.Velocity;
            Vector3 radial = Direction(position - anchor, fallback);
            velocity -= radial * Vector3.Dot(velocity, radial);
            Check(position); Check(velocity);
            return new SpringState { Position = position, Velocity = velocity };
        }

        private static Vector3 Integrate(Vector3 offset, Vector3 velocity, float omega, float damping, float seconds)
        {
            if (Math.Abs(damping - 1) < .0001f)
                return (offset + (velocity + omega * offset) * seconds) * (float)Math.Exp(-omega * seconds);
            if (damping < 1)
            {
                float frequency = omega * (float)Math.Sqrt(1 - damping * damping);
                return (offset * (float)Math.Cos(frequency * seconds) + (velocity + damping * omega * offset) / frequency * (float)Math.Sin(frequency * seconds)) * (float)Math.Exp(-damping * omega * seconds);
            }
            float root = (float)Math.Sqrt(damping * damping - 1);
            float first = -omega * (damping - root);
            float second = -omega * (damping + root);
            Vector3 coefficient = (velocity - second * offset) / (first - second);
            return coefficient * (float)Math.Exp(first * seconds) + (offset - coefficient) * (float)Math.Exp(second * seconds);
        }

        private static Vector3 Outside(Vector3 anchor, float length, Vector3 position, Vector3 center, float radius, Vector3 fallback)
        {
            Vector3 offset = center - anchor;
            float distance = offset.Length();
            if (distance < .000001f)
            {
                if (length + .00001f < radius) throw new ArgumentException("Obstacle encloses the particle reach");
                return position;
            }
            float cosine = (length * length + distance * distance - radius * radius) / (2 * length * distance);
            if (cosine < -1) throw new ArgumentException("Obstacle encloses the particle reach");
            if (cosine >= 1) return position;
            Vector3 axis = offset / distance;
            Vector3 direction = Direction(position - anchor, fallback);
            Vector3 tangent = direction - axis * Vector3.Dot(direction, axis);
            if (tangent.LengthSquared() < .0000000001f) tangent = fallback - axis * Vector3.Dot(fallback, axis);
            if (tangent.LengthSquared() < .0000000001f) tangent = Vector3.Cross(axis, Math.Abs(axis.Y) < .9f ? Vector3.UnitY : Vector3.UnitX);
            return anchor + length * (axis * cosine + Vector3.Normalize(tangent) * (float)Math.Sqrt(Math.Max(0, 1 - cosine * cosine)));
        }

        private static Vector3 Closest(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 span = end - start;
            float squared = span.LengthSquared();
            float along = squared < .0000000001f ? 0 : Math.Max(0, Math.Min(1, Vector3.Dot(point - start, span) / squared));
            return start + span * along;
        }

        private static Vector3 Direction(Vector3 value, Vector3 fallback) { return value.LengthSquared() < .0000000001f ? fallback : Vector3.Normalize(value); }
        private static void Check(Vector3 value) { Range(value.X, -1000000, 1000000); Range(value.Y, -1000000, 1000000); Range(value.Z, -1000000, 1000000); }
        private static void Range(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) throw new ArgumentException("Invalid spring value");
        }
    }
}
