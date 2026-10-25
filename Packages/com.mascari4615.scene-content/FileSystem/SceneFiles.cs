using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SceneContent.Serialization;

namespace SceneContent.Storage
{
    public static class SceneFiles
    {
        public static SceneDocument Read(string file)
        {
            FileInfo info = new FileInfo(file);
            if (info.Length > 32000000)
            {
                throw new IOException("Scene document exceeds size limit");
            }
            return SceneJson.Read(File.ReadAllText(file, Encoding.UTF8));
        }

        public static void Save(string file, SceneDocument document, IContentSource source)
        {
            string json = SceneJson.Write(document);
            string destination = Path.GetFullPath(file);
            string directory = Path.GetDirectoryName(destination);
            if (Directory.Exists(directory) == false)
            {
                throw new DirectoryNotFoundException(directory);
            }
            foreach (ContentResource resource in document.Resources)
            {
                string resourcePath = ContentPaths.Resolve(directory, resource.Path);
                StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(destination, resourcePath, comparison)) throw new IOException("Resource collides with scene manifest");
                byte[] data = source.Read(resource);
                string hash;
                using (SHA256 algorithm = SHA256.Create())
                {
                    hash = BitConverter.ToString(algorithm.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                }
                if (hash != resource.Sha256)
                {
                    throw new IOException("Source resource checksum mismatch");
                }
                if (File.Exists(resourcePath))
                {
                    new DirectoryContentSource(directory).Read(resource);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(resourcePath));
                string staged = Path.Combine(Path.GetDirectoryName(resourcePath), Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllBytes(staged, data);
                    File.Move(staged, resourcePath);
                }
                finally
                {
                    if (File.Exists(staged))
                    {
                        File.Delete(staged);
                    }
                }
            }
            AtomicFiles.WriteText(destination, json);
        }
    }
}
