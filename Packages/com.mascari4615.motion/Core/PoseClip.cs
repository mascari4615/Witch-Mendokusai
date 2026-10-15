using System;

namespace Motion
{
    [Serializable]
    public sealed class PoseClip
    {
        public string Format = "pose-clip";
        public int Version = 1;
        public string Id;
        public float Duration;
        public bool Loop;
        public PoseTrack[] Tracks;
        public ScalarTrack[] Values = new ScalarTrack[0];
    }

    [Serializable]
    public sealed class PoseTrack
    {
        public string Node;
        public PoseKey[] Keys;
    }

    [Serializable]
    public sealed class PoseKey
    {
        public float Time;
        public float[] Position;
        public float[] Rotation;
        public float[] Scale;
    }
}
