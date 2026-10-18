using System;
using System.Collections.Generic;

namespace SceneContent
{
    [Serializable]
    public sealed class SceneLimbConstraint
    {
        public string Id;
        public string Upper;
        public string Lower;
        public string End;
        public string Goal;
        public string Pole;
        public bool Enabled = true;
    }

    public static class SceneConstraints
    {
        public static int[] Order(SceneDocument scene)
        {
            if (scene.LimbConstraints == null || scene.LimbConstraints.Length > 256) throw new ArgumentException("Invalid limb constraint collection");
            if (scene.LimbConstraints.Length == 0) return new int[0];
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++) nodes.Add(scene.Nodes[index].Id, index);
            int[][] ancestry = Intervals(scene);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> writers = new HashSet<int>();
            int[][] endpoints = new int[scene.LimbConstraints.Length][];
            for (int index = 0; index < endpoints.Length; index++)
            {
                SceneLimbConstraint constraint = scene.LimbConstraints[index];
                if (constraint == null || string.IsNullOrEmpty(constraint.Id) || constraint.Id.Length > 1024 || ids.Add(constraint.Id) == false)
                    throw new ArgumentException("Constraints require unique stable identities");
                string[] names = { constraint.Upper, constraint.Lower, constraint.End, constraint.Goal, constraint.Pole };
                int[] resolved = new int[5];
                for (int component = 0; component < names.Length; component++)
                    if (names[component] == null || nodes.TryGetValue(names[component], out resolved[component]) == false)
                        throw new ArgumentException("Constraint node does not exist");
                int upper = resolved[0];
                int lower = resolved[1];
                int end = resolved[2];
                if (upper == lower || lower == end || upper == end || Ancestor(ancestry, upper, lower) == false || Ancestor(ancestry, lower, end) == false)
                    throw new ArgumentException("Constraint joints must be distinct and in ancestor order");
                if (constraint.Enabled == false) continue;
                if (writers.Add(upper) == false || writers.Add(lower) == false) throw new ArgumentException("Constraints compete for joint rotations");
                if (Ancestor(ancestry, upper, resolved[3]) || Ancestor(ancestry, upper, resolved[4]))
                    throw new ArgumentException("Constraint targets cannot depend on their own joint rotations");
                endpoints[index] = resolved;
            }
            List<int>[] followers = new List<int>[endpoints.Length];
            int[] dependencies = new int[endpoints.Length];
            for (int index = 0; index < followers.Length; index++) followers[index] = new List<int>();
            for (int writer = 0; writer < endpoints.Length; writer++)
            {
                for (int reader = 0; reader < endpoints.Length; reader++)
                {
                    if (writer == reader || endpoints[writer] == null || endpoints[reader] == null) continue;
                    bool affected = false;
                    foreach (int node in endpoints[reader]) affected |= Ancestor(ancestry, endpoints[writer][0], node) || Ancestor(ancestry, endpoints[writer][1], node);
                    if (affected) { followers[writer].Add(reader); dependencies[reader]++; }
                }
            }
            Queue<int> ready = new Queue<int>();
            int active = 0;
            for (int index = 0; index < dependencies.Length; index++)
                if (endpoints[index] != null) { active++; if (dependencies[index] == 0) ready.Enqueue(index); }
            List<int> order = new List<int>();
            while (ready.Count > 0)
            {
                int current = ready.Dequeue();
                order.Add(current);
                foreach (int next in followers[current]) if (--dependencies[next] == 0) ready.Enqueue(next);
            }
            if (order.Count != active) throw new ArgumentException("Constraint dependency cycle");
            return order.ToArray();
        }

        internal static bool Ancestor(int[][] intervals, int ancestor, int node)
        {
            return intervals[0][ancestor] <= intervals[0][node] && intervals[1][node] <= intervals[1][ancestor];
        }

