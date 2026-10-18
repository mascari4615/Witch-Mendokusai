using System;
using System.Collections.Generic;

namespace SceneContent
{
    public sealed class SpringDistribution
    {
        public float FrequencyStart = 2;
        public float FrequencyEnd = 2;
        public float DampingStart = .5f;
        public float DampingEnd = .5f;
        public float RadiusStart = .01f;
        public float RadiusEnd = .01f;
        public float[] Acceleration = new[] { 0f, -9.81f, 0f };
    }

    public static class SceneSpringAuthoring
    {
        public static void SetChain(SceneDocument scene, string first, string last, SpringDistribution distribution)
        {
            if (distribution == null || distribution.Acceleration == null) throw new ArgumentNullException("distribution");
            SceneDocument candidate = SceneCloning.Copy(scene);
            Dictionary<string, int> nodes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++) nodes.Add(scene.Nodes[index].Id, index);
            if (first == null || last == null || nodes.ContainsKey(first) == false || nodes.ContainsKey(last) == false || first == last)
                throw new ArgumentException("Choose distinct existing chain endpoints");
            List<int> path = new List<int>();
            int current = nodes[last];
            while (current >= 0)
            {
                path.Add(current);
                if (current == nodes[first]) break;
                if (path.Count > 257) throw new ArgumentException("Spring chain exceeds limit");
                current = scene.Nodes[current].Parent;
            }
            if (current < 0) throw new ArgumentException("The end must descend from the selected joint");
            path.Reverse();
            float[] along = new float[path.Count - 1];
            for (int index = 1; index < along.Length; index++)
            {
                float[] position = scene.Nodes[path[index]].Position;
                float scale = 1;
                for (int parent = path[index - 1]; parent >= 0; parent = scene.Nodes[parent].Parent) scale *= scene.Nodes[parent].Scale[0];
                along[index] = along[index - 1] + (float)Math.Sqrt(position[0] * position[0] + position[1] * position[1] + position[2] * position[2]) * scale;
            }
            List<SceneSpring> springs = new List<SceneSpring>(candidate.Springs);
            for (int index = 0; index < path.Count - 1; index++)
            {
                string joint = scene.Nodes[path[index]].Id;
                string tip = scene.Nodes[path[index + 1]].Id;
                SceneSpring spring = springs.Find(value => value.Joint == joint);
                if (spring == null) { spring = new SceneSpring { Id = Guid.NewGuid().ToString("N"), Joint = joint }; springs.Add(spring); }
                float fraction = along.Length < 2 || along[along.Length - 1] <= 0 ? 0 : along[index] / along[along.Length - 1];
                spring.Tip = tip; spring.Enabled = true;
                spring.FrequencyHz = Mix(distribution.FrequencyStart, distribution.FrequencyEnd, fraction);
                spring.DampingRatio = Mix(distribution.DampingStart, distribution.DampingEnd, fraction);
                spring.Radius = Mix(distribution.RadiusStart, distribution.RadiusEnd, fraction);
                spring.Acceleration = (float[])distribution.Acceleration.Clone();
            }
            candidate.Springs = springs.ToArray();
            SceneValidation.Ensure(candidate);
            scene.Springs = candidate.Springs;
        }

        private static float Mix(float first, float last, float fraction) { return first + (last - first) * fraction; }
    }
}
