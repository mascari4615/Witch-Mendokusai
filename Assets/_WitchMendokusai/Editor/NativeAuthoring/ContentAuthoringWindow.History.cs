using System;
using System.Collections.Generic;
using Motion;
using Motion.Unity;
using SceneContent;
using SceneContent.Unity;
using WitchMendokusai.ContentRuntime;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class ContentAuthoringWindow
    {
        public void UndoSceneChange()
        {
            workspace.UndoChange(ValidateMotionTargets);
            RefreshPreview();
        }

        public void RedoSceneChange()
        {
            workspace.RedoChange(ValidateMotionTargets);
            RefreshPreview();
        }

        private void ValidateMotionTargets(SceneDocument document, SceneInstance instance)
        {
            try
            {
                new PoseClipSampler(motion.Clip).ValidateTargets(new TransformPoseTarget(instance.FindNode), SceneMotionBindings.Create(document, () => instance));
                SceneSequence.SequenceValidation.Ensure(new SceneSequence.SequenceDocument { Id = "workspace", Scene = document, Clips = new[] { motion.Clip } });
                new SceneSequence.SceneConstraintPlan(document).Apply(new TransformPoseTarget(instance.FindNode));
                new SceneSequence.SceneDynamicsPlan(document).Apply(new TransformPoseTarget(instance.FindNode), 0);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidOperationException("This scene change would remove a motion target. Remove the affected keys or start a new clip first.", exception);
            }
        }

        private void DrawSceneHistory()
        {
            const string TOOLTIP = "Restore camera, light and surface changes. Opening a scene or adding an actor starts a new scene history.";
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false || workspace.CanUndoChange == false))
            {
                if (GUILayout.Button(new GUIContent(AuthoringText.Get("Undo scene change"), AuthoringText.Get(TOOLTIP))))
                {
                    Run(UndoSceneChange);
                }
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false || workspace.CanRedoChange == false))
            {
                if (GUILayout.Button(new GUIContent(AuthoringText.Get("Redo scene change"), AuthoringText.Get(TOOLTIP))))
                {
                    Run(RedoSceneChange);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
