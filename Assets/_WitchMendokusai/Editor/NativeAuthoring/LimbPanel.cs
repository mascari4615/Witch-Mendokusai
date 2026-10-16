using System;
using System.IO;
using Rigging;
using Rigging.Serialization;
using Rigging.Unity;
using SceneContent;
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
        private Action<Action<SceneDocument>> change;

        public LimbPanel(Action<string[]> recordNodes)
        {
            record = recordNodes;
            SceneView.duringSceneGui += DrawHandles;
        }

        public void Bind(ContentWorkspace value, Action<Action<SceneDocument>> change = null)
        {
            string previous = rig != null && selected < rig.Limbs.Length ? rig.Limbs[selected].Id : null;
            this.change = change;
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
                    int preserved = Array.FindIndex(rig.Limbs, limb => limb.Id == previous);
                    if (preserved >= 0) { selected = preserved; ResetGoal(); }
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
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Limb goals"), true);
            if (expanded == false)
            {
                return;
            }
            if (GUILayout.Button(AuthoringText.Get("Open rig bindings...")))
            {
                string file = EditorUtility.OpenFilePanel(AuthoringText.Get("Open rig bindings"), "", "json");
                if (string.IsNullOrEmpty(file) == false)
                {
                    try { Load(file); }
                    catch (Exception exception) { message = exception.Message; }
                }
            }
            if (rig == null || rig.Limbs.Length == 0)
            {
                EditorGUILayout.HelpBox(AuthoringText.Get(message ?? "Open rig bindings to move hands or feet by their goals."), MessageType.Info);
                return;
            }
            int next = EditorGUILayout.Popup(AuthoringText.Get("Limb"), selected, AuthoringText.Labels(labels));
            if (next != selected)
            {
                selected = next;
                ResetGoal();
            }
            handles = EditorGUILayout.Toggle(AuthoringText.Get("Show stage handles"), handles);
            SceneLimbConstraint current = FindConstraint();
            if (current != null && current.Enabled && EditorGUIUtility.editingTextField == false)
            {
                goal = workspace.Instance.FindNode(current.Goal).position;
                pole = workspace.Instance.FindNode(current.Pole).position;
            }
            EditorGUI.BeginChangeCheck();
            goal = EditorGUILayout.Vector3Field(AuthoringText.Get("Goal (world)"), goal);
            pole = EditorGUILayout.Vector3Field(AuthoringText.Get("Bend pole (world)"), pole);
            if (EditorGUI.EndChangeCheck() && current != null && current.Enabled) ApplyGoal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Apply goal")))
            {
                ApplyGoal();
            }
            if (GUILayout.Button(AuthoringText.Get("Use current end")))
            {
                ResetGoal();
            }
            EditorGUILayout.EndHorizontal();
            SceneLimbConstraint persistent = FindConstraint();
            bool maintained = persistent != null && persistent.Enabled;
            bool nextMaintained = EditorGUILayout.Toggle(AuthoringText.Get("Keep goal during playback"), maintained);
            if (nextMaintained != maintained) SetPersistentGoal(nextMaintained);
            if (maintained) EditorGUILayout.LabelField(AuthoringText.Get("This goal stays active after motion. Recording stores the goal and bend pole."), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button(AuthoringText.Get(maintained ? "Record goal key" : "Record limb key")))
            {
                RigLimb limb = rig.Limbs[selected];
                record(maintained ? new[] { persistent.Goal, persistent.Pole } : new[] { rig.Resolve(limb.Upper), rig.Resolve(limb.Lower) });
                message = maintained ? "Recorded goal and bend pole at the current clip time." : "Recorded both joint poses at the current clip time.";
            }
            if (string.IsNullOrEmpty(message) == false)
            {
                EditorGUILayout.HelpBox(AuthoringText.Get(message), MessageType.Info);
            }
        }

        private Transform Joint(string role)
        {
            return workspace.Instance.FindNode(rig.Resolve(role));
        }

        private SceneLimbConstraint FindConstraint()
        {
            if (rig == null || rig.Limbs.Length == 0) return null;
            RigLimb limb = rig.Limbs[selected];
            return Array.Find(workspace.Document.LimbConstraints, value => value.Upper == rig.Resolve(limb.Upper) && value.Lower == rig.Resolve(limb.Lower) && value.End == rig.Resolve(limb.End));
        }

        public void SetPersistentGoal(bool enabled)
        {
            if (change == null || rig == null || rig.Limbs.Length == 0) throw new InvalidOperationException("A loaded editable limb is required");
            SceneLimbConstraint constraint = FindConstraint();
            if (constraint != null)
            {
                string id = constraint.Id;
                change(document => Array.Find(document.LimbConstraints, value => value.Id == id).Enabled = enabled);
                return;
            }
            if (enabled == false) return;
            RigLimb limb = rig.Limbs[selected];
            string identity = "limb-" + Guid.NewGuid().ToString("N");
            Vector3 localGoal = workspace.Instance.transform.InverseTransformPoint(goal);
            Vector3 localPole = workspace.Instance.transform.InverseTransformPoint(pole);
            change(document => SceneConstraints.Pin(document, identity, limb.Id, rig.Resolve(limb.Upper), rig.Resolve(limb.Lower), rig.Resolve(limb.End),
                new[] { localGoal.x, localGoal.y, -localGoal.z }, new[] { localPole.x, localPole.y, -localPole.z }));
        }

        private void ResetGoal()
        {
            if (rig.Limbs.Length == 0)
            {
                return;
            }
            RigLimb limb = rig.Limbs[selected];
            SceneLimbConstraint persistent = FindConstraint();
            if (persistent != null)
            {
                goal = workspace.Instance.FindNode(persistent.Goal).position;
                pole = workspace.Instance.FindNode(persistent.Pole).position;
                return;
            }
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
            SceneLimbConstraint persistent = FindConstraint();
            Transform goalNode = persistent == null ? null : workspace.Instance.FindNode(persistent.Goal);
            Transform poleNode = persistent == null ? null : workspace.Instance.FindNode(persistent.Pole);
            Undo.RecordObjects(persistent == null ? new UnityEngine.Object[] { upper, lower } : new UnityEngine.Object[] { upper, lower, goalNode, poleNode }, "Move limb goal");
            try
            {
                LimbSolution solution = TransformLimbIk.Apply(upper, lower, Joint(limb.End), goal, pole);
                if (persistent != null) { goalNode.position = goal; poleNode.position = pole; }
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
            SceneLimbConstraint persistent = FindConstraint();
            if (persistent != null && persistent.Enabled)
            {
                goal = workspace.Instance.FindNode(persistent.Goal).position;
                pole = workspace.Instance.FindNode(persistent.Pole).position;
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
