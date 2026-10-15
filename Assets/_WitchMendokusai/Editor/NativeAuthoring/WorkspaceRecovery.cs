using System;
using System.IO;
using SceneContent.Serialization;
using SceneContent.Storage;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    [Serializable]
    public sealed class WorkspaceDraft
    {
        public int Version = 1;
        public string SourceFile;
        public string Scene;
        public string Clip;
        public float Time;
        public bool ClipChanged;
        public string SavedAt;
    }

    public static class WorkspaceRecovery
    {
        private const int MAX_DRAFT_BYTES = 96000000;
        private static string DraftFile
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ContentAuthoring/recovery.json")); }
        }

        public static bool Exists { get { return File.Exists(DraftFile); } }

        public static void Write(ContentWorkspace workspace, MotionClipPanel motion)
        {
            if (workspace.IsLoaded == false)
            {
                return;
            }
            WorkspaceDraft draft = new WorkspaceDraft
            {
                SourceFile = workspace.FilePath,
                Scene = SceneJson.Write(SceneSnapshot.Capture(workspace.Document, workspace.Instance)),
                Clip = motion.ExportDraft(), Time = motion.Time, ClipChanged = motion.HasChanges,
                SavedAt = DateTime.UtcNow.ToString("o")
            };
            string directory = Path.GetDirectoryName(DraftFile);
            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(draft, true);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_DRAFT_BYTES)
            {
                throw new IOException("Workspace recovery exceeds size limit");
            }
            AtomicFiles.WriteText(DraftFile, json);
        }

        public static WorkspaceDraft Read()
        {
            if (new FileInfo(DraftFile).Length > MAX_DRAFT_BYTES)
            {
                throw new IOException("Workspace recovery exceeds size limit");
            }
            WorkspaceDraft draft = JsonUtility.FromJson<WorkspaceDraft>(File.ReadAllText(DraftFile));
            if (draft == null || draft.Version != 1 || string.IsNullOrEmpty(draft.SourceFile) || File.Exists(draft.SourceFile) == false)
            {
                throw new IOException("Recovery source is unavailable; the draft has been kept");
            }
            SceneJson.Read(draft.Scene);
            Motion.Serialization.PoseClipJson.Read(draft.Clip);
            if (float.IsNaN(draft.Time) || float.IsInfinity(draft.Time))
            {
                throw new IOException("Invalid recovery clip time");
            }
            return draft;
        }

        public static void Clear()
        {
            if (File.Exists(DraftFile))
            {
                File.Delete(DraftFile);
            }
        }
    }
}
