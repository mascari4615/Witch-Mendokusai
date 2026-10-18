using System;
using System.Collections.Generic;
using System.Numerics;
using Motion;
using SceneContent;

namespace SceneSequence
{
    internal sealed class DynamicsFrame
    {
        public readonly string[] Ids;
        public readonly int[] Parents;
        public readonly PoseValue[] Local;
        public readonly Matrix4x4[] World;
        public readonly Quaternion[] Rotation;
        public readonly Dictionary<string, int> Indices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly int[] order;

        public DynamicsFrame(SceneDocument scene)
        {
            int count = scene.Nodes.Length;
            Ids = new string[count]; Parents = new int[count]; Local = new PoseValue[count]; World = new Matrix4x4[count]; Rotation = new Quaternion[count];
            List<int>[] children = new List<int>[count];
            Queue<int> ready = new Queue<int>();
            for (int index = 0; index < count; index++)
            {
                Ids[index] = scene.Nodes[index].Id; Parents[index] = scene.Nodes[index].Parent; Indices.Add(Ids[index], index); children[index] = new List<int>();
                if (Parents[index] < 0) ready.Enqueue(index);
            }
            for (int index = 0; index < count; index++) if (Parents[index] >= 0) children[Parents[index]].Add(index);
            List<int> sorted = new List<int>();
            while (ready.Count > 0) { int node = ready.Dequeue(); sorted.Add(node); foreach (int child in children[node]) ready.Enqueue(child); }
            order = sorted.ToArray();
        }

        public void Read(IPoseTarget target)
        {
            foreach (int index in order)
            {
                PoseValue value = target.Read(Ids[index]);
                if (Finite(value.Position) == false || Finite(value.Scale) == false || float.IsNaN(value.Rotation.LengthSquared()) || Math.Abs(value.Rotation.LengthSquared() - 1) > .001f)
                    throw new ArgumentException("Dynamics require finite normalized poses");
                Local[index] = value;
            }
            Rebuild();
        }

        public void Rebuild()
        {
            foreach (int index in order)
            {
                PoseValue pose = Local[index];
                Matrix4x4 local = Matrix4x4.CreateScale(pose.Scale) * Matrix4x4.CreateFromQuaternion(pose.Rotation) * Matrix4x4.CreateTranslation(pose.Position);
                World[index] = Parents[index] < 0 ? local : local * World[Parents[index]];
                Rotation[index] = Parents[index] < 0 ? pose.Rotation : Quaternion.Normalize(Rotation[Parents[index]] * pose.Rotation);
            }
        }

        public Vector3 Position(int node) { return World[node].Translation; }

        public void Rotate(int joint, Vector3 from, Vector3 to)
        {
            Vector3 a = Vector3.Normalize(from); Vector3 b = Vector3.Normalize(to);
            float dot = Math.Max(-1, Math.Min(1, Vector3.Dot(a, b)));
            Quaternion delta;
            if (dot < -.999999f) delta = Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(a, Math.Abs(a.Y) < .9f ? Vector3.UnitY : Vector3.UnitX)), (float)Math.PI);
            else delta = Quaternion.Normalize(new Quaternion(Vector3.Cross(a, b), 1 + dot));
            Quaternion parent = Parents[joint] < 0 ? Quaternion.Identity : Rotation[Parents[joint]];
            Local[joint].Rotation = Quaternion.Normalize(Quaternion.Inverse(parent) * delta * Rotation[joint]);
            Rebuild();
        }

        private static bool Finite(Vector3 value)
        {
            return float.IsNaN(value.X) == false && float.IsNaN(value.Y) == false && float.IsNaN(value.Z) == false &&
                float.IsInfinity(value.X) == false && float.IsInfinity(value.Y) == false && float.IsInfinity(value.Z) == false;
        }
    }
}
