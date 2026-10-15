using System.Collections.Generic;
using Motion;
using Motion.Serialization;

namespace Motion.Serialization
{
    public sealed class MotionDraftHistory
    {
        private const int MAX_STEPS = 100;
        private readonly List<string> undo = new List<string>();
        private readonly List<string> redo = new List<string>();
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public void Record(PoseClip previous)
        {
            Add(undo, PoseClipJson.Write(previous));
            redo.Clear();
        }

        public PoseClip Undo(PoseClip current)
        {
            return Move(undo, redo, current);
        }

        public PoseClip Redo(PoseClip current)
        {
            return Move(redo, undo, current);
        }

        public void Clear()
        {
            undo.Clear();
            redo.Clear();
        }

        private static PoseClip Move(List<string> from, List<string> to, PoseClip current)
        {
            if (from.Count == 0) return current;
            PoseClip result = PoseClipJson.Read(from[from.Count - 1]);
            Add(to, PoseClipJson.Write(current));
            from.RemoveAt(from.Count - 1);
            return result;
        }

        private static void Add(List<string> history, string value)
        {
            if (history.Count == 0 || history[history.Count - 1] != value) history.Add(value);
            if (history.Count > MAX_STEPS) history.RemoveAt(0);
        }
    }
}
