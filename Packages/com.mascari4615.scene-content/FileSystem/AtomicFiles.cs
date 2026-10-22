using System;
using System.IO;
using System.Text;

namespace SceneContent.Storage
{
    public static class AtomicFiles
    {
        // Another process (a scanner, an indexer) can hold the destination open for a moment, and File.Replace then fails
        // with "unable to remove the file to be replaced". A short bounded wait clears it without hiding a lasting lock.
        private const int REPLACE_ATTEMPTS = 6;
        private const int REPLACE_WAIT_MILLISECONDS = 50;

        private static void ReplaceWithRetry(string temporary, string destination)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Replace(temporary, destination, null);
                    return;
                }
                catch (Exception exception) when (attempt < REPLACE_ATTEMPTS && (exception is IOException || exception is UnauthorizedAccessException))
                {
                    System.Threading.Thread.Sleep(REPLACE_WAIT_MILLISECONDS * attempt);
                }
            }
        }

        public static void WriteText(string file, string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            string destination = Path.GetFullPath(file);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(destination))
                {
                    ReplaceWithRetry(temporary, destination);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
