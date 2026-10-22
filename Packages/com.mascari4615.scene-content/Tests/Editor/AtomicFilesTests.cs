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
