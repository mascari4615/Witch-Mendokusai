using System;
using System.Collections.Generic;
using Appearance;
using SceneContent;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed class ParameterPanel
    {
        private ContentWorkspace workspace;
        private Action<Action<SceneDocument>> apply;
        private readonly Dictionary<string, float> pending = new Dictionary<string, float>(StringComparer.Ordinal);
        private bool expanded = true;
        private bool createExpanded;
        private string label = AuthoringText.Get("Size");
        private int axis;
        private float minimum = .5f;
        private float maximum = 1.5f;
        private static readonly string[] AXES = { "Overall size", "Width", "Height", "Depth" };

        public void Bind(ContentWorkspace workspace, Action<Action<SceneDocument>> apply)
        {
            this.workspace = workspace;
            this.apply = apply;
            pending.Clear();
            if (workspace.Document.Parameters == null) return;
            foreach (ParameterControl control in workspace.Document.Parameters.Profile.Controls) pending.Add(control.Id, control.Value);
        }

        public void Draw(int selected)
        {
            expanded = EditorGUILayout.Foldout(expanded, AuthoringText.Get("Body and face controls"), true);
            if (expanded == false) return;
            SceneParameterProfile parameters = workspace.Document.Parameters;
            if (parameters == null || parameters.Profile.Controls.Length == 0)
            {
                EditorGUILayout.LabelField(AuthoringText.Get("Create a named control for a selected part, or open a scene with calibrated controls."), EditorStyles.wordWrappedLabel);
            }
            else
            {
                bool changed = false;
                foreach (ParameterControl control in parameters.Profile.Controls)
                {
                    float value = EditorGUILayout.Slider(AuthoringText.NodeLabel(control.Label), pending[control.Id], control.Minimum, control.Maximum);
                    pending[control.Id] = value;
                    changed |= value != control.Value;
                }
                using (new EditorGUI.DisabledScope(changed == false))
                {
                    if (GUILayout.Button(AuthoringText.Get("Apply body controls"))) ApplyValues(new Dictionary<string, float>(pending));
                }
                if (changed) EditorGUILayout.LabelField(AuthoringText.Get("Unapplied control values. Apply to update the preview."), EditorStyles.wordWrappedLabel);
                if (GUILayout.Button(AuthoringText.Get("Reset body controls")))
                {
                    Dictionary<string, float> defaults = new Dictionary<string, float>(StringComparer.Ordinal);
                    foreach (ParameterControl control in parameters.Profile.Controls) defaults.Add(control.Id, control.Default);
                    ApplyValues(defaults);
                }
            }
            createExpanded = EditorGUILayout.Foldout(createExpanded, AuthoringText.Get("Create scale control"), true);
            if (createExpanded == false) return;
            EditorGUILayout.LabelField(AuthoringText.Get("Select a part in Pose controls. The new control keeps its current scale as the reference."), EditorStyles.wordWrappedLabel);
            label = EditorGUILayout.TextField(AuthoringText.Get("Control name"), label);
            axis = EditorGUILayout.Popup(AuthoringText.Get("Dimension"), axis, AuthoringText.Labels(AXES));
            minimum = EditorGUILayout.FloatField(AuthoringText.Get("Minimum"), minimum);
            maximum = EditorGUILayout.FloatField(AuthoringText.Get("Maximum"), maximum);
            using (new EditorGUI.DisabledScope(selected < 0))
            {
                if (GUILayout.Button(AuthoringText.Get("Add control for selected part")))
                {
                    string node = workspace.Document.Nodes[selected].Id;
                    string id = "control-" + Guid.NewGuid().ToString("N");
                    apply(document => SceneParameterAuthoring.AddScaleControl(document, node, id, label, axis - 1, minimum, maximum));
                }
            }
        }

        private void ApplyValues(Dictionary<string, float> values)
        {
            apply(document => SceneParameters.SetValues(document, values));
        }
    }
}
