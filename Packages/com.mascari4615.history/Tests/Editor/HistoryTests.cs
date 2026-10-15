using System;
using NUnit.Framework;

namespace History.Tests
{
    public sealed class HistoryTests
    {
        [Test]
        public void MutableInputsAreSnapshottedAndNewEditsDiscardRedo()
        {
            SnapshotHistory<Value> history = Create();
            Value current = new Value { Text = "before" };
            history.Record(current);
            current.Text = "after";
            Value previous = history.Undo(current);
            Assert.That(previous.Text, Is.EqualTo("before"));
            Assert.That(history.Redo(previous).Text, Is.EqualTo("after"));
            history.Undo(current);
            history.Record(previous);
            Assert.That(history.CanRedo, Is.False);
        }

        [Test]
        public void FailedApplicationLeavesBothHistoryStacksIntact()
        {
            SnapshotHistory<Value> history = Create();
            history.Record(new Value { Text = "before" });
            Assert.Throws<InvalidOperationException>(() => history.Undo(new Value { Text = "after" }, value => { throw new InvalidOperationException(); }));
            Assert.That(history.CanUndo, Is.True);
            Assert.That(history.CanRedo, Is.False);
            Assert.That(history.StoredSteps, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => history.Execute(new Value { Text = "before" }, () => { throw new InvalidOperationException(); }));
            Assert.That(history.StoredSteps, Is.EqualTo(1));
            Assert.That(history.Undo(new Value { Text = "after" }).Text, Is.EqualTo("before"));
        }

        [Test]
        public void CapacityAppliesAcrossUndoAndRedoAndRejectsOversizedSnapshotBeforeChange()
        {
            SnapshotHistory<Value> history = Create(2, 10);
            history.Record(new Value { Text = "1111" });
            history.Record(new Value { Text = "2222" });
            history.Record(new Value { Text = "3333" });
            Assert.That(history.StoredSteps, Is.EqualTo(2));
            Value current = history.Undo(new Value { Text = "4444" });
            Assert.That(current.Text, Is.EqualTo("3333"));
            Assert.That(history.StoredCharacters, Is.LessThanOrEqualTo(10));
            Assert.That(history.Redo(current).Text, Is.EqualTo("4444"));
            bool changed = false;
            Assert.Throws<ArgumentException>(() => history.Execute(new Value { Text = "too-large-snapshot" }, () => changed = true));
            Assert.That(changed, Is.False);
            history.Clear();
            Assert.That(history.StoredSteps, Is.EqualTo(0));
            Assert.That(history.StoredCharacters, Is.EqualTo(0));
        }

        private static SnapshotHistory<Value> Create(int steps = 100, int characters = 32000000)
        {
            return new SnapshotHistory<Value>(value => value.Text, text => new Value { Text = text }, steps, characters);
        }

        private sealed class Value
        {
            public string Text;
        }
    }
}
