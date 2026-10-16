using System;
using System.Collections.Generic;
using Appearance;
using Motion;
using SceneContent;

namespace SceneSequence
{
    public static class SequenceParameters
    {
        public static SequenceDocument WithValues(SequenceDocument source, IDictionary<string, float> values, Func<int, int, string> morphTarget = null)
        {
            SequenceValidation.Ensure(source);
            if (source.Scene.Parameters == null) throw new ArgumentException("Sequence has no parameter controls");
            Dictionary<string, float> before = ParameterProfiles.Evaluate(source.Scene.Parameters.Profile);
            SceneDocument scene = SceneParameters.WithValues(source.Scene, values);
            Dictionary<string, float> after = ParameterProfiles.Evaluate(scene.Parameters.Profile);
            SequenceDocument result = new SequenceDocument { Id = source.Id, Scene = scene, Clips = new PoseClip[source.Clips.Length] };
            Dictionary<string, SceneNode> previousNodes = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
            Dictionary<string, SceneNode> nextNodes = new Dictionary<string, SceneNode>(StringComparer.Ordinal);
            foreach (SceneNode node in source.Scene.Nodes) previousNodes.Add(node.Id, node);
            foreach (SceneNode node in scene.Nodes) nextNodes.Add(node.Id, node);
            Dictionary<string, double[]> scale = new Dictionary<string, double[]>(StringComparer.Ordinal);
            Dictionary<string, double[]> position = new Dictionary<string, double[]>(StringComparer.Ordinal);
            Dictionary<string, double> morph = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (SceneParameterBinding binding in scene.Parameters.Bindings)
            {
                double difference = (double)after[binding.Property] - before[binding.Property];
                if (binding.Kind == "morph")
                {
                    scene.Meshes[binding.Mesh].MorphWeights[binding.Component] = Morph(source.Scene.Meshes[binding.Mesh].MorphWeights[binding.Component] + difference);
                    if (difference == 0) continue;
                    bool hasValues = Array.Exists(source.Clips, clip => clip.Values.Length > 0);
                    if (hasValues == false) continue;
                    string target = morphTarget == null ? null : morphTarget(binding.Mesh, binding.Component);
                    if (string.IsNullOrEmpty(target) || morph.ContainsKey(target)) throw new ArgumentException("Animated morph changes require unique resolved property targets");
                    morph.Add(target, difference);
                    continue;
                }
                SceneNode previous = previousNodes[binding.Node];
                SceneNode next = nextNodes[binding.Node];
                bool isScale = binding.Kind == "scale";
                Dictionary<string, double[]> adjustments = isScale ? scale : position;
                double[] adjustment;
                if (adjustments.TryGetValue(binding.Node, out adjustment) == false)
                {
                    adjustment = isScale ? new[] { 1d, 1d, 1d } : new double[3];
                    adjustments.Add(binding.Node, adjustment);
                }
                adjustment[binding.Component] = isScale ? (double)after[binding.Property] / before[binding.Property] : difference;
                if (isScale) next.Scale[binding.Component] = Finite(previous.Scale[binding.Component] * adjustment[binding.Component]);
                else next.Position[binding.Component] = Finite(previous.Position[binding.Component] + difference);
            }
            for (int index = 0; index < result.Clips.Length; index++) result.Clips[index] = Adjust(source.Clips[index], scale, position, morph);
            SequenceValidation.Ensure(result);
            return result;
        }

        private static PoseClip Adjust(PoseClip source, Dictionary<string, double[]> scale, Dictionary<string, double[]> position, Dictionary<string, double> morph)
        {
            PoseClip result = new PoseClip { Id = source.Id, Duration = source.Duration, Loop = source.Loop,
                Tracks = new PoseTrack[source.Tracks.Length], Values = new ScalarTrack[source.Values.Length] };
            for (int trackIndex = 0; trackIndex < source.Tracks.Length; trackIndex++)
            {
                PoseTrack track = source.Tracks[trackIndex];
                PoseTrack changed = new PoseTrack { Node = track.Node, Keys = new PoseKey[track.Keys.Length] };
                double[] scaleAdjustment;
                double[] positionAdjustment;
                scale.TryGetValue(track.Node, out scaleAdjustment);
                position.TryGetValue(track.Node, out positionAdjustment);
                for (int index = 0; index < track.Keys.Length; index++)
                {
                    PoseKey key = track.Keys[index];
                    PoseKey next = new PoseKey { Time = key.Time, Rotation = (float[])key.Rotation.Clone(), Position = (float[])key.Position.Clone(), Scale = (float[])key.Scale.Clone() };
                    for (int component = 0; component < 3; component++)
                    {
                        if (scaleAdjustment != null) next.Scale[component] = Finite(key.Scale[component] * scaleAdjustment[component]);
                        if (positionAdjustment != null) next.Position[component] = Finite(key.Position[component] + positionAdjustment[component]);
                    }
                    changed.Keys[index] = next;
                }
                result.Tracks[trackIndex] = changed;
            }
            for (int trackIndex = 0; trackIndex < source.Values.Length; trackIndex++)
            {
                ScalarTrack track = source.Values[trackIndex];
                ScalarTrack changed = new ScalarTrack { Target = track.Target, Keys = new ScalarKey[track.Keys.Length] };
                double difference;
                bool adjusted = morph.TryGetValue(track.Target, out difference);
                for (int index = 0; index < track.Keys.Length; index++)
                {
                    ScalarKey key = track.Keys[index];
                    changed.Keys[index] = new ScalarKey { Time = key.Time, Value = adjusted ? Morph(key.Value + difference) : key.Value };
                }
                result.Values[trackIndex] = changed;
            }
            return result;
        }

        private static float Finite(double value)
        {
            float result = (float)value;
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new ArgumentException("Adjusted motion must be finite");
            return result;
        }

        private static float Morph(double value)
        {
            if (value < 0 || value > 1) throw new ArgumentException("Adjusted morph would leave its supported range");
            return Finite(value);
        }
    }
}
