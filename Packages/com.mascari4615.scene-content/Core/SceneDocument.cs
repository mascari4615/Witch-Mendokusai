using System;

namespace SceneContent
{
    [Serializable]
    public sealed class SceneDocument
    {
        public string Format = "scene-content";
        public int Version = 1;
        public string Id;
        public string CoordinateFrame = "right-handed-y-up-z-back";
        public SceneNode[] Nodes;
        public MeshInstance[] Meshes;
        public SurfaceMaterial[] Materials;
        public ContentResource[] Resources;
    }

    [Serializable]
    public sealed class SceneNode
    {
        public string Id;
        public int Parent = -1;
        public float[] Position;
        public float[] Rotation;
        public float[] Scale;
    }

    [Serializable]
    public sealed class MeshInstance
    {
        public int Node;
        public string Geometry;
        public int[] Bones;
        public int[] Materials;
        public bool ShadowOnly;
        public int RootBone = -1;
        public float[] MorphWeights = new float[0];
    }

    [Serializable]
    public sealed class SurfaceMaterial
    {
        public float[] BaseColor;
        public string BaseColorTexture;
        public string AlphaMode = "opaque";
        public bool DoubleSided;
        public float[] TextureScale = new[] { 1f, 1f };
        public float[] TextureOffset = new[] { 0f, 0f };
    }

    [Serializable]
    public sealed class ContentResource
    {
        public string Id;
        public string Path;
        public string MediaType;
        public string Sha256;
    }

    public interface IContentSource
    {
        byte[] Read(ContentResource resource);
    }
}
