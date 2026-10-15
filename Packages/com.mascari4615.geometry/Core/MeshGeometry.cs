using System;
using System.Collections.Generic;

namespace Geometry
{
    public sealed class MeshGeometry
    {
        public float[] Positions;
        public float[] Normals;
        public float[] Uv;
        public float[] Tangents = new float[0];
        public float[] Colors = new float[0];
        public int[] BoneIndices;
        public float[] BoneWeights;
        public float[] BindPoses;
        public int[][] Submeshes;
        public MorphTarget[] Morphs = new MorphTarget[0];

        public void Validate()
        {
            if (Positions == null || Positions.Length == 0 || Positions.Length % 3 != 0 || Positions.Length > 6000000)
                throw new ArgumentException("Invalid vertex positions");
            int vertices = Positions.Length / 3;
            CheckFloats(Positions);
            CheckAttribute(Normals, vertices * 3);
            CheckAttribute(Uv, vertices * 2);
            CheckAttribute(Tangents, vertices * 4);
            CheckAttribute(Colors, vertices * 4);
            if (BindPoses == null || BindPoses.Length % 16 != 0 || BindPoses.Length > 262144)
                throw new ArgumentException("Invalid bind poses");
            CheckFloats(BindPoses);
            if (BoneIndices == null || BoneWeights == null || BoneIndices.Length != BoneWeights.Length ||
                (BoneIndices.Length != 0 && BoneIndices.Length != vertices * 4))
                throw new ArgumentException("Invalid skin influences");
            CheckFloats(BoneWeights);
            for (int index = 0; index < BoneWeights.Length; index += 4)
            {
                float sum = 0;
                for (int influence = 0; influence < 4; influence++)
                {
                    int slot = index + influence;
                    if (BoneWeights[slot] < 0 || BoneWeights[slot] > 1 ||
                        (BoneWeights[slot] > 0 && (BoneIndices[slot] < 0 || BoneIndices[slot] >= BindPoses.Length / 16)))
                        throw new ArgumentException("Invalid bone index or weight");
                    sum += BoneWeights[slot];
                }
                if (Math.Abs(sum - 1) > 0.002f) throw new ArgumentException("Skin weights must sum to one");
            }
            if (Submeshes == null || Submeshes.Length == 0 || Submeshes.Length > 128)
                throw new ArgumentException("Invalid submesh count");
            long total = 0;
            foreach (int[] indices in Submeshes)
            {
                if (indices == null || indices.Length % 3 != 0) throw new ArgumentException("Invalid triangles");
                total += indices.Length;
                if (total > 12000000) throw new ArgumentException("Too many indices");
                foreach (int vertex in indices)
                    if (vertex < 0 || vertex >= vertices) throw new ArgumentException("Triangle index out of range");
            }
            if (Morphs == null || Morphs.Length > 4096) throw new ArgumentException("Invalid morph target count");
            HashSet<string> morphIds = new HashSet<string>(StringComparer.Ordinal);
            long deltas = 0;
            foreach (MorphTarget morph in Morphs)
            {
                if (morph == null || string.IsNullOrEmpty(morph.Id) || morph.Id.Length > 128 ||
                    morphIds.Add(morph.Id) == false || morph.Frames == null || morph.Frames.Length == 0 || morph.Frames.Length > 64)
                    throw new ArgumentException("Invalid morph target");
                float previous = float.NegativeInfinity;
                foreach (MorphFrame frame in morph.Frames)
                {
                    if (frame == null || float.IsNaN(frame.Weight) || float.IsInfinity(frame.Weight) || frame.Weight <= previous || frame.Indices == null ||
                        frame.Positions == null || frame.Positions.Length != frame.Indices.Length * 3)
                        throw new ArgumentException("Invalid morph frame");
                    previous = frame.Weight;
                    CheckFloats(frame.Positions);
                    CheckAttribute(frame.Normals, frame.Positions.Length);
                    CheckAttribute(frame.Tangents, frame.Positions.Length);
                    int last = -1;
                    foreach (int vertex in frame.Indices)
                    {
                        if (vertex <= last || vertex >= vertices) throw new ArgumentException("Invalid sparse morph index");
                        last = vertex;
                    }
                    deltas += frame.Indices.Length;
                    if (deltas > 16000000) throw new ArgumentException("Too many morph deltas");
                }
            }
        }

        private static void CheckAttribute(float[] values, int expected)
        {
            if (values == null || (values.Length != 0 && values.Length != expected))
                throw new ArgumentException("Attribute count differs from vertex count");
            CheckFloats(values);
        }

        private static void CheckFloats(float[] values)
        {
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Non-finite geometry value");
        }
    }

    public sealed class MorphTarget
    {
        public string Id;
        public MorphFrame[] Frames;
    }

    public sealed class MorphFrame
    {
        public float Weight;
        public int[] Indices;
        public float[] Positions;
        public float[] Normals;
        public float[] Tangents;
    }
}