        internal static int[][] Intervals(SceneDocument scene)
        {
            int[] enter = new int[scene.Nodes.Length];
            int[] exit = new int[scene.Nodes.Length];
            List<int>[] children = new List<int>[scene.Nodes.Length];
            Stack<int> pending = new Stack<int>();
            for (int index = 0; index < children.Length; index++) children[index] = new List<int>();
            for (int index = scene.Nodes.Length - 1; index >= 0; index--)
            {
                int parent = scene.Nodes[index].Parent;
                if (parent < -1 || parent >= scene.Nodes.Length) throw new ArgumentException("Invalid scene parent");
                if (parent < 0) pending.Push(index);
                else children[parent].Add(index);
            }
            int clock = 0;
            int visited = 0;
            while (pending.Count > 0)
            {
                int node = pending.Pop();
                if (node < 0) { exit[~node] = ++clock; continue; }
                if (enter[node] != 0) throw new ArgumentException("Cyclic scene hierarchy");
                visited++;
                enter[node] = ++clock;
                pending.Push(~node);
                foreach (int child in children[node]) pending.Push(child);
            }
            if (visited != scene.Nodes.Length) throw new ArgumentException("Cyclic scene hierarchy");
            return new[] { enter, exit };
        }

        public static SceneLimbConstraint[] Copy(SceneLimbConstraint[] source, string prefix = null)
        {
            SceneLimbConstraint[] result = new SceneLimbConstraint[source.Length];
            string space = prefix == null ? "" : prefix + ":";
            for (int index = 0; index < source.Length; index++)
            {
                SceneLimbConstraint value = source[index];
                result[index] = new SceneLimbConstraint { Id = space + value.Id, Upper = space + value.Upper, Lower = space + value.Lower,
                    End = space + value.End, Goal = space + value.Goal, Pole = space + value.Pole, Enabled = value.Enabled };
            }
            return result;
        }

        public static HashSet<string> RigidNodes(SceneDocument scene)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            SceneDynamics.AddRigidNodes(scene, result);
            if (scene.LimbConstraints.Length == 0) return result;
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++) nodes.Add(scene.Nodes[index].Id, index);
            foreach (SceneLimbConstraint constraint in scene.LimbConstraints)
            {
                if (constraint.Enabled == false) continue;
                int node = nodes[constraint.Lower];
                while (node >= 0)
                {
                    if (result.Add(scene.Nodes[node].Id) == false) break;
                    node = scene.Nodes[node].Parent;
                }
            }
            return result;
        }

        public static void EnsureScale(float[] scale)
        {
            float maximum = Math.Max(Math.Abs(scale[0]), Math.Max(Math.Abs(scale[1]), Math.Abs(scale[2])));
            if (scale[0] <= 0 || scale[1] <= 0 || scale[2] <= 0 || float.IsNaN(maximum) || float.IsInfinity(maximum) ||
                Math.Abs(scale[0] - scale[1]) > maximum * .0001f || Math.Abs(scale[0] - scale[2]) > maximum * .0001f)
                throw new ArgumentException("Persistent limb constraints require positive uniform ancestor scale");
        }

        public static void Pin(SceneDocument document, string id, string label, string upper, string lower, string end, float[] goal, float[] pole)
        {
            if (goal == null || pole == null) throw new ArgumentException("Goal and pole positions are required");
            SceneDocument candidate = SceneCloning.Copy(document);
            List<SceneNode> nodes = new List<SceneNode>(candidate.Nodes);
            nodes.Add(new SceneNode { Id = id + ":goal", Label = label + " goal", Parent = -1, Position = (float[])goal.Clone(), Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } });
            nodes.Add(new SceneNode { Id = id + ":pole", Label = label + " pole", Parent = -1, Position = (float[])pole.Clone(), Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } });
            List<SceneLimbConstraint> constraints = new List<SceneLimbConstraint>(candidate.LimbConstraints)
            {
                new SceneLimbConstraint { Id = id, Upper = upper, Lower = lower, End = end, Goal = id + ":goal", Pole = id + ":pole" }
            };
            candidate.Nodes = nodes.ToArray();
            candidate.LimbConstraints = constraints.ToArray();
            SceneValidation.Ensure(candidate);
            document.Nodes = candidate.Nodes;
            document.LimbConstraints = candidate.LimbConstraints;
        }
    }
}
