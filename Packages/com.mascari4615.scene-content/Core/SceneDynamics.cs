using System;
using System.Collections.Generic;

namespace SceneContent
{
    [Serializable]
    public sealed class SceneSpring
    {
        public string Id;
        public string Joint;
        public string Tip;
        public float FrequencyHz = 2;
        public float DampingRatio = .5f;
        public float Inertia;
        public float Radius = .01f;
        public float[] Acceleration = new[] { 0f, -9.81f, 0f };
        public string[] Colliders = new string[0];
        public bool Enabled = true;
    }

    [Serializable]
    public sealed class SceneCollider
    {
        public string Id;
        public string Node;
        public float[] Start = new float[3];
        public float[] End = new float[3];
        public float Radius = .1f;
    }

    public static class SceneDynamics
    {
        public static int[] Order(SceneDocument scene)
        {
            if (scene.Springs == null || scene.Springs.Length > 256 || scene.Colliders == null || scene.Colliders.Length > 256)
                throw new ArgumentException("Invalid dynamics collections");
            if (scene.Springs.Length == 0 && scene.Colliders.Length == 0) return new int[0];
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++) nodes.Add(scene.Nodes[index].Id, index);
            int[][] ancestry = SceneConstraints.Intervals(scene);
            HashSet<string> colliderIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneCollider collider in scene.Colliders)
            {
                if (collider == null) throw new ArgumentException("Missing collider");
                Identity(collider.Id, colliderIds);
                if (collider.Node == null || nodes.ContainsKey(collider.Node) == false) throw new ArgumentException("Collider node missing");
                Vector(collider.Start); Vector(collider.End); Range(collider.Radius, 0, 10000);
            }
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> writers = new HashSet<string>(StringComparer.Ordinal);
            List<int> order = new List<int>();
            for (int index = 0; index < scene.Springs.Length; index++)
            {
                SceneSpring spring = scene.Springs[index];
                if (spring == null) throw new ArgumentException("Missing spring");
                Identity(spring.Id, ids);
                if (spring.Joint == null || spring.Tip == null || nodes.ContainsKey(spring.Joint) == false || nodes.ContainsKey(spring.Tip) == false ||
                    scene.Nodes[nodes[spring.Tip]].Parent != nodes[spring.Joint]) throw new ArgumentException("Spring tip must be a direct child of its joint");
                Range(spring.FrequencyHz, .1f, 30); Range(spring.DampingRatio, 0, 4); Range(spring.Inertia, 0, 1); Range(spring.Radius, 0, 100); Vector(spring.Acceleration);
                if (spring.Colliders == null || spring.Colliders.Length > 256) throw new ArgumentException("Invalid spring colliders");
                HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in spring.Colliders)
                    if (id == null || colliderIds.Contains(id) == false || selected.Add(id) == false) throw new ArgumentException("Spring collider reference missing or repeated");
                if (spring.Enabled == false) continue;
                EnsureTipSpan(scene.Nodes[nodes[spring.Tip]].Position, scene.Nodes[nodes[spring.Tip]].Position);
                if (writers.Add(spring.Joint) == false) throw new ArgumentException("Springs compete for joint rotations");
                foreach (SceneLimbConstraint limb in scene.LimbConstraints)
                {
                    if (limb.Enabled == false) continue;
                    foreach (string node in new[] { limb.Upper, limb.Lower, limb.End, limb.Goal, limb.Pole })
                        if (SceneConstraints.Ancestor(ancestry, nodes[spring.Joint], nodes[node])) throw new ArgumentException("Spring output cannot feed an earlier limb constraint");
                }
                order.Add(index);
            }
            foreach (int index in order)
            {
                SceneSpring spring = scene.Springs[index];
                foreach (string id in spring.Colliders)
                {
                    SceneCollider collider = Array.Find(scene.Colliders, value => value.Id == id);
                    foreach (int writer in order)
                        if (SceneConstraints.Ancestor(ancestry, nodes[scene.Springs[writer].Joint], nodes[collider.Node]))
                            throw new ArgumentException("Spring colliders must be kinematic inputs");
                }
            }
            order.Sort((left, right) => ancestry[0][nodes[scene.Springs[left].Joint]].CompareTo(ancestry[0][nodes[scene.Springs[right].Joint]]));
            return order.ToArray();
        }

        public static void AddRigidNodes(SceneDocument scene, HashSet<string> result)
        {
            if (scene.Springs.Length == 0 && scene.Colliders.Length == 0) return;
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++) nodes.Add(scene.Nodes[index].Id, index);
            foreach (SceneSpring spring in scene.Springs) if (spring.Enabled) AddAncestors(scene, nodes[spring.Joint], result);
            foreach (SceneCollider collider in scene.Colliders) AddAncestors(scene, nodes[collider.Node], result);
        }

        public static void EnsureTipSpan(float[] first, float[] last)
        {
            Vector(first); Vector(last);
            float squared = 0; float dot = 0;
            for (int index = 0; index < 3; index++) { float delta = last[index] - first[index]; squared += delta * delta; dot += first[index] * delta; }
            float time = squared == 0 ? 0 : Math.Max(0, Math.Min(1, -dot / squared));
            float minimum = 0;
            for (int index = 0; index < 3; index++) { float component = first[index] + time * (last[index] - first[index]); minimum += component * component; }
            if (minimum < .0000000001f) throw new ArgumentException("Spring tips must remain separated from their joint");
        }

        private static void AddAncestors(SceneDocument scene, int node, HashSet<string> result)
        {
            while (node >= 0) { if (result.Add(scene.Nodes[node].Id) == false) break; node = scene.Nodes[node].Parent; }
        }

        public static SceneSpring[] Copy(SceneSpring[] source, string prefix = "")
        {
            SceneSpring[] result = new SceneSpring[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                SceneSpring value = source[index];
                string[] colliders = new string[value.Colliders.Length];
                for (int collider = 0; collider < colliders.Length; collider++) colliders[collider] = prefix + value.Colliders[collider];
                result[index] = new SceneSpring { Id = prefix + value.Id, Joint = prefix + value.Joint, Tip = prefix + value.Tip,
                    FrequencyHz = value.FrequencyHz, DampingRatio = value.DampingRatio, Inertia = value.Inertia, Radius = value.Radius,
                    Acceleration = (float[])value.Acceleration.Clone(), Colliders = colliders, Enabled = value.Enabled };
            }
            return result;
        }

        public static SceneCollider[] Copy(SceneCollider[] source, string prefix = "")
        {
            SceneCollider[] result = new SceneCollider[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                SceneCollider value = source[index];
                result[index] = new SceneCollider { Id = prefix + value.Id, Node = prefix + value.Node, Start = (float[])value.Start.Clone(), End = (float[])value.End.Clone(), Radius = value.Radius };
            }
            return result;
        }

        private static void Identity(string id, HashSet<string> ids) { if (string.IsNullOrEmpty(id) || id.Length > 1024 || ids.Add(id) == false) throw new ArgumentException("Dynamics require unique stable identities"); }
        private static void Vector(float[] value) { if (value == null || value.Length != 3) throw new ArgumentException("Dynamics require three coordinates"); foreach (float component in value) Range(component, -1000000, 1000000); }
        private static void Range(float value, float minimum, float maximum) { if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) throw new ArgumentException("Invalid dynamics value"); }
    }
}
