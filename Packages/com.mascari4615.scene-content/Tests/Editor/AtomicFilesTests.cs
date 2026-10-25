using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SceneContent.Storage;

namespace SceneContent.Tests
{
    public sealed class AtomicFilesTests
    {
        [Test]
        public void ValidDeepWindowsTextPathCanBeCreatedReplacedAndKeptOnLockFailure()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows path-boundary regression");
            string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string root = Path.Combine(temporary, "atomic-deep-" + Guid.NewGuid().ToString("N"));
            string name = new string('a', 64) + ".scene.json";
            int padding = 233 - name.Length - root.Length - 2;
            Assert.That(padding, Is.GreaterThan(0), "The temporary root must leave room for the path-boundary fixture");
            string directory = Path.Combine(root, new string('d', padding));
            string file = Path.Combine(directory, name);
            Assert.That(file.Length, Is.EqualTo(233));
            Assert.That((file + "." + new string('0', 32) + ".tmp").Length, Is.GreaterThanOrEqualTo(260));
            try
            {
                Directory.CreateDirectory(directory);
                AtomicFiles.WriteText(file, "first");
                Assert.That(File.ReadAllText(file), Is.EqualTo("first"));
                AtomicFiles.WriteText(file, "second");
                Assert.That(File.ReadAllText(file), Is.EqualTo("second"));
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.Catch<IOException>(() => AtomicFiles.WriteText(file, "blocked"));
                }
                Assert.That(File.ReadAllText(file), Is.EqualTo("second"));
                Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
            }
            finally
            {
                string target = Path.GetFullPath(root);
                if (target.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) == false || Path.GetFileName(target).StartsWith("atomic-deep-", StringComparison.Ordinal) == false)
                    throw new InvalidOperationException("Unexpected atomic fixture cleanup path");
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
        }

        [Test]
        public void OverwriteWaitsOutAShortLockAndStillFailsOnALastingOne()
        {
            string folder = Path.Combine(Path.GetTempPath(), "atomic-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "document.json");
                AtomicFiles.WriteText(file, "one");

                // A reader that blocks deletion for a moment, as a scanner or indexer does.
                FileStream held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                Thread releaser = new Thread(() =>
                {
                    Thread.Sleep(120);
                    held.Dispose();
                });
                releaser.Start();
                AtomicFiles.WriteText(file, "two");
                releaser.Join();
                Assert.That(File.ReadAllText(file), Is.EqualTo("two"));

                // A lock that never ends must still surface as an error, with the old text kept and no temporary file left.
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.Catch<IOException>(() => AtomicFiles.WriteText(file, "three"));
                }
                Assert.That(File.ReadAllText(file), Is.EqualTo("two"));
                Assert.That(Directory.GetFiles(folder).Length, Is.EqualTo(1));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
