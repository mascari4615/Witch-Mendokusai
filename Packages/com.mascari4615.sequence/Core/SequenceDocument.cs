using System;
using System.Collections.Generic;
using Motion;
using SceneContent;

namespace SceneSequence
{
    public sealed class SequenceDocument
    {
        public string Format = "scene-sequence";
        public int Version = 1;
        public string Id;
        public SceneDocument Scene;
        public PoseClip[] Clips = new PoseClip[0];
    }

    public static class SequenceValidation
    {
        public static void Ensure(SequenceDocument document)
        {
            if (document == null || document.Format != "scene-sequence" || document.Version != 1 ||
                string.IsNullOrEmpty(document.Id) || document.Id.Length > 1024 || document.Clips == null || document.Clips.Length > 256)
            {
                throw new ArgumentException("Invalid scene sequence header");
            }
            SceneValidation.Ensure(document.Scene);
            HashSet<string> nodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneNode node in document.Scene.Nodes)
            {
                nodes.Add(node.Id);
            }
            HashSet<string> poses = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> rigid = SceneConstraints.RigidNodes(document.Scene);
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoseClip clip in document.Clips)
            {
                new PoseClipSampler(clip);
                foreach (PoseTrack track in clip.Tracks)
                {
                    if (nodes.Contains(track.Node) == false || poses.Add(track.Node) == false)
                    {
                        throw new ArgumentException("Sequence pose targets must exist and cannot overlap between clips");
                    }
                    if (rigid.Contains(track.Node)) foreach (PoseKey key in track.Keys) SceneConstraints.EnsureScale(key.Scale);
                }
                foreach (ScalarTrack track in clip.Values)
                {
                    if (values.Add(track.Target) == false)
                    {
                        throw new ArgumentException("Sequence property targets cannot overlap between clips");
                    }
                }
            }
        }
    }
}
