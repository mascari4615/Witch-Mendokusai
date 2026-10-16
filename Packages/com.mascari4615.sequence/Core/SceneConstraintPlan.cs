using System;
using System.Collections.Generic;
using System.Numerics;
using Motion;
using Rigging;
using SceneContent;

namespace SceneSequence
{
    public sealed class SceneConstraintPlan
    {
        private readonly string[] ids;
        private readonly int[] parents;
        private readonly int[] hierarchy;
        private readonly int[][] limbs;
        private readonly PoseValue[] working;
        private readonly Matrix4x4[] world;
        private readonly Quaternion[] rotations;
        private readonly bool[] written;
        public int Count { get { return limbs.Length; } }

        public SceneConstraintPlan(SceneDocument scene)
        {
            SceneValidation.Ensure(scene);
            int[] order = SceneConstraints.Order(scene);
            ids = new string[scene.Nodes.Length];
            parents = new int[ids.Length];
            working = new PoseValue[ids.Length];
            world = new Matrix4x4[ids.Length];
            rotations = new Quaternion[ids.Length];
            written = new bool[ids.Length];
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            List<int>[] children = new List<int>[ids.Length];
            Queue<int> ready = new Queue<int>();
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = scene.Nodes[index].Id;
                nodes.Add(ids[index], index);
                parents[index] = scene.Nodes[index].Parent;
                children[index] = new List<int>();
                if (parents[index] < 0) ready.Enqueue(index);
            }
            for (int index = 0; index < ids.Length; index++) if (parents[index] >= 0) children[parents[index]].Add(index);
            List<int> sorted = new List<int>();
            while (ready.Count > 0)
            {
                int current = ready.Dequeue();
                sorted.Add(current);
                foreach (int child in children[current]) ready.Enqueue(child);
            }
            hierarchy = sorted.ToArray();
            limbs = new int[order.Length][];
            for (int index = 0; index < order.Length; index++)
            {
                SceneLimbConstraint constraint = scene.LimbConstraints[order[index]];
                limbs[index] = new[] { nodes[constraint.Upper], nodes[constraint.Lower], nodes[constraint.End], nodes[constraint.Goal], nodes[constraint.Pole] };
                written[limbs[index][0]] = true;
                written[limbs[index][1]] = true;
            }
        }

        public LimbSolution[] Apply(IPoseTarget target)
        {
            if (Count == 0) return new LimbSolution[0];
            if (target == null) throw new ArgumentNullException("target");
            for (int index = 0; index < ids.Length; index++)
            {
                PoseValue pose = target.Read(ids[index]);
                Check(pose.Position);
                Check(pose.Scale);
                Quaternion rotation = pose.Rotation;
                if (Finite(rotation.X) == false || Finite(rotation.Y) == false || Finite(rotation.Z) == false || Finite(rotation.W) == false || Math.Abs(rotation.LengthSquared() - 1) > .001f)
                    throw new ArgumentException("Constraint poses require normalized finite rotations");
                working[index] = pose;
            }
            LimbSolution[] results = new LimbSolution[limbs.Length];
            for (int index = 0; index < limbs.Length; index++)
            {
                int[] limb = limbs[index];
                CheckRigid(limb[0]);
                CheckRigid(limb[1]);
                UpdateWorld();
                LimbPose pose = new LimbPose { Upper = Position(limb[0]), Lower = Position(limb[1]), End = Position(limb[2]),
                    UpperRotation = rotations[limb[0]], LowerRotation = rotations[limb[1]] };
                LimbSolution solution = TwoBoneIk.Solve(pose, Position(limb[3]), Position(limb[4]));
                SetRotation(limb[0], solution.UpperRotation);
                UpdateWorld();
                SetRotation(limb[1], solution.LowerRotation);
                UpdateWorld();
                float tolerance = Math.Max(.00001f, (pose.Lower - pose.Upper).Length() * .0001f + (pose.End - pose.Lower).Length() * .0001f);
                if (Vector3.Distance(Position(limb[2]), solution.End) > tolerance)
                    throw new ArgumentException("Constraint chain does not preserve rigid segment lengths");
                results[index] = solution;
            }
            foreach (int index in hierarchy) if (written[index]) target.Write(ids[index], working[index]);
            return results;
        }

        private void SetRotation(int node, Quaternion rotation)
        {
            Quaternion parent = parents[node] < 0 ? Quaternion.Identity : rotations[parents[node]];
            working[node].Rotation = Quaternion.Normalize(Quaternion.Inverse(parent) * rotation);
        }

        private void UpdateWorld()
        {
            foreach (int index in hierarchy)
            {
                PoseValue pose = working[index];
                Quaternion rotation = Quaternion.Normalize(pose.Rotation);
                Matrix4x4 local = Matrix4x4.CreateScale(pose.Scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(pose.Position);
                world[index] = parents[index] < 0 ? local : local * world[parents[index]];
                rotations[index] = parents[index] < 0 ? rotation : Quaternion.Normalize(rotations[parents[index]] * rotation);
            }
        }

        private Vector3 Position(int node) { return new Vector3(world[node].M41, world[node].M42, world[node].M43); }

        private void CheckRigid(int node)
        {
            while (node >= 0)
            {
                Vector3 scale = working[node].Scale;
                float maximum = Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z)));
                if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0 || Math.Abs(scale.X - scale.Y) > maximum * .0001f || Math.Abs(scale.X - scale.Z) > maximum * .0001f)
                    throw new ArgumentException("Persistent limb constraints require positive uniform ancestor scale");
                node = parents[node];
            }
        }

        private static void Check(Vector3 value)
        {
            if (Finite(value.X) == false || Finite(value.Y) == false || Finite(value.Z) == false) throw new ArgumentException("Constraint poses must be finite");
        }

        private static bool Finite(float value) { return float.IsNaN(value) == false && float.IsInfinity(value) == false; }
    }
}
