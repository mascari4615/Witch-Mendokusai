using System;
using System.Collections.Generic;
using System.Numerics;
using Dynamics;
using Motion;
using SceneContent;

namespace SceneSequence
{
    public sealed class SceneDynamicsPlan
    {
        private readonly DynamicsFrame frame;
        private readonly SceneSpring[] springs;
        private readonly SceneCollider[] colliders;
        private readonly int[] order;
        private readonly int[] joints;
        private readonly int[] tips;
        private readonly Quaternion[] rest;
        private readonly Quaternion[] driven;
        private readonly string[] rigid;
        private SpringState[] states;
        private Vector3[] anchors;
        private readonly PoseValue[] backup;
        public int Count { get { return order.Length; } }

        public SceneDynamicsPlan(SceneDocument scene)
        {
            SceneValidation.Ensure(scene);
            order = SceneDynamics.Order(scene);
            frame = new DynamicsFrame(scene);
            backup = new PoseValue[scene.Nodes.Length];
            springs = SceneDynamics.Copy(scene.Springs); colliders = SceneDynamics.Copy(scene.Colliders);
            joints = new int[order.Length]; tips = new int[order.Length]; rest = new Quaternion[order.Length];
            driven = new Quaternion[order.Length];
            HashSet<string> rigidNodes = SceneConstraints.RigidNodes(scene);
            rigid = new string[rigidNodes.Count]; rigidNodes.CopyTo(rigid);
            for (int index = 0; index < order.Length; index++)
            {
                joints[index] = frame.Indices[springs[order[index]].Joint]; tips[index] = frame.Indices[springs[order[index]].Tip];
                float[] rotation = scene.Nodes[joints[index]].Rotation;
                rest[index] = Quaternion.Normalize(new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]));
            }
        }

        public void Reset() { states = null; anchors = null; }

        internal sealed class Checkpoint
        {
            public SpringState[] States;
            public Vector3[] Anchors;
            public Quaternion[] Driven;
        }

        internal Checkpoint Capture(IPoseTarget target)
        {
            for (int index = 0; index < backup.Length; index++) backup[index] = target.Read(frame.Ids[index]);
            return new Checkpoint { States = states, Anchors = anchors, Driven = (Quaternion[])driven.Clone() };
        }

        internal void Restore(Checkpoint checkpoint, IPoseTarget target)
        {
            for (int index = 0; index < backup.Length; index++) target.Write(frame.Ids[index], backup[index]);
            states = checkpoint.States;
            anchors = checkpoint.Anchors;
            Array.Copy(checkpoint.Driven, driven, driven.Length);
        }

        public void RestoreAuthoredRotations(IPoseTarget target)
        {
            for (int index = 0; index < joints.Length; index++)
            {
                string id = frame.Ids[joints[index]];
                PoseValue pose = target.Read(id); pose.Rotation = rest[index]; target.Write(id, pose);
            }
        }

        public void RestoreDrivenRotations(IPoseTarget target)
        {
            for (int index = 0; index < joints.Length; index++)
            {
                string id = frame.Ids[joints[index]];
                PoseValue pose = target.Read(id); pose.Rotation = driven[index]; target.Write(id, pose);
            }
        }

        public void Apply(IPoseTarget target, float seconds, bool commit = true)
        {
            if (Count == 0) return;
            if (target == null) throw new ArgumentNullException("target");
            frame.Read(target);
            for (int index = 0; index < Count; index++) driven[index] = frame.Local[joints[index]].Rotation;
            foreach (string id in rigid)
            {
                Vector3 scale = frame.Local[frame.Indices[id]].Scale;
                SceneConstraints.EnsureScale(new[] { scale.X, scale.Y, scale.Z });
            }
            SpringState[] next = states == null ? new SpringState[Count] : (SpringState[])states.Clone();
            Vector3[] nextAnchors = new Vector3[Count];
            for (int index = 0; index < Count; index++)
            {
                SceneSpring spring = springs[order[index]];
                int joint = joints[index]; int tip = tips[index];
                Vector3 anchor = frame.Position(joint); Vector3 goal = frame.Position(tip);
                CapsuleObstacle[] obstacles = new CapsuleObstacle[spring.Colliders.Length];
                for (int collider = 0; collider < obstacles.Length; collider++)
                {
                    SceneCollider source = Array.Find(colliders, value => value.Id == spring.Colliders[collider]);
                    int node = frame.Indices[source.Node];
                    float scale = Vector3.TransformNormal(Vector3.UnitX, frame.World[node]).Length();
                    obstacles[collider] = new CapsuleObstacle { Start = Vector3.Transform(V(source.Start), frame.World[node]), End = Vector3.Transform(V(source.End), frame.World[node]), Radius = source.Radius * scale };
                }
                // Inertia: the particle keeps this fraction of the joint's own movement since the last committed step, so it lags only by the rest.
                if (states == null) next[index] = new SpringState { Position = goal };
                else if (spring.Inertia > 0 && anchors != null) next[index].Position += (anchor - anchors[index]) * spring.Inertia;
                nextAnchors[index] = anchor;
                next[index] = SpringParticle.Step(next[index], goal, anchor, (goal - anchor).Length(), new SpringSettings
                {
                    FrequencyHz = spring.FrequencyHz, DampingRatio = spring.DampingRatio,
                    Radius = spring.Radius * Vector3.TransformNormal(Vector3.UnitX, frame.World[joint]).Length(), Acceleration = V(spring.Acceleration)
                }, obstacles, seconds);
                frame.Rotate(joint, goal - anchor, next[index].Position - anchor);
                if (Vector3.Distance(frame.Position(tip), next[index].Position) > .00002f) throw new ArgumentException("Spring rotation did not preserve its segment");
            }
            foreach (int joint in joints) target.Write(frame.Ids[joint], frame.Local[joint]);
            if (commit) { states = next; anchors = nextAnchors; }
        }

        private static Vector3 V(float[] value) { return new Vector3(value[0], value[1], value[2]); }
    }

    public sealed class SceneDynamicsClock
    {
        private readonly SceneDynamicsPlan plan;
        private readonly IPoseTarget target;
        private int tick = -1;
        private float fraction;
        private readonly IValueTarget values;
        private readonly string[] properties;
        private bool evaluating;
        public int Count { get { return plan.Count; } }
        public int StepBudget { get; set; } = 4096;

        public SceneDynamicsClock(SceneDocument scene, IPoseTarget target, IValueTarget values = null, IEnumerable<string> properties = null)
        {
            plan = new SceneDynamicsPlan(scene); this.target = target;
            if (plan.Count > 0 && target == null) throw new ArgumentNullException("target");
            HashSet<string> names = new HashSet<string>(properties ?? new string[0], StringComparer.Ordinal);
            this.properties = new string[names.Count]; names.CopyTo(this.properties); this.values = values;
            if ((values == null && names.Count > 0) || names.Contains(null)) throw new ArgumentException("Known scalar targets require a value port");
            Array.Sort(this.properties, StringComparer.Ordinal);
        }
        public void Invalidate() { tick = -1; plan.Reset(); }

        public void Evaluate(float seconds, Action<float> sample)
        {
            if (Count == 0) { sample(seconds); return; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds > int.MaxValue / 120f || sample == null)
                throw new ArgumentException("Invalid dynamics time or sampler");
            double requestedTick = Math.Floor((double)seconds * 120);
            if (requestedTick > int.MaxValue) throw new ArgumentException("Dynamics time exceeds the tick range");
            int requested = (int)requestedTick;
            bool reset = tick < 0 || requested < tick;
            if (requested - (reset ? 0 : tick) > StepBudget) throw new ArgumentException("Dynamics seek exceeds the step budget; advance in smaller intervals");
            if (evaluating) throw new InvalidOperationException("Dynamics evaluation cannot be reentrant");
            SceneDynamicsPlan.Checkpoint previous = plan.Capture(target);
            float[] priorValues = new float[properties.Length];
            for (int index = 0; index < properties.Length; index++) priorValues[index] = values.Read(properties[index]);
            int oldTick = tick; float oldFraction = fraction;
            evaluating = true;
            try
            {
                if (reset)
                {
                    plan.Reset(); Sample(0, sample); plan.Apply(target, 0); tick = 0;
                }
                while (tick < requested)
                {
                    Sample((float)((tick + 1) / 120.0), sample); plan.Apply(target, 1f / 120); tick++;
                }
                Sample(seconds, sample);
                fraction = (float)(seconds - tick / 120.0);
                plan.Apply(target, fraction, commit: false);
            }
            catch
            {
                plan.Restore(previous, target);
                for (int index = 0; index < properties.Length; index++) values.Write(properties[index], priorValues[index]);
                tick = oldTick; fraction = oldFraction;
                throw;
            }
            finally { evaluating = false; }
        }

        public void Refresh(Action<float> sample, Action constraints)
        {
            if (Count == 0) return;
            if (tick < 0) { Evaluate(0, sample); return; }
            if (evaluating) throw new InvalidOperationException("Dynamics evaluation cannot be reentrant");
            SceneDynamicsPlan.Checkpoint previous = plan.Capture(target);
            evaluating = true;
            try
            {
                plan.RestoreDrivenRotations(target);
                constraints();
                plan.Apply(target, fraction, commit: false);
            }
            catch { plan.Restore(previous, target); throw; }
            finally { evaluating = false; }
        }

        private void Sample(float seconds, Action<float> sample) { plan.RestoreAuthoredRotations(target); sample(seconds); }
    }
}
