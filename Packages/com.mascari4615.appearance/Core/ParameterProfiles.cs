using System;
using System.Collections.Generic;

namespace Appearance
{
    public static class ParameterProfiles
    {
        public static Dictionary<string, float> Evaluate(ParameterProfile profile)
        {
            if (profile == null || profile.Format != "parameter-profile" || profile.Version != 1 || string.IsNullOrEmpty(profile.Id) || profile.Id.Length > 256 ||
                profile.Properties == null || profile.Controls == null || profile.Properties.Length > 10000 || profile.Controls.Length > 1000)
                throw new ArgumentException("Invalid parameter profile");
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (ParameterProperty property in profile.Properties)
            {
                if (property == null || string.IsNullOrEmpty(property.Id) || property.Id.Length > 256 || values.ContainsKey(property.Id) ||
                    Finite(property.Minimum) == false || Finite(property.Maximum) == false || property.Minimum > property.Maximum ||
                    InRange(property.Baseline, property.Minimum, property.Maximum) == false)
                    throw new ArgumentException("Invalid or repeated parameter property");
                values.Add(property.Id, property.Baseline);
            }
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> referenced = new HashSet<string>(StringComparer.Ordinal);
            int influences = 0;
            foreach (ParameterControl control in profile.Controls)
            {
                if (control == null || string.IsNullOrEmpty(control.Id) || control.Id.Length > 256 || ids.Add(control.Id) == false ||
                    string.IsNullOrEmpty(control.Label) || control.Label.Trim().Length == 0 || control.Label.Length > 256 ||
                    Finite(control.Minimum) == false || Finite(control.Maximum) == false || control.Minimum >= control.Maximum ||
                    InRange(control.Default, control.Minimum, control.Maximum) == false || InRange(control.Value, control.Minimum, control.Maximum) == false ||
                    control.Influences == null || control.Influences.Length == 0 || control.Influences.Length > 10000)
                    throw new ArgumentException("Invalid parameter control");
                influences += control.Influences.Length;
                if (influences > 100000) throw new ArgumentException("Too many parameter influences");
                HashSet<string> properties = new HashSet<string>(StringComparer.Ordinal);
                foreach (ParameterInfluence influence in control.Influences)
                {
                    if (influence == null || influence.Property == null || values.ContainsKey(influence.Property) == false ||
                        properties.Add(influence.Property) == false || Finite(influence.Weight) == false || influence.Weight == 0)
                        throw new ArgumentException("Invalid or repeated parameter influence");
                    values[influence.Property] += ((double)control.Value - control.Default) * influence.Weight;
                    referenced.Add(influence.Property);
                }
            }
            Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (ParameterProperty property in profile.Properties)
            {
                double value = values[property.Id];
                float sampled = (float)value;
                if (referenced.Contains(property.Id) == false || double.IsNaN(value) || double.IsInfinity(value) || InRange(sampled, property.Minimum, property.Maximum) == false)
                    throw new ArgumentException("Parameter result outside property bounds: " + property.Id);
                result.Add(property.Id, sampled);
            }
            return result;
        }

        public static ParameterProfile WithValues(ParameterProfile source, IDictionary<string, float> changes)
        {
            if (changes == null) throw new ArgumentNullException("changes");
            ParameterProfile result = Copy(source);
            foreach (KeyValuePair<string, float> change in changes)
            {
                ParameterControl control = Array.Find(result.Controls, candidate => candidate.Id == change.Key);
                if (control == null) throw new ArgumentException("Unknown parameter control: " + change.Key);
                control.Value = change.Value;
            }
            Evaluate(result);
            return result;
        }

        public static ParameterProfile Copy(ParameterProfile source)
        {
            Evaluate(source);
            ParameterProfile result = new ParameterProfile { Id = source.Id, Properties = new ParameterProperty[source.Properties.Length], Controls = new ParameterControl[source.Controls.Length] };
            for (int index = 0; index < result.Properties.Length; index++)
            {
                ParameterProperty value = source.Properties[index];
                result.Properties[index] = new ParameterProperty { Id = value.Id, Baseline = value.Baseline, Minimum = value.Minimum, Maximum = value.Maximum };
            }
            for (int index = 0; index < result.Controls.Length; index++)
            {
                ParameterControl value = source.Controls[index];
                ParameterControl control = new ParameterControl { Id = value.Id, Label = value.Label, Minimum = value.Minimum, Maximum = value.Maximum, Default = value.Default, Value = value.Value, Influences = new ParameterInfluence[value.Influences.Length] };
                for (int influence = 0; influence < control.Influences.Length; influence++)
                    control.Influences[influence] = new ParameterInfluence { Property = value.Influences[influence].Property, Weight = value.Influences[influence].Weight };
                result.Controls[index] = control;
            }
            return result;
        }

        private static bool Finite(float value) { return float.IsNaN(value) == false && float.IsInfinity(value) == false; }
        private static bool InRange(float value, float minimum, float maximum) { return Finite(value) && value >= minimum && value <= maximum; }
    }
}
