using System;
using System.Collections.Generic;

namespace Motion
{
    public sealed class FloatPropertyTarget : IValueTarget
    {
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);

        public void Bind(string id, Func<float> read, Action<float> write, float minimum, float maximum)
        {
            if (string.IsNullOrEmpty(id) || read == null || write == null || Finite(minimum) == false || Finite(maximum) == false || minimum > maximum)
            {
                throw new ArgumentException("Invalid float property binding");
            }
            bindings.Add(id, new Binding { Read = read, Write = write, Minimum = minimum, Maximum = maximum });
        }

        public float Read(string target)
        {
            float value = bindings[target].Read();
            Validate(target, value);
            return value;
        }

        public void Validate(string target, float value)
        {
            Binding binding = bindings[target];
            if (Finite(value) == false || value < binding.Minimum || value > binding.Maximum)
            {
                throw new ArgumentException("Motion value outside property bounds: " + target);
            }
            if (Finite(binding.Read()) == false)
            {
                throw new ArgumentException("Motion property is unavailable or nonfinite: " + target);
            }
        }

        public void Write(string target, float value)
        {
            Validate(target, value);
            bindings[target].Write(value);
        }

        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }

        private sealed class Binding
        {
            public Func<float> Read;
            public Action<float> Write;
            public float Minimum;
            public float Maximum;
        }
    }
}
