using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SceneContent.Serialization;

namespace SceneContent.Storage
{
    public sealed class ContentLibrary
    {
        public string Root { get; private set; }

        public ContentLibrary(string directory)
        {
            Root = Path.GetFullPath(directory);
        }

        public string Import(SceneDocument document, IContentSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            SceneDocument copy = SceneCloning.Copy(document);
            Dictionary<string, ContentResource> originals = new Dictionary<string, ContentResource>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Resources.Length; index++)
            {
                ContentResource resource = copy.Resources[index];
                originals.Add(resource.Id, document.Resources[index]);
                resource.Path = "objects/" + resource.Sha256.Substring(0, 2) + "/" + resource.Sha256 + ".blob";
            }
            string key;
            using (SHA256 hash = SHA256.Create())
            {
                key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(SceneJson.Write(copy)))).Replace("-", "").ToLowerInvariant();
            }
            Directory.CreateDirectory(Root);
            string file = ContentPaths.Resolve(Root, key + ".scene.json");
            SceneFiles.Save(file, copy, new OriginalSource(source, originals));
            return file;
        }

        public string[] Entries()
        {
            if (Directory.Exists(Root) == false)
            {
                return new string[0];
            }
            string[] files = Directory.GetFiles(Root, "*.scene.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public LibraryUsage Usage()
        {
            long bytes = 0;
            int objects = 0;
            string directory = Path.Combine(Root, "objects");
            if (Directory.Exists(directory))
            {
                ContentPaths.Resolve(Root, "objects");
                foreach (string folder in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    ContentPaths.Resolve(Root, "objects/" + Path.GetFileName(folder));
                    foreach (string file in Directory.GetFiles(folder, "*.blob", SearchOption.TopDirectoryOnly))
                    {
                        string relative = file.Substring(Root.Length).TrimStart(Path.DirectorySeparatorChar);
                        bytes += new FileInfo(ContentPaths.Resolve(Root, relative)).Length;
                        objects++;
                    }
                }
            }
            return new LibraryUsage { Documents = Entries().Length, Objects = objects, ResourceBytes = bytes };
        }

        private sealed class OriginalSource : IContentSource
        {
            private readonly IContentSource source;
            private readonly Dictionary<string, ContentResource> descriptors;

            public OriginalSource(IContentSource input, Dictionary<string, ContentResource> originals)
            {
                source = input;
                descriptors = originals;
            }

            public byte[] Read(ContentResource resource)
            {
                return source.Read(descriptors[resource.Id]);
            }
        }
    }

    public struct LibraryUsage
    {
        public int Documents;
        public int Objects;
        public long ResourceBytes;
    }
}
