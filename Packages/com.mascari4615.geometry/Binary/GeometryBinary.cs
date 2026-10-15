using System;
using System.IO;
using System.Text;

namespace Geometry.Serialization
{
    public static class GeometryBinary
    {
        private const int MAGIC = 0x324d474e;
        private const int MAX_BYTES = 268435456;

        public static byte[] Write(MeshGeometry mesh)
        {
            mesh.Validate();
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(MAGIC);
                WriteFloats(writer, mesh.Positions);
                WriteFloats(writer, mesh.Normals);
                WriteFloats(writer, mesh.Uv);
                WriteFloats(writer, mesh.Tangents);
                WriteFloats(writer, mesh.Colors);
                writer.Write(mesh.BoneIndices.Length);
                foreach (int index in mesh.BoneIndices) writer.Write(index);
                WriteFloats(writer, mesh.BoneWeights);
                WriteFloats(writer, mesh.BindPoses);
                writer.Write(mesh.Submeshes.Length);
                foreach (int[] indices in mesh.Submeshes)
                {
                    writer.Write(indices.Length);
                    foreach (int index in indices) writer.Write(index);
                }
                writer.Write(mesh.Morphs.Length);
                foreach (MorphTarget morph in mesh.Morphs)
                {
                    byte[] id = Encoding.UTF8.GetBytes(morph.Id);
                    writer.Write(id.Length);
                    writer.Write(id);
                    writer.Write(morph.Frames.Length);
                    foreach (MorphFrame frame in morph.Frames)
                    {
                        writer.Write(frame.Weight);
                        writer.Write(frame.Indices.Length);
                        foreach (int vertex in frame.Indices) writer.Write(vertex);
                        WriteFloats(writer, frame.Positions);
                        WriteFloats(writer, frame.Normals);
                        WriteFloats(writer, frame.Tangents);
                    }
                }
                writer.Flush();
                if (stream.Length > MAX_BYTES) throw new ArgumentException("Geometry exceeds resource limit");
                return stream.ToArray();
            }
        }

        public static MeshGeometry Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MAX_BYTES) throw new ArgumentException("Invalid resource size");
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != MAGIC) throw new IOException("Unsupported geometry format");
                MeshGeometry mesh = new MeshGeometry();
                mesh.Positions = ReadFloats(reader, 6000000);
                mesh.Normals = ReadFloats(reader, mesh.Positions.Length);
                mesh.Uv = ReadFloats(reader, mesh.Positions.Length / 3 * 2);
                mesh.Tangents = ReadFloats(reader, mesh.Positions.Length / 3 * 4);
                mesh.Colors = ReadFloats(reader, mesh.Positions.Length / 3 * 4);
                mesh.BoneIndices = ReadInts(reader, mesh.Positions.Length / 3 * 4);
                mesh.BoneWeights = ReadFloats(reader, mesh.BoneIndices.Length);
                mesh.BindPoses = ReadFloats(reader, 262144);
                int count = ReadCount(reader, 128, 4);
                mesh.Submeshes = new int[count][];
                int remaining = 12000000;
                for (int index = 0; index < count; index++)
                {
                    mesh.Submeshes[index] = ReadInts(reader, remaining);
                    remaining -= mesh.Submeshes[index].Length;
                }
                mesh.Morphs = new MorphTarget[ReadCount(reader, 4096, 8)];
                int deltasRemaining = 16000000;
                for (int index = 0; index < mesh.Morphs.Length; index++)
                {
                    int length = ReadCount(reader, 512, 1);
                    string id = new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
                    MorphFrame[] frames = new MorphFrame[ReadCount(reader, 64, 8)];
                    for (int frame = 0; frame < frames.Length; frame++)
                    {
                        float weight = reader.ReadSingle();
                        int[] indices = ReadInts(reader, Math.Min(mesh.Positions.Length / 3, deltasRemaining));
                        deltasRemaining -= indices.Length;
                        frames[frame] = new MorphFrame
                        {
                            Weight = weight, Indices = indices, Positions = ReadFloats(reader, indices.Length * 3),
                            Normals = ReadFloats(reader, indices.Length * 3), Tangents = ReadFloats(reader, indices.Length * 3)
                        };
                    }
                    mesh.Morphs[index] = new MorphTarget { Id = id, Frames = frames };
                }
                if (stream.Position != stream.Length) throw new IOException("Trailing geometry data");
                mesh.Validate();
                return mesh;
            }
        }

        private static int ReadCount(BinaryReader reader, int maximum, int width)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum || (long)count * width > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new IOException("Invalid geometry buffer length");
            return count;
        }

        private static float[] ReadFloats(BinaryReader reader, int maximum)
        {
            float[] result = new float[ReadCount(reader, maximum, 4)];
            for (int index = 0; index < result.Length; index++) result[index] = reader.ReadSingle();
            return result;
        }

        private static int[] ReadInts(BinaryReader reader, int maximum)
        {
            int[] result = new int[ReadCount(reader, maximum, 4)];
            for (int index = 0; index < result.Length; index++) result[index] = reader.ReadInt32();
            return result;
        }

        private static void WriteFloats(BinaryWriter writer, float[] values)
        {
            writer.Write(values.Length);
            foreach (float value in values) writer.Write(value);
        }
    }
}
