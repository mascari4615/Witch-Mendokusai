using System;
using UnityEngine;

namespace Rigging.Unity
{
    public static class TransformLimbIk
    {
        public static LimbSolution Apply(Transform upper, Transform lower, Transform end, Vector3 goal, Vector3 pole)
        {
            if (upper == null || lower == null || end == null || lower.IsChildOf(upper) == false || end.IsChildOf(lower) == false ||
                upper == lower || lower == end)
            {
                throw new ArgumentException("Limb requires three distinct joints in ancestor order");
            }
            CheckScale(upper);
            CheckScale(lower);
            LimbPose pose = new LimbPose
            {
                Upper = Position(upper.position), Lower = Position(lower.position), End = Position(end.position),
                UpperRotation = Rotation(upper.rotation), LowerRotation = Rotation(lower.rotation)
            };
            LimbSolution solution = TwoBoneIk.Solve(pose, Position(goal), Position(pole));
            Quaternion originalUpper = upper.rotation;
            Quaternion originalLower = lower.rotation;
            upper.rotation = Rotation(solution.UpperRotation);
            lower.rotation = Rotation(solution.LowerRotation);
            Vector3 expected = new Vector3(solution.End.X, solution.End.Y, -solution.End.Z);
            float tolerance = Math.Max(0.00001f, Vector3.Distance(upper.position, end.position) * 0.0001f);
            if (Vector3.Distance(end.position, expected) > tolerance)
            {
                upper.rotation = originalUpper;
                lower.rotation = originalLower;
                throw new InvalidOperationException("Limb contains intermediate transforms that do not preserve rigid segment lengths");
            }
            return solution;
        }

        private static void CheckScale(Transform target)
        {
            Vector3 scale = target.lossyScale;
            float maximum = Math.Max(Math.Abs(scale.x), Math.Max(Math.Abs(scale.y), Math.Abs(scale.z)));
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0 || maximum <= 0 ||
                Math.Abs(scale.x - scale.y) > maximum * .0001f || Math.Abs(scale.x - scale.z) > maximum * .0001f)
            {
                throw new ArgumentException("Limb rotation solver requires positive uniform world scale");
            }
        }

        private static System.Numerics.Vector3 Position(Vector3 value)
        {
            return new System.Numerics.Vector3(value.x, value.y, -value.z);
        }

        private static System.Numerics.Quaternion Rotation(Quaternion value)
        {
            return new System.Numerics.Quaternion(-value.x, -value.y, value.z, value.w);
        }

        private static Quaternion Rotation(System.Numerics.Quaternion value)
        {
            return new Quaternion(-value.X, -value.Y, value.Z, value.W);
        }
    }
}
