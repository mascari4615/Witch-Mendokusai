using System;
using System.Collections.Generic;

namespace History
{
    public sealed class SnapshotHistory<T>
    {
        private readonly Func<T, string> serialize;
        private readonly Func<string, T> deserialize;
        private readonly int maximumSteps;
        private readonly int maximumCharacters;
        private readonly List<string> undo = new List<string>();
        private readonly List<string> redo = new List<string>();
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public int StoredSteps { get { return undo.Count + redo.Count; } }
        public long StoredCharacters { get; private set; }

        public SnapshotHistory(Func<T, string> encode, Func<string, T> decode, int maxSteps = 100, int maxCharacters = 32000000)
        {
            if (encode == null || decode == null || maxSteps < 1 || maxCharacters < 1)
            {
                throw new ArgumentException("History requires serialization and positive capacity");
            }
            serialize = encode;
            deserialize = decode;
            maximumSteps = maxSteps;
            maximumCharacters = maxCharacters;
        }

        public void Record(T previous)
        {
            RecordSnapshot(Encode(previous));
        }

        public void Execute(T previous, Action change)
        {
            if (change == null)
            {
                throw new ArgumentNullException("change");
            }
            string snapshot = Encode(previous);
            change();
            RecordSnapshot(snapshot);
        }

        public T Undo(T current, Action<T> apply = null)
        {
            return Move(undo, redo, current, apply);
        }

        public T Redo(T current, Action<T> apply = null)
        {
            return Move(redo, undo, current, apply);
        }

        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            StoredCharacters = 0;
        }

        private void RecordSnapshot(string snapshot)
        {
            foreach (string value in redo)
            {
                StoredCharacters -= value.Length;
            }
            redo.Clear();
            Add(undo, snapshot);
        }

        private T Move(List<string> from, List<string> to, T current, Action<T> apply)
        {
            if (from.Count == 0)
            {
                return current;
            }
            string snapshot = Encode(current);
            string candidate = from[from.Count - 1];
            T result = deserialize(candidate);
            if (apply != null)
            {
                apply(result);
            }
            from.RemoveAt(from.Count - 1);
            StoredCharacters -= candidate.Length;
            Add(to, snapshot);
            return result;
        }

        private string Encode(T value)
        {
            string snapshot = serialize(value);
            if (snapshot == null || snapshot.Length > maximumCharacters)
            {
                throw new ArgumentException("Snapshot exceeds history capacity");
            }
            return snapshot;
        }

        private void Add(List<string> destination, string snapshot)
        {
            if (destination.Count == 0 || string.Equals(destination[destination.Count - 1], snapshot, StringComparison.Ordinal) == false)
            {
                destination.Add(snapshot);
                StoredCharacters += snapshot.Length;
            }
            while (StoredSteps > maximumSteps || StoredCharacters > maximumCharacters)
            {
                List<string> oldest = destination.Count > 1 ? destination : destination == undo ? redo : undo;
                StoredCharacters -= oldest[0].Length;
                oldest.RemoveAt(0);
            }
        }
    }
}
