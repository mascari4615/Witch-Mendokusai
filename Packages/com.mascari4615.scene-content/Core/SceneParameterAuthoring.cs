using System;
using System.Collections.Generic;
using Appearance;

namespace SceneContent
{
    public static class SceneParameterAuthoring
    {
        public static void AddScaleControl(SceneDocument document, string nodeId, string controlId, string label, int axis, float minimum, float maximum)
        {
            SceneValidation.Ensure(document);
            SceneNode node = Array.Find(document.Nodes, candidate => candidate.Id == nodeId);
            if (node == null || axis < -1 || axis > 2 || string.IsNullOrEmpty(controlId)) throw new ArgumentException("Select a scene node and scale axis");
            SceneParameterProfile parameters = SceneParameters.Copy(document.Parameters) ?? new SceneParameterProfile
            {
                Profile = new ParameterProfile { Id = "parameters-" + document.Id, Controls = new ParameterControl[0], Properties = new ParameterProperty[0] },
                Bindings = new SceneParameterBinding[0]
            };
            List<ParameterProperty> properties = new List<ParameterProperty>(parameters.Profile.Properties);
            List<ParameterControl> controls = new List<ParameterControl>(parameters.Profile.Controls);
            List<SceneParameterBinding> bindings = new List<SceneParameterBinding>(parameters.Bindings);
            List<ParameterInfluence> influences = new List<ParameterInfluence>();
            for (int component = 0; component < 3; component++)
            {
                if (axis >= 0 && axis != component) continue;
                if (bindings.Exists(binding => binding.Kind == "scale" && binding.Node == nodeId && binding.Component == component))
                    throw new ArgumentException("This scale component already has a parameter control");
                float baseline = node.Scale[component];
                if (baseline <= 0 || float.IsNaN(minimum) || float.IsInfinity(minimum) || minimum <= 0 ||
                    float.IsNaN(maximum) || float.IsInfinity(maximum) || minimum >= maximum || minimum > 1 || maximum < 1)
                    throw new ArgumentException("Scale controls require positive scale and a finite range containing 1");
                string property = controlId + ":" + component;
                properties.Add(new ParameterProperty { Id = property, Baseline = baseline, Minimum = baseline * minimum, Maximum = baseline * maximum });
                influences.Add(new ParameterInfluence { Property = property, Weight = baseline });
                bindings.Add(new SceneParameterBinding { Property = property, Kind = "scale", Node = nodeId, Component = component });
            }
            controls.Add(new ParameterControl { Id = controlId, Label = label, Minimum = minimum, Maximum = maximum, Default = 1, Value = 1, Influences = influences.ToArray() });
            parameters.Profile.Properties = properties.ToArray();
            parameters.Profile.Controls = controls.ToArray();
            parameters.Bindings = bindings.ToArray();
            SceneParameterProfile previous = document.Parameters;
            document.Parameters = parameters;
            try { SceneParameters.Ensure(document); }
            catch { document.Parameters = previous; throw; }
        }
    }
}
