using System.Numerics;

namespace Motion
{
    public struct PoseValue
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public PoseValue(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    public interface IPoseTarget
    {
        PoseValue Read(string nodeId);
        void Write(string nodeId, PoseValue pose);
    }

    public struct PoseSample
    {
        public string Node;
        public PoseValue Pose;

        public PoseSample(string node, PoseValue pose)
        {
            Node = node;
            Pose = pose;
        }
    }
}
