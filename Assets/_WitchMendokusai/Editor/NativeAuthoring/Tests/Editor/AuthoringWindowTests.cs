using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SceneContent.Serialization;
using SceneContent.Storage;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.ContentAuthoring.Tests
{
    public sealed partial class AuthoringWindowTests
    {
        [Test]
        public void SurfaceEditingPreservesTextureWrapThroughHistory()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => { scene.Version = 2; scene.Materials[0].TextureWrap = "clamp"; });
                SceneContent.SurfaceMaterial edited = SceneContent.SceneCloning.CopySurface(window.Workspace.Document.Materials[0]);
                edited.BaseColor[0] = .25f;
                window.Surfaces.Apply(0, edited);
                Assert.That(window.Workspace.Document.Materials[0].TextureWrap, Is.EqualTo("clamp"));
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Materials[0].TextureWrap, Is.EqualTo("clamp"));
                window.RedoSceneChange();
                Assert.That(window.Workspace.Document.Materials[0].TextureWrap, Is.EqualTo("clamp"));
            }
        }

        [Test]
        public void InvalidSequenceScalarTargetKeepsCurrentWorkspaceAndRecovery()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => scene.Nodes[0].Label = "first");
                window.ChangeScene(scene => scene.Nodes[0].Label = "second");
                window.UndoSceneChange();
                window.Motion.Record("root");
                string firstClip = window.Motion.ExportDraft();
                window.Motion.Seek(.5f);
                window.Motion.Record("root");
                string secondClip = window.Motion.ExportDraft();
                window.Motion.UndoEdit();
                string clip = window.Motion.ExportDraft();
                float seconds = window.Motion.Time;
                bool changed = window.Motion.HasChanges;
                UnityEngine.Object instance = window.Workspace.Instance;
                string originalScene = SceneJson.Write(window.Workspace.Document);
                window.SaveRecovery();
                string recoveryFile = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ContentAuthoring/recovery.json"));
                string recovery = File.ReadAllText(recoveryFile);
                string file = Path.Combine(Path.GetDirectoryName(fixture.OtherFile), "test.sequence.json");
                string sequencePrefix = "{\"Format\":\"scene-sequence\",\"Version\":1,\"Id\":\"test-sequence\",\"Scene\":" + SceneJson.Write(SceneFiles.Read(fixture.OtherFile)) + ",\"Clips\":[";
                string invalidClip = "{\"Format\":\"pose-clip\",\"Version\":1,\"Id\":\"invalid-scalar\",\"Duration\":3,\"Loop\":false,\"Tracks\":[],\"Values\":[{\"Target\":\"camera:missing:fov\",\"Keys\":[{\"Time\":0,\"Value\":30}]}]}";
                string sequence = sequencePrefix + invalidClip + "]}";
                File.WriteAllText(file, sequence);
                KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => window.LoadSequence(file));
                Assert.That(exception.Message, Does.Contain("camera:missing:fov"));
                Assert.That(window.Workspace.Instance, Is.SameAs(instance));
                Assert.That(window.Workspace.FilePath, Is.EqualTo(fixture.ActorFile));
                Assert.That(SceneJson.Write(window.Workspace.Document), Is.EqualTo(originalScene));
                Assert.That(window.Workspace.CanUndoChange, Is.True);
                Assert.That(window.Workspace.CanRedoChange, Is.True);
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(clip));
                Assert.That(window.Motion.Time, Is.EqualTo(seconds));
                Assert.That(window.Motion.HasChanges, Is.EqualTo(changed));
                Assert.That(File.ReadAllText(recoveryFile), Is.EqualTo(recovery));
                Assert.That(File.ReadAllText(file), Is.EqualTo(sequence));
                window.Motion.RedoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(firstClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.Not.EqualTo(firstClip));
                window.RedoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("second"));
                window.UndoSceneChange();
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("initial"));
                File.WriteAllText(file, sequencePrefix + secondClip + "]}");
                window.LoadSequence(file);
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                Assert.That(window.Motion.Time, Is.EqualTo(0));
                Assert.That(window.Motion.HasChanges, Is.False);
                Assert.That(window.Workspace.CanUndoChange, Is.False);
                Assert.That(window.Workspace.CanRedoChange, Is.False);
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                Assert.That(WorkspaceRecovery.Exists, Is.False);
                fixture.CheckResource(window.Workspace, "other");
            }
        }

        [Test]
        public void AddingAnActorPreservesMotionHistoryAndStartsSceneHistory()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => scene.Nodes[0].Label = "first");
                window.ChangeScene(scene => scene.Nodes[0].Label = "second");
                window.UndoSceneChange();
                window.Motion.Record("root");
                string firstClip = window.Motion.ExportDraft();
                window.Motion.Seek(.5f);
                window.Workspace.Instance.FindNode("root").localPosition = new Vector3(2, 0, 0);
                window.Motion.Record("root");
                string secondClip = window.Motion.ExportDraft();
                window.Motion.UndoEdit();
                string clip = window.Motion.ExportDraft();
                float seconds = window.Motion.Time;
                bool changed = window.Motion.HasChanges;
                window.AddActor(fixture.OtherFile);
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(3));
                Assert.That(window.Workspace.Instance.FindNode("actor-2:root"), Is.Not.Null);
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("first"));
                Assert.That(window.Workspace.CanUndoChange, Is.False);
                Assert.That(window.Workspace.CanRedoChange, Is.False);
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(clip));
                Assert.That(window.Motion.Time, Is.EqualTo(seconds));
                Assert.That(window.Motion.HasChanges, Is.EqualTo(changed));
                window.Motion.RedoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(firstClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.Not.EqualTo(firstClip));
                fixture.CheckResource(window.Workspace, "actor");
                Assert.That(File.ReadAllText(fixture.ActorFile), Is.EqualTo(fixture.ActorOriginal));
                Assert.That(File.ReadAllText(fixture.ActorRig), Is.EqualTo(fixture.RigOriginal));
            }
        }

        [Test]
        public void AddingAnActorWithInvalidMotionKeepsCurrentWorkspaceAndHistory()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => scene.Nodes[0].Label = "first");
                window.ChangeScene(scene => scene.Nodes[0].Label = "second");
                window.UndoSceneChange();
                window.Motion.Record("root");
                string firstClip = window.Motion.ExportDraft();
                window.Motion.Seek(.5f);
                window.Motion.Record("root");
                string secondClip = window.Motion.ExportDraft();
                window.Motion.UndoEdit();
                // Simulate a caller mutating the exposed clip without adding a test assembly dependency.
                object clip = typeof(MotionClipPanel).GetField("clip", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window.Motion);
                Array tracks = (Array)clip.GetType().GetField("Tracks").GetValue(clip);
                object track = tracks.GetValue(0);
                FieldInfo node = track.GetType().GetField("Node");
                node.SetValue(track, "missing");
                string invalidClip = window.Motion.ExportDraft();
                string originalScene = SceneJson.Write(window.Workspace.Document);
                UnityEngine.Object instance = window.Workspace.Instance;
                float seconds = window.Motion.Time;
                bool changed = window.Motion.HasChanges;
                Assert.Throws<InvalidOperationException>(() => window.AddActor(fixture.OtherFile));
                Assert.That(window.Workspace.Instance, Is.SameAs(instance));
                Assert.That(window.Workspace.FilePath, Is.EqualTo(fixture.ActorFile));
                Assert.That(SceneJson.Write(window.Workspace.Document), Is.EqualTo(originalScene));
                Assert.That(window.Workspace.CanUndoChange, Is.True);
                Assert.That(window.Workspace.CanRedoChange, Is.True);
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(invalidClip));
                Assert.That(window.Motion.Time, Is.EqualTo(seconds));
                Assert.That(window.Motion.HasChanges, Is.EqualTo(changed));
                node.SetValue(track, "root");
                window.Motion.RedoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(firstClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.Not.EqualTo(firstClip));
                window.RedoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("second"));
                window.UndoSceneChange();
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("initial"));
                fixture.CheckResource(window.Workspace, "actor");
            }
        }

        [Test]
        public void FailedRecoveryKeepsSceneMotionHistoryAndDraft()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => scene.Nodes[0].Label = "first");
                window.ChangeScene(scene => scene.Nodes[0].Label = "second");
                window.UndoSceneChange();
                window.Motion.Record("root");
                string firstClip = window.Motion.ExportDraft();
                window.Motion.Seek(.5f);
                window.Motion.Record("root");
                string secondClip = window.Motion.ExportDraft();
                window.Motion.UndoEdit();
                string clip = window.Motion.ExportDraft();
                float seconds = window.Motion.Time;
                UnityEngine.Object instance = window.Workspace.Instance;
                string originalScene = SceneJson.Write(window.Workspace.Document);
                string recoveryFile = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ContentAuthoring/recovery.json"));
                WorkspaceDraft draft = new WorkspaceDraft
                {
                    SourceFile = fixture.OtherFile, Scene = SceneJson.Write(SceneFiles.Read(fixture.OtherFile)),
                    Clip = secondClip.Replace("\"root\"", "\"missing\""), Time = .5f, ClipChanged = true
                };
                Assert.That(draft.Clip, Is.Not.EqualTo(secondClip));
                Directory.CreateDirectory(Path.GetDirectoryName(recoveryFile));
                string original = JsonUtility.ToJson(draft, true);
                File.WriteAllText(recoveryFile, original);
                Assert.Catch(() => window.RestoreRecovery());
                Assert.That(window.Workspace.Instance, Is.SameAs(instance));
                Assert.That(window.Workspace.FilePath, Is.EqualTo(fixture.ActorFile));
                Assert.That(SceneJson.Write(window.Workspace.Document), Is.EqualTo(originalScene));
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(clip));
                Assert.That(window.Motion.Time, Is.EqualTo(seconds));
                Assert.That(File.ReadAllText(recoveryFile), Is.EqualTo(original));
                Assert.That(window.Workspace.CanUndoChange, Is.True);
                Assert.That(window.Workspace.CanRedoChange, Is.True);
                window.Motion.RedoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(firstClip));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.Not.EqualTo(firstClip));
                window.RedoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("second"));
                window.UndoSceneChange();
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("initial"));
                draft.Clip = secondClip;
                File.WriteAllText(recoveryFile, JsonUtility.ToJson(draft, true));
                window.RestoreRecovery();
                Assert.That(window.Workspace.FilePath, Is.EqualTo(fixture.OtherFile));
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                Assert.That(window.Motion.Time, Is.EqualTo(.5f));
                Assert.That(window.Workspace.CanUndoChange, Is.False);
                Assert.That(window.Workspace.CanRedoChange, Is.False);
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(secondClip));
                fixture.CheckResource(window.Workspace, "other");
            }
        }

        [Test]
        public void OutfitsPreserveSceneAndClipHistoryAcrossUndoRedoAndSave()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.ChangeScene(scene => { scene.Nodes[0].Label = "edited"; scene.Materials[0].BaseColor[0] = .3f; });
                window.Motion.Record("root");
                string firstClip = window.Motion.ExportDraft();
                window.Motion.Seek(.5f);
                window.Workspace.Instance.FindNode("root").localPosition = new Vector3(2, 0, 0);
                window.Motion.Record("root");
                string clip = window.Motion.ExportDraft();
                window.Wardrobe.Open(fixture.OutfitFile);
                window.WearOutfit("outfit");
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(2));
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(clip));
                Assert.That(window.Motion.Time, Is.EqualTo(.5f));
                window.Motion.UndoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(firstClip));
                window.Motion.RedoEdit();
                Assert.That(window.Motion.ExportDraft(), Is.EqualTo(clip));
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(1));
                Assert.That(window.Workspace.FilePath, Is.EqualTo(fixture.ActorFile));
                fixture.CheckResource(window.Workspace, "actor");
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes[0].Label, Is.EqualTo("initial"));
                Assert.That(window.Workspace.Document.Materials[0].BaseColor[0], Is.EqualTo(1f));
                window.RedoSceneChange();
                window.RedoSceneChange();
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(2));
                window.WearOutfit(null);
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(1));
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(2));
                string saved = Path.Combine(fixture.Root, "saved", "actor.scene.json");
                Directory.CreateDirectory(Path.GetDirectoryName(saved));
                window.Workspace.Save(saved);
                Assert.That(window.Workspace.HasChanges, Is.False);
                window.UndoSceneChange();
                Assert.That(window.Workspace.HasChanges, Is.True);
                fixture.CheckResource(window.Workspace, "actor");
                window.RedoSceneChange();
                Assert.That(window.Workspace.FilePath, Is.EqualTo(saved));
                Assert.That(window.Workspace.HasChanges, Is.False);
                using (ContentWorkspace reopened = new ContentWorkspace())
                {
                    reopened.Load(saved);
                    Assert.That(reopened.Document.Nodes.Length, Is.EqualTo(2));
                    fixture.CheckResource(reopened, "actor");
                    Assert.That(RigFiles.Read(saved, reopened.Document), Is.EqualTo(RigFiles.Read(fixture.ActorFile, reopened.Document)));
                }
                Assert.That(File.ReadAllText(fixture.ActorFile), Is.EqualTo(fixture.ActorOriginal));
                Assert.That(File.ReadAllText(fixture.ActorRig), Is.EqualTo(fixture.RigOriginal));
            }
        }

        [Test]
        public void FailedFileHistoryRestorationKeepsPreviewAndBothStacks()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            using (ContentWorkspace workspace = new ContentWorkspace())
            {
                workspace.Load(fixture.ActorFile);
                workspace.ChangeFile(fixture.OtherFile);
                UnityEngine.Object instance = workspace.Instance;
                string document = SceneJson.Write(workspace.Document);
                string currentRig = RigFiles.Find(fixture.OtherFile);
                string bytes = File.ReadAllText(currentRig);
                File.AppendAllText(currentRig, " ");
                Assert.Throws<IOException>(() => workspace.UndoChange());
                File.WriteAllText(currentRig, bytes);
                File.AppendAllText(fixture.ActorRig, " ");
                Assert.Throws<IOException>(() => workspace.UndoChange());
                File.WriteAllText(fixture.ActorRig, fixture.RigOriginal);
                Assert.Throws<InvalidOperationException>(() => workspace.UndoChange((scene, candidate) => { throw new InvalidOperationException("Rejected staging"); }));
                Assert.That(workspace.Instance, Is.SameAs(instance));
                Assert.That(SceneJson.Write(workspace.Document), Is.EqualTo(document));
                Assert.That(workspace.FilePath, Is.EqualTo(fixture.OtherFile));
                Assert.That(workspace.CanUndoChange, Is.True);
                Assert.That(workspace.CanRedoChange, Is.False);
                fixture.CheckResource(workspace, "other");
                workspace.UndoChange();
                Assert.That(workspace.FilePath, Is.EqualTo(fixture.ActorFile));
                fixture.CheckResource(workspace, "actor");
                workspace.RedoChange();
                fixture.CheckResource(workspace, "other");
                workspace.Load(fixture.ActorFile);
                Assert.That(workspace.CanUndoChange, Is.False);
            }
        }

        [Test]
        public void OutfitUndoCannotRemoveAnAuthoredMotionTarget()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            {
                ContentAuthoringWindow window = fixture.OpenWindow();
                window.Wardrobe.Open(fixture.OutfitFile);
                window.WearOutfit("outfit");
                window.Motion.Record("part:part-root");
                UnityEngine.Object instance = window.Workspace.Instance;
                Assert.Throws<InvalidOperationException>(() => window.UndoSceneChange());
                Assert.That(window.Workspace.Instance, Is.SameAs(instance));
                Assert.That(window.Workspace.CanUndoChange, Is.True);
                Assert.That(window.Workspace.CanRedoChange, Is.False);
                window.Motion.UndoEdit();
                window.UndoSceneChange();
                Assert.That(window.Workspace.Document.Nodes.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void SavingWithTheSameFallbackRigDoesNotInvalidateHistory()
        {
            using (WorkspaceFixture fixture = new WorkspaceFixture())
            using (ContentWorkspace workspace = new ContentWorkspace())
            {
                string fallback = Path.Combine(Path.GetDirectoryName(fixture.ActorFile), "rig.json");
                File.Move(fixture.ActorRig, fallback);
                workspace.Load(fixture.ActorFile);
                workspace.Change(scene => scene.Nodes[0].Label = "edited");
                workspace.Save(fixture.ActorFile);
                Assert.That(File.Exists(fixture.ActorRig), Is.False);
                Assert.That(File.ReadAllText(fallback), Is.EqualTo(fixture.RigOriginal));
                workspace.UndoChange();
                Assert.That(workspace.Document.Nodes[0].Label, Is.EqualTo("initial"));
                Assert.That(workspace.HasChanges, Is.True);
            }
        }

        [Test]
        public void ClosingWorkspaceReleasesCachedWardrobeThumbnails()
        {
            ContentAuthoringWindow window = ScriptableObject.CreateInstance<ContentAuthoringWindow>();
            Texture2D thumbnail = new Texture2D(2, 2);
            thumbnail.hideFlags = HideFlags.HideAndDontSave;
            WardrobePanel wardrobe = window.Wardrobe;
            FieldInfo thumbnailsField = typeof(WardrobePanel).GetField("thumbs", BindingFlags.Instance | BindingFlags.NonPublic);
            Dictionary<string, Texture2D> thumbnails = (Dictionary<string, Texture2D>)thumbnailsField.GetValue(wardrobe);
            try
            {
                thumbnails.Add("outfit", thumbnail);
                thumbnails.Add("missing", null);
                UnityEngine.Object.DestroyImmediate(window);
                Assert.That(thumbnail == null, Is.True);
                Assert.That(thumbnails, Is.Empty);
                Assert.DoesNotThrow(() => ((IDisposable)wardrobe).Dispose());
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                if (thumbnail != null) UnityEngine.Object.DestroyImmediate(thumbnail);
            }
        }

        [Test]
        public void WindowConstructionInitializesLocalizedStateAfterUnityCreatesTheWindow()
        {
            ContentAuthoringWindow window = ScriptableObject.CreateInstance<ContentAuthoringWindow>();
            try
            {
                Assert.That(window.maxSize.x, Is.GreaterThanOrEqualTo(window.minSize.x));
                Assert.That(window.maxSize.y, Is.GreaterThanOrEqualTo(window.minSize.y));
                Assert.That(new SerializedObject(window).FindProperty("libraryTitle").stringValue, Is.Not.Empty);
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

    }
}
