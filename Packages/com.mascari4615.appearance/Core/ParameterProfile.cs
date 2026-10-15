using System;

namespace Appearance
{
    [Serializable]
    public sealed class ParameterProfile
    {
        public string Format = "parameter-profile";
        public int Version = 1;
        public string Id;
        public ParameterProperty[] Properties;
        public ParameterControl[] Controls;
    }

    [Serializable]
    public sealed class ParameterProperty
    {
        public string Id;
        public float Baseline;
        public float Minimum;
        public float Maximum;
    }

    [Serializable]
    public sealed class ParameterControl
    {
        public string Id;
        public string Label;
        public float Minimum;
        public float Maximum;
        public float Default;
        public float Value;
        public ParameterInfluence[] Influences;
    }

    [Serializable]
    public sealed class ParameterInfluence
    {
        public string Property;
        public float Weight;
    }
}
