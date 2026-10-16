using System;
using System.Collections.Generic;
using System.IO;
using Motion.Serialization;
using SceneContent;
using SceneContent.Storage;
using SceneContent.Unity;
using SceneSequence;
using SceneSequence.Storage;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.ContentAuthoring
{
    public sealed partial class ContentAuthoringWindow
    {
        public void SaveParameterSequence(string file, IDictionary<string, float> values)
        {
            motion.ValidateDraft();
            SceneDocument snapshot = SceneSnapshot.Capture(workspace.Document, workspace.Instance);
            SequenceDocument source = new SequenceDocument { Id = "sequence-" + snapshot.Id, Scene = snapshot, Clips = new[] { motion.Clip } };
            SequenceDocument sequence = SequenceParameters.WithValues(source, values, (mesh, component) =>
            {
                SkinnedMeshRenderer renderer = workspace.Instance.FindMesh(mesh) as SkinnedMeshRenderer;
                if (renderer == null || component >= renderer.sharedMesh.blendShapeCount) throw new ArgumentException("Morph motion requires a bound shape");
                string node = snapshot.Nodes[snapshot.Meshes[mesh].Node].Id;
                return SceneMotionBindings.Expression(node, mesh, renderer.sharedMesh.GetBlendShapeName(component));
            });
            string rig = RigFiles.Read(workspace.FilePath, snapshot);
            RigFiles.Save(Path.GetFullPath(file), rig);
            SequenceFiles.Save(file, sequence, new DirectoryContentSource(Path.GetDirectoryName(workspace.FilePath)));
            message = "Exported control variant with fitted motion. Open the sequence to preview it.";
        }

        private void ExportParameterSequence(IDictionary<string, float> values)
        {
            string file = EditorUtility.SaveFilePanel(AuthoringText.Get("Export control variant"), "", "variant.sequence", "json");
            if (string.IsNullOrEmpty(file) == false) Run(delegate { SaveParameterSequence(file, values); });
        }

        public void SaveSequence(string file)
        {
            motion.ValidateDraft();
            SceneDocument snapshot = SceneSnapshot.Capture(workspace.Document, workspace.Instance);
            SequenceDocument sequence = new SequenceDocument { Id = "sequence-" + snapshot.Id, Scene = snapshot, Clips = new[] { motion.Clip } };
            string rig = RigFiles.Read(workspace.FilePath, snapshot);
            RigFiles.Save(Path.GetFullPath(file), rig);
            SequenceFiles.Save(file, sequence, new DirectoryContentSource(Path.GetDirectoryName(workspace.FilePath)));
            message = "Exported scene, motion and shared resources as a reusable sequence.";
        }

        public void LoadSequence(string file)
        {
            SequenceDocument sequence = SequenceFiles.Read(file);
            if (sequence.Clips.Length > 1)
            {
                throw new ArgumentException("This editor edits one motion draft at a time; this multi-clip sequence can be played at runtime.");
            }
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WitchMendokusai", "ContentLibrary");
            ContentLibrary library = new ContentLibrary(EditorPrefs.GetString("WM.ContentLibrary.Directory", directory));
            string sceneFile = library.Import(sequence.Scene, new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(file))));
            RigFiles.Save(sceneFile, RigFiles.Read(file, sequence.Scene));
            workspace.Load(sceneFile);
            ShowLoaded();
            if (sequence.Clips.Length == 1)
            {
                motion.RestoreDraft(PoseClipJson.Write(sequence.Clips[0]), 0, false);
                motion.Seek(0);
            }
            WorkspaceRecovery.Clear();
            message = "Opened scene and motion from the sequence.";
        }

        private void DrawSequenceFiles()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AuthoringText.Get("Open sequence...")))
            {
                string file = EditorUtility.OpenFilePanel(AuthoringText.Get("Open scene sequence"), "", "json");
                if (string.IsNullOrEmpty(file) == false &&
                    (workspace.IsLoaded == false || (workspace.HasChanges == false && motion.HasChanges == false) ||
                    EditorUtility.DisplayDialog(AuthoringText.Get("Unsaved workspace"), AuthoringText.Get("Discard current scene and motion edits?"), AuthoringText.Get("Discard"), AuthoringText.Get("Cancel"))))
                {
                    Run(delegate { LoadSequence(file); });
                }
            }
            using (new EditorGUI.DisabledScope(workspace.IsLoaded == false))
            {
                if (GUILayout.Button(AuthoringText.Get("Export sequence...")))
                {
                    string file = EditorUtility.SaveFilePanel(AuthoringText.Get("Export scene sequence and resources"), "", "scene.sequence", "json");
                    if (string.IsNullOrEmpty(file) == false)
                    {
                        Run(delegate { SaveSequence(file); });
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
