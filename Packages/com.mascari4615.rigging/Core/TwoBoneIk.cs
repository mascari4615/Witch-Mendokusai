using System;
using System.Numerics;

namespace Rigging
{
    public struct LimbPose
    {
        public Vector3 Upper;
        public Vector3 Lower;
        public Vector3 End;
        public Quaternion UpperRotation;
        public Quaternion LowerRotation;
    }

    public struct LimbSolution
    {
        public Vector3 Elbow;
        public Vector3 End;
        public Quaternion UpperRotation;
        public Quaternion LowerRotation;
        public bool Reachable;
    }

    public static class TwoBoneIk
    {
        private const float EPSILON = 0.000001f;

        public static LimbSolution Solve(LimbPose pose, Vector3 goal, Vector3 pole)
        {
            Check(pose.Upper);
            Check(pose.Lower);
            Check(pose.End);
            Check(goal);
            Check(pole);
            Check(pose.UpperRotation);
            Check(pose.LowerRotation);
            Vector3 upperDirection = pose.Lower - pose.Upper;
            Vector3 lowerDirection = pose.End - pose.Lower;
            float upperLength = upperDirection.Length();
            float lowerLength = lowerDirection.Length();
            if (upperLength < EPSILON || lowerLength < EPSILON || upperLength + lowerLength > 1000000)
            {
                throw new ArgumentException("Limb segments must have finite nonzero lengths");
            }
            Vector3 offset = goal - pose.Upper;
            float requested = offset.Length();
            if (float.IsInfinity(requested))
            {
                throw new ArgumentException("Goal distance exceeds numeric range");
            }
            Vector3 direction = requested > EPSILON ? offset / requested : Vector3.Normalize(upperDirection);
            float minimum = Math.Abs(upperLength - lowerLength);
            float maximum = upperLength + lowerLength;
            float distance = Math.Max(Math.Max(minimum, EPSILON), Math.Min(maximum, requested));
            Vector3 side = pole - pose.Upper;
            side -= direction * Vector3.Dot(side, direction);
            if (side.LengthSquared() < EPSILON * EPSILON)
            {
                side = upperDirection - direction * Vector3.Dot(upperDirection, direction);
            }
            if (side.LengthSquared() < EPSILON * EPSILON)
            {
                side = Vector3.Cross(direction, Math.Abs(direction.Y) < .9f ? Vector3.UnitY : Vector3.UnitX);
            }
            side = Vector3.Normalize(side);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2 * distance);
            along = Math.Max(-upperLength, Math.Min(upperLength, along));
            float away = (float)Math.Sqrt(Math.Max(0, upperLength * upperLength - along * along));
            Vector3 elbow = pose.Upper + direction * along + side * away;
            Vector3 end = pose.Upper + direction * distance;
            Quaternion upperDelta = FromTo(upperDirection, elbow - pose.Upper);
            Quaternion lowerDelta = FromTo(lowerDirection, end - elbow);
            return new LimbSolution
            {
                Elbow = elbow, End = end,
                UpperRotation = Quaternion.Normalize(upperDelta * pose.UpperRotation),
                LowerRotation = Quaternion.Normalize(lowerDelta * pose.LowerRotation),
                Reachable = requested >= minimum && requested <= maximum
            };
        }

        private static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);
            float dot = Math.Max(-1, Math.Min(1, Vector3.Dot(from, to)));
            if (dot < -.999999f)
            {
                Vector3 axis = Vector3.Normalize(Vector3.Cross(from, Math.Abs(from.Y) < .9f ? Vector3.UnitY : Vector3.UnitX));
                return Quaternion.CreateFromAxisAngle(axis, (float)Math.PI);
            }
            return Quaternion.Normalize(new Quaternion(Vector3.Cross(from, to), 1 + dot));
        }

        private static void Check(Vector3 value)
        {
            if (Finite(value.X) == false || Finite(value.Y) == false || Finite(value.Z) == false ||
                Math.Abs(value.X) > 1000000 || Math.Abs(value.Y) > 1000000 || Math.Abs(value.Z) > 1000000)
            {
                throw new ArgumentException("Limb coordinates must be finite and within one million units");
            }
        }

        private static void Check(Quaternion value)
        {
            if (Finite(value.X) == false || Finite(value.Y) == false || Finite(value.Z) == false || Finite(value.W) == false || Math.Abs(value.LengthSquared() - 1) > .001f)
            {
                throw new ArgumentException("Limb rotations must be normalized");
            }
        }

        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }
    }
}
