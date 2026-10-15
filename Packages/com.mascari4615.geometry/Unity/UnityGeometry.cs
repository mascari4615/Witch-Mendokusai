using Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace Geometry.Unity
{
    public static class UnityGeometry
    {
        public static Mesh Create(MeshGeometry geometry)
        {
            geometry.Validate();
            Mesh mesh = new Mesh { name = "Native geometry", indexFormat = geometry.Positions.Length / 3 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = Vectors(geometry.Positions);
            if (geometry.Normals.Length > 0) mesh.normals = Vectors(geometry.Normals);
            Vector2[] uv = new Vector2[geometry.Uv.Length / 2];
            for (int index = 0; index < uv.Length; index++) uv[index] = new Vector2(geometry.Uv[index * 2], geometry.Uv[index * 2 + 1]);
            mesh.uv = uv;
            if (geometry.Tangents.Length > 0)
            {
                Vector4[] tangents = new Vector4[geometry.Tangents.Length / 4];
                for (int index = 0; index < tangents.Length; index++) tangents[index] = new Vector4(geometry.Tangents[index * 4], geometry.Tangents[index * 4 + 1], -geometry.Tangents[index * 4 + 2], -geometry.Tangents[index * 4 + 3]);
                mesh.tangents = tangents;
            }
            if (geometry.Colors.Length > 0)
            {
                Color[] colors = new Color[geometry.Colors.Length / 4];
                for (int index = 0; index < colors.Length; index++) colors[index] = new Color(geometry.Colors[index * 4], geometry.Colors[index * 4 + 1], geometry.Colors[index * 4 + 2], geometry.Colors[index * 4 + 3]);
                mesh.colors = colors;
            }
            BoneWeight[] weights = new BoneWeight[geometry.BoneWeights.Length / 4];
            for (int index = 0; index < weights.Length; index++)
            {
                int slot = index * 4;
                weights[index] = new BoneWeight
                {
                    boneIndex0 = geometry.BoneIndices[slot], boneIndex1 = geometry.BoneIndices[slot + 1],
                    boneIndex2 = geometry.BoneIndices[slot + 2], boneIndex3 = geometry.BoneIndices[slot + 3],
                    weight0 = geometry.BoneWeights[slot], weight1 = geometry.BoneWeights[slot + 1],
                    weight2 = geometry.BoneWeights[slot + 2], weight3 = geometry.BoneWeights[slot + 3]
                };
            }
            if (weights.Length > 0) mesh.boneWeights = weights;
            Matrix4x4[] poses = new Matrix4x4[geometry.BindPoses.Length / 16];
            for (int index = 0; index < poses.Length; index++)
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 4; row++)
                        poses[index][row, column] = geometry.BindPoses[index * 16 + column * 4 + row] * (row == 2 ? -1 : 1) * (column == 2 ? -1 : 1);
            mesh.bindposes = poses;
            foreach (MorphTarget morph in geometry.Morphs)
            {
                foreach (MorphFrame frame in morph.Frames)
                {
                    Vector3[] positions = Expand(frame.Indices, frame.Positions, mesh.vertexCount);
                    Vector3[] normals = frame.Normals.Length == 0 ? null : Expand(frame.Indices, frame.Normals, mesh.vertexCount);
                    Vector3[] tangents = frame.Tangents.Length == 0 ? null : Expand(frame.Indices, frame.Tangents, mesh.vertexCount);
                    mesh.AddBlendShapeFrame(morph.Id, frame.Weight * 100, positions, normals, tangents);
                }
            }
            mesh.subMeshCount = geometry.Submeshes.Length;
            for (int submesh = 0; submesh < geometry.Submeshes.Length; submesh++)
            {
                int[] triangles = (int[])geometry.Submeshes[submesh].Clone();
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    int saved = triangles[index + 1];
                    triangles[index + 1] = triangles[index + 2];
                    triangles[index + 2] = saved;
                }
                mesh.SetTriangles(triangles, submesh, false);
            }
            if (geometry.Normals.Length == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] Expand(int[] indices, float[] values, int count)
        {
            Vector3[] vectors = new Vector3[count];
            for (int index = 0; index < indices.Length; index++) vectors[indices[index]] = new Vector3(values[index * 3], values[index * 3 + 1], -values[index * 3 + 2]);
            return vectors;
        }

        private static Vector3[] Vectors(float[] values)
        {
            Vector3[] vectors = new Vector3[values.Length / 3];
            for (int index = 0; index < vectors.Length; index++) vectors[index] = new Vector3(values[index * 3], values[index * 3 + 1], -values[index * 3 + 2]);
            return vectors;
        }
    }
}
