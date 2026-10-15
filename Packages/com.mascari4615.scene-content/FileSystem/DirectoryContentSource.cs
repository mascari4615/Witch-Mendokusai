using System;
using System.IO;
using System.Security.Cryptography;

namespace SceneContent.Storage
{
    public sealed class DirectoryContentSource : IContentSource
    {
        private const int MAX_RESOURCE_BYTES = 268435456;
        private readonly string root;

        public DirectoryContentSource(string directory)
        {
            root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public byte[] Read(ContentResource resource)
        {
            if (resource == null) throw new ArgumentNullException("resource");
            string path = ContentPaths.Resolve(root, resource.Path);
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > MAX_RESOURCE_BYTES) throw new IOException("Resource exceeds size limit");
                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                using (SHA256 hash = SHA256.Create())
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    if (actual != resource.Sha256) throw new InvalidDataException("Resource checksum mismatch");
                }
                return bytes;
            }
        }
    }
}
