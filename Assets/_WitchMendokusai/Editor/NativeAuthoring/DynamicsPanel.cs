using System;
using System.Collections.Generic;
using SceneContent;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class DynamicsPanel : IDisposable
    {
        private ContentWorkspace workspace;
        private Action<Action<SceneDocument>> change;
        private bool expanded;
        private bool handles;
        private int selectedSpring;
        private int rootSelection = -2;
        private int endSelection;
        private int[] descendants = new int[0];
        private string loadedSpring;
        private float frequency = 2;
        private float endFrequency = 2;
        private float damping = .5f;
        private float inertia;
        private float endDamping = .5f;
        private float radius = .01f;
        private float endRadius = .01f;
        private Vector3 acceleration = new Vector3(0, -9.81f, 0);
        private bool enabled = true;
        private readonly HashSet<string> collisions = new HashSet<string>(StringComparer.Ordinal);
        private bool showColliders;
        private Vector3 colliderEnd;
        private float colliderRadius = .1f;

        public DynamicsPanel() { SceneView.duringSceneGui += DrawHandles; }
        public void Reveal() { expanded = true; handles = true; }

        public void Bind(ContentWorkspace value, Action<Action<SceneDocument>> apply)
        {
            workspace = value; change = apply; rootSelection = -2; loadedSpring = null;
        }

        public void Draw(int selected)
        {
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Secondary motion"), true);
            if (expanded == false) return;
            handles = EditorGUILayout.Toggle(AuthoringText.Get("Show stage handles"), handles);
            SceneDocument scene = workspace.Document;
            if (scene.Springs.Length > 0)
            {
                string[] names = new string[scene.Springs.Length];
                for (int index = 0; index < names.Length; index++) names[index] = Label(Array.FindIndex(scene.Nodes, node => node.Id == scene.Springs[index].Joint));
                selectedSpring = EditorGUILayout.Popup(AuthoringText.Get("Spring joint"), Math.Min(selectedSpring, names.Length - 1), names);
                SceneSpring spring = scene.Springs[selectedSpring];
                if (loadedSpring != spring.Id)
                {
                    loadedSpring = spring.Id; frequency = spring.FrequencyHz; damping = spring.DampingRatio; inertia = spring.Inertia; radius = spring.Radius; acceleration = V(spring.Acceleration); enabled = spring.Enabled;
                    collisions.Clear(); foreach (string id in spring.Colliders) collisions.Add(id);
                }
                enabled = EditorGUILayout.Toggle(AuthoringText.Get("Enabled"), enabled);
            }
            frequency = EditorGUILayout.FloatField(AuthoringText.Get("Frequency (Hz)"), frequency);
            damping = EditorGUILayout.FloatField(AuthoringText.Get("Damping ratio"), damping);
            inertia = EditorGUILayout.Slider(AuthoringText.Get("Movement carried along"), inertia, 0, 1);
            radius = EditorGUILayout.FloatField(AuthoringText.Get("Contact radius (local)"), radius);
            acceleration = EditorGUILayout.Vector3Field(AuthoringText.Get("Acceleration (scene)"), acceleration);
            if (scene.Springs.Length > 0)
            {
                foreach (SceneCollider collider in scene.Colliders)
                {
                    bool next = EditorGUILayout.Toggle(Label(Array.FindIndex(scene.Nodes, node => node.Id == collider.Node)) + " / " + AuthoringText.Get("Collider"), collisions.Contains(collider.Id));
                    if (next) collisions.Add(collider.Id); else collisions.Remove(collider.Id);
                }
                if (GUILayout.Button(AuthoringText.Get("Apply spring settings")))
                {
                    string id = scene.Springs[selectedSpring].Id;
                    change(document =>
                    {
                        SceneSpring spring = Array.Find(document.Springs, value => value.Id == id);
                        spring.Enabled = enabled; spring.FrequencyHz = frequency; spring.DampingRatio = damping; spring.Inertia = Mathf.Clamp01(inertia); spring.Radius = radius;
                        spring.Acceleration = A(acceleration); spring.Colliders = new List<string>(collisions).ToArray();
                    });
                }
                if (GUILayout.Button(AuthoringText.Get("Remove spring")))
                {
                    string id = scene.Springs[selectedSpring].Id;
                    change(document => { List<SceneSpring> remaining = new List<SceneSpring>(document.Springs); remaining.RemoveAll(value => value.Id == id); document.Springs = remaining.ToArray(); });
                }
            }
            if (selected >= 0)
            {
                if (rootSelection != selected)
                {
                    rootSelection = selected; descendants = Descendants(selected); endSelection = PreferredEnd(selected);
                    if (descendants.Length > 0)
                    {
                        SceneSpring last = Array.Find(scene.Springs, value => value.Tip == scene.Nodes[descendants[endSelection]].Id);
                        if (last != null) { endFrequency = last.FrequencyHz; endDamping = last.DampingRatio; endRadius = last.Radius; }
                    }
                }
                EditorGUILayout.LabelField(AuthoringText.Get("Chain start"), Label(selected));
                if (descendants.Length > 0)
                {
                    string[] names = new string[descendants.Length];
                    for (int index = 0; index < names.Length; index++) names[index] = Label(descendants[index]);
                    endSelection = EditorGUILayout.Popup(AuthoringText.Get("Chain end"), endSelection, names);
                    endFrequency = EditorGUILayout.FloatField(AuthoringText.Get("End frequency (Hz)"), endFrequency);
                    endDamping = EditorGUILayout.FloatField(AuthoringText.Get("End damping ratio"), endDamping);
                    endRadius = EditorGUILayout.FloatField(AuthoringText.Get("End contact radius"), endRadius);
                    if (GUILayout.Button(AuthoringText.Get("Apply settings along chain")))
                    {
                        string first = scene.Nodes[selected].Id; string last = scene.Nodes[descendants[endSelection]].Id;
                        change(document => SceneSpringAuthoring.SetChain(document, first, last, new SpringDistribution
                        {
                            FrequencyStart = frequency, FrequencyEnd = endFrequency, DampingStart = damping, DampingEnd = endDamping,
                            RadiusStart = radius, RadiusEnd = endRadius, Acceleration = A(acceleration)
                        }));
                    }
                }
                showColliders = EditorGUILayout.Foldout(showColliders, AuthoringText.Get("Collision shapes"), true);
                if (showColliders)
                {
                    colliderEnd = EditorGUILayout.Vector3Field(AuthoringText.Get("Capsule end (local)"), colliderEnd);
                    colliderRadius = EditorGUILayout.FloatField(AuthoringText.Get("Collider radius (local)"), colliderRadius);
                    if (GUILayout.Button(AuthoringText.Get("Add collider to selected node")))
                    {
                        string node = scene.Nodes[selected].Id;
                        change(document =>
                        {
                            List<SceneCollider> colliders = new List<SceneCollider>(document.Colliders)
                            {
                                new SceneCollider { Id = Guid.NewGuid().ToString("N"), Node = node, End = A(colliderEnd), Radius = colliderRadius }
                            };
                            document.Colliders = colliders.ToArray();
                        });
                    }
                    for (int index = 0; index < scene.Colliders.Length; index++)
                    {
                        SceneCollider collider = scene.Colliders[index];
                        if (GUILayout.Button(AuthoringText.Get("Remove collider") + " / " + Label(Array.FindIndex(scene.Nodes, node => node.Id == collider.Node))))
                            change(document => { List<SceneCollider> remaining = new List<SceneCollider>(document.Colliders); remaining.RemoveAll(value => value.Id == collider.Id); document.Colliders = remaining.ToArray(); });
                    }
                }
            }
            EditorGUILayout.HelpBox(AuthoringText.Get("Select a joint and a descendant to create a chain. Disable the spring before editing its rest pose. Seeking recalculates from the start."), MessageType.Info);
        }

        private int[] Descendants(int root)
        {
            List<int>[] children = new List<int>[workspace.Document.Nodes.Length];
            for (int index = 0; index < children.Length; index++) children[index] = new List<int>();
            for (int index = 0; index < children.Length; index++) if (workspace.Document.Nodes[index].Parent >= 0) children[workspace.Document.Nodes[index].Parent].Add(index);
            Stack<int> pending = new Stack<int>(); foreach (int child in children[root]) pending.Push(child);
            List<int> result = new List<int>();
            while (pending.Count > 0) { int node = pending.Pop(); result.Add(node); foreach (int child in children[node]) pending.Push(child); }
            return result.ToArray();
        }

        private int PreferredEnd(int selected)
        {
            string node = workspace.Document.Nodes[selected].Id;
            for (int count = 0; count < 256; count++)
            {
                SceneSpring spring = Array.Find(workspace.Document.Springs, value => value.Joint == node);
                if (spring == null) break;
                node = spring.Tip;
            }
            int existing = Array.FindIndex(descendants, index => workspace.Document.Nodes[index].Id == node);
            if (existing >= 0) return existing;
            int named = Array.FindIndex(descendants, index => string.IsNullOrEmpty(workspace.Document.Nodes[index].Label) == false);
            return Math.Max(0, named);
        }

        private string Label(int index)
        {
            SceneNode node = workspace.Document.Nodes[index];
            return string.IsNullOrEmpty(node.Label) ? node.Id : AuthoringText.NodeLabel(node.Label);
        }

        private void DrawHandles(SceneView view)
        {
            if (handles == false || workspace == null || workspace.IsLoaded == false || (view is ContentStageView) == false) return;
            foreach (SceneSpring spring in workspace.Document.Springs)
            {
                Handles.color = spring.Enabled ? Color.yellow : Color.gray;
                Transform joint = workspace.Instance.FindNode(spring.Joint); Transform tip = workspace.Instance.FindNode(spring.Tip);
                Handles.DrawLine(joint.position, tip.position);
                Sphere(tip.position, spring.Radius * joint.lossyScale.x);
            }
            Handles.color = Color.cyan;
            foreach (SceneCollider collider in workspace.Document.Colliders)
            {
                Transform node = workspace.Instance.FindNode(collider.Node);
                Vector3 start = node.TransformPoint(V(collider.Start)); Vector3 end = node.TransformPoint(V(collider.End));
                float size = collider.Radius * node.lossyScale.x;
                Sphere(start, size); Sphere(end, size);
                if (start == end) continue;
                Vector3 axis = (end - start).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized * size;
                Vector3 other = Vector3.Cross(axis, side);
                foreach (Vector3 offset in new[] { side, -side, other, -other }) Handles.DrawLine(start + offset, end + offset);
            }
        }

        private static void Sphere(Vector3 center, float radius) { Handles.DrawWireDisc(center, Vector3.up, radius); Handles.DrawWireDisc(center, Vector3.right, radius); Handles.DrawWireDisc(center, Vector3.forward, radius); }
        private static Vector3 V(float[] value) { return new Vector3(value[0], value[1], -value[2]); }
        private static float[] A(Vector3 value) { return new[] { value.x, value.y, -value.z }; }
        public void Dispose() { SceneView.duringSceneGui -= DrawHandles; }
    }
}
