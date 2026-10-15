using System;
using System.Collections.Generic;
using Appearance;

namespace SceneContent
{
    [Serializable]
    public sealed class SceneParameterProfile
    {
        public ParameterProfile Profile;
        public SceneParameterBinding[] Bindings;
    }

    [Serializable]
    public sealed class SceneParameterBinding
    {
        public string Property;
        public string Kind;
        public string Node;
        public int Mesh = -1;
        public int Component;
    }

    public static class SceneParameters
    {
        public static void Ensure(SceneDocument scene)
        {
            SceneParameterProfile parameters = scene.Parameters;
            if (parameters == null) return;
            Dictionary<string, float> values = ParameterProfiles.Evaluate(parameters.Profile);
            if (parameters.Bindings == null || parameters.Bindings.Length != values.Count) throw new ArgumentException("Parameter bindings must cover every property exactly once");
            HashSet<string> properties = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> endpoints = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> nodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneNode node in scene.Nodes) nodes.Add(node.Id);
            foreach (SceneParameterBinding binding in parameters.Bindings)
            {
                if (binding == null || binding.Property == null || values.ContainsKey(binding.Property) == false || properties.Add(binding.Property) == false)
                    throw new ArgumentException("Missing or repeated parameter property binding");
                string endpoint;
                float value = values[binding.Property];
                if (binding.Kind == "morph")
                {
                    if (string.IsNullOrEmpty(binding.Node) == false || binding.Mesh < 0 || binding.Mesh >= scene.Meshes.Length || binding.Component < 0 ||
                        binding.Component >= scene.Meshes[binding.Mesh].MorphWeights.Length || value < 0 || value > 1)
                        throw new ArgumentException("Invalid parameter morph endpoint");
                    endpoint = "morph:" + binding.Mesh + ":" + binding.Component;
                }
                else
                {
                    if ((binding.Kind != "scale" && binding.Kind != "position") || binding.Mesh != -1 || binding.Component < 0 || binding.Component > 2 ||
                        binding.Node == null || nodes.Contains(binding.Node) == false || (binding.Kind == "scale" && value <= 0))
                        throw new ArgumentException("Invalid parameter transform endpoint");
                    endpoint = binding.Kind + ":" + binding.Node.Length + ":" + binding.Node + ":" + binding.Component;
                }
                if (endpoints.Add(endpoint) == false) throw new ArgumentException("Parameter properties compete for the same endpoint");
            }
        }

        public static SceneDocument WithValues(SceneDocument source, IDictionary<string, float> values)
        {
            SceneDocument result = SceneCloning.Copy(source);
            SetValues(result, values);
            return result;
        }

        public static void SetValues(SceneDocument document, IDictionary<string, float> values)
        {
            SceneValidation.Ensure(document);
            if (document.Parameters == null) throw new ArgumentException("Scene has no parameter controls");
            SceneParameterProfile candidate = Copy(document.Parameters);
            candidate.Profile = ParameterProfiles.WithValues(candidate.Profile, values);
            SceneParameterProfile previous = document.Parameters;
            document.Parameters = candidate;
            try { Ensure(document); }
            catch { document.Parameters = previous; throw; }
            Dictionary<string, float> evaluated = ParameterProfiles.Evaluate(candidate.Profile);
            Dictionary<string, SceneNode> nodes = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
            foreach (SceneNode node in document.Nodes) nodes.Add(node.Id, node);
            foreach (SceneParameterBinding binding in candidate.Bindings)
            {
                float value = evaluated[binding.Property];
                if (binding.Kind == "morph") document.Meshes[binding.Mesh].MorphWeights[binding.Component] = value;
                else
                {
                    SceneNode node = nodes[binding.Node];
                    if (binding.Kind == "scale") node.Scale[binding.Component] = value;
                    else node.Position[binding.Component] = value;
                }
            }
        }

        public static SceneParameterProfile Copy(SceneParameterProfile source)
        {
            if (source == null) return null;
            SceneParameterProfile result = new SceneParameterProfile { Profile = ParameterProfiles.Copy(source.Profile), Bindings = new SceneParameterBinding[source.Bindings.Length] };
            for (int index = 0; index < result.Bindings.Length; index++)
            {
                SceneParameterBinding binding = source.Bindings[index];
                result.Bindings[index] = new SceneParameterBinding { Property = binding.Property, Kind = binding.Kind, Node = binding.Node, Mesh = binding.Mesh, Component = binding.Component };
            }
            return result;
        }

        public static SceneParameterProfile Compose(SceneParameterProfile first, SceneParameterProfile second, string prefix, int meshOffset)
        {
            if (first == null && second == null) return null;
            SceneParameterProfile left = Copy(first);
            SceneParameterProfile right = Copy(second);
            if (right != null)
            {
                foreach (ParameterProperty property in right.Profile.Properties) property.Id = prefix + ":" + property.Id;
                foreach (ParameterControl control in right.Profile.Controls)
                {
                    control.Id = prefix + ":" + control.Id;
                    control.Label = prefix + "/" + control.Label;
                    foreach (ParameterInfluence influence in control.Influences) influence.Property = prefix + ":" + influence.Property;
                }
                foreach (SceneParameterBinding binding in right.Bindings)
                {
                    binding.Property = prefix + ":" + binding.Property;
                    if (binding.Kind == "morph") binding.Mesh += meshOffset;
                    else binding.Node = prefix + ":" + binding.Node;
                }
            }
            List<ParameterProperty> properties = new List<ParameterProperty>();
            List<ParameterControl> controls = new List<ParameterControl>();
            List<SceneParameterBinding> bindings = new List<SceneParameterBinding>();
            foreach (SceneParameterProfile part in new[] { left, right })
            {
                if (part == null) continue;
                properties.AddRange(part.Profile.Properties);
                controls.AddRange(part.Profile.Controls);
                bindings.AddRange(part.Bindings);
            }
            return new SceneParameterProfile { Profile = new ParameterProfile { Id = "composed-parameters", Properties = properties.ToArray(), Controls = controls.ToArray() }, Bindings = bindings.ToArray() };
        }
    }
}
