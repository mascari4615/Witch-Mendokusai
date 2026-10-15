using System;
using System.IO;
using Rigging;
using Rigging.Serialization;
using Rigging.Unity;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class LimbPanel : IDisposable
    {
        private ContentWorkspace workspace;
        private RigDefinition rig;
        private string[] labels;
        private int selected;
        private Vector3 goal;
        private Vector3 pole;
        private bool expanded;
        private bool handles;
        private string message;
        private readonly Action<string[]> record;

        public LimbPanel(Action<string[]> recordNodes)
        {
            record = recordNodes;
            SceneView.duringSceneGui += DrawHandles;
        }

        public void Bind(ContentWorkspace value)
        {
            workspace = value;
            rig = null;
            handles = false;
            selected = 0;
            string file = RigFiles.Find(value.FilePath);
            if (File.Exists(file))
            {
                try
                {
                    Load(file);
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                }
            }
        }

        public void Load(string file)
        {
            RigDefinition candidate = RigJson.Read(File.ReadAllText(file));
            foreach (RigJoint joint in candidate.Joints)
            {
                workspace.Instance.FindNode(joint.Node);
            }
            rig = candidate;
            labels = new string[rig.Limbs.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index] = rig.Limbs[index].Id;
            }
            selected = 0;
            ResetGoal();
            message = null;
        }

        public void Draw()
        {
            expanded = EditorGUILayout.Foldout(expanded, "Limb goals", true);
            if (expanded == false)
            {
                return;
            }
            if (GUILayout.Button("Open rig bindings..."))
            {
                string file = EditorUtility.OpenFilePanel("Open rig bindings", "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    try { Load(file); }
                    catch (Exception exception) { message = exception.Message; }
                }
            }
            if (rig == null || rig.Limbs.Length == 0)
            {
                EditorGUILayout.HelpBox(message ?? "Open rig bindings to move hands or feet by their goals.", MessageType.Info);
                return;
            }
            int next = EditorGUILayout.Popup("Limb", selected, labels);
            if (next != selected)
            {
                selected = next;
                ResetGoal();
            }
            handles = EditorGUILayout.Toggle("Show stage handles", handles);
            goal = EditorGUILayout.Vector3Field("Goal (world)", goal);
            pole = EditorGUILayout.Vector3Field("Bend pole (world)", pole);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply goal"))
            {
                ApplyGoal();
            }
            if (GUILayout.Button("Use current end"))
            {
                ResetGoal();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Record limb key"))
            {
                RigLimb limb = rig.Limbs[selected];
                record(new[] { rig.Resolve(limb.Upper), rig.Resolve(limb.Lower) });
                message = "Recorded both joint poses at the current clip time.";
            }
            if (string.IsNullOrEmpty(message) == false)
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
        }

        private Transform Joint(string role)
        {
            return workspace.Instance.FindNode(rig.Resolve(role));
        }

        private void ResetGoal()
        {
            if (rig.Limbs.Length == 0)
            {
                return;
            }
            RigLimb limb = rig.Limbs[selected];
            Vector3 upper = Joint(limb.Upper).position;
            Vector3 lower = Joint(limb.Lower).position;
            goal = Joint(limb.End).position;
            Vector3 axis = goal - upper;
            Vector3 bend = lower - upper;
            bend -= Vector3.Project(bend, axis);
            if (bend.sqrMagnitude < .000001f)
            {
                bend = Vector3.Cross(axis, Mathf.Abs(axis.normalized.y) < .9f ? Vector3.up : Vector3.right);
            }
            pole = lower + bend.normalized * Mathf.Max(.1f, axis.magnitude);
        }

        private void ApplyGoal()
        {
            RigLimb limb = rig.Limbs[selected];
            Transform upper = Joint(limb.Upper);
            Transform lower = Joint(limb.Lower);
            Undo.RecordObjects(new UnityEngine.Object[] { upper, lower }, "Move limb goal");
            try
            {
                LimbSolution solution = TransformLimbIk.Apply(upper, lower, Joint(limb.End), goal, pole);
                message = solution.Reachable ? "Goal reached." : "Goal is outside limb reach; stopped at the closest reachable point.";
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                message = exception.Message;
            }
        }

        private void DrawHandles(SceneView view)
        {
            if (handles == false || rig == null || workspace == null || workspace.IsLoaded == false || view is ContentStageView == false)
            {
                return;
            }
            EditorGUI.BeginChangeCheck();
            Vector3 nextGoal = Handles.PositionHandle(goal, Quaternion.identity);
            Vector3 nextPole = Handles.PositionHandle(pole, Quaternion.identity);
            Handles.Label(nextGoal, "Goal");
            Handles.Label(nextPole, "Bend pole");
            Handles.DrawDottedLine(nextGoal, nextPole, 4);
            if (EditorGUI.EndChangeCheck())
            {
                goal = nextGoal;
                pole = nextPole;
                ApplyGoal();
            }
        }

        public void Dispose()
        {
            SceneView.duringSceneGui -= DrawHandles;
        }
    }
}
