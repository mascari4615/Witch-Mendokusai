using System;

namespace Motion
{
    [Serializable]
    public sealed class ScalarTrack
    {
        public string Target;
        public ScalarKey[] Keys;
    }

    [Serializable]
    public sealed class ScalarKey
    {
        public float Time;
        public float Value;
    }

    public struct ValueSample
    {
        public string Target;
        public float Value;
    }

    public interface IValueTarget
    {
        float Read(string target);
        void Validate(string target, float value);
        void Write(string target, float value);
    }
}
