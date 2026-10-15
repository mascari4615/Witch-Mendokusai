using System;

namespace SceneContent
{
    [Serializable]
    public sealed class SceneCamera
    {
        public int Node;
        public string Projection = "perspective";
        public float FieldOfView = 45;
        public float OrthographicSize = 5;
        public float Near = .01f;
        public float Far = 1000;
        public float[] ClearColor = new[] { .12f, .16f, .21f, 1f };
        public bool Enabled = true;
    }

    [Serializable]
    public sealed class SceneLight
    {
        public int Node;
        public string Kind = "directional";
        public float[] Color = new[] { 1f, 1f, 1f };
        public float Intensity = 1;
        public string IntensityUnit = "relative";
        public float Range = 10;
        public float SpotAngle = 45;
        public bool Shadows;
        public bool Enabled = true;
    }
}
