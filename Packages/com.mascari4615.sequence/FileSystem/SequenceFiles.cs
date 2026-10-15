using System;
using System.IO;
using SceneContent;
using SceneContent.Storage;
using SceneSequence.Serialization;

namespace SceneSequence.Storage
{
    public static class SequenceFiles
    {
        public static SequenceDocument Read(string file)
        {
            string path = Path.GetFullPath(file);
            FileInfo info = new FileInfo(path);
            if (info.Length > SequenceJson.MAX_JSON_CHARACTERS * 4L)
            {
                throw new ArgumentException("Sequence file exceeds size limit");
            }
            SequenceDocument document = SequenceJson.Read(File.ReadAllText(path));
            DirectoryContentSource source = new DirectoryContentSource(Path.GetDirectoryName(path));
            foreach (ContentResource resource in document.Scene.Resources)
            {
                source.Read(resource);
            }
            return document;
        }

        public static void Save(string file, SequenceDocument document, IContentSource source)
        {
            SequenceValidation.Ensure(document);
            string path = Path.GetFullPath(file);
            if (path.EndsWith(".sequence.json", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new ArgumentException("Sequence exports must end in .sequence.json");
            }
            SequenceJson.Write(document);
            ContentLibrary library = new ContentLibrary(Path.GetDirectoryName(path));
            string sceneFile = library.Import(document.Scene, source);
            SequenceDocument copy = new SequenceDocument
            {
                Id = document.Id, Scene = SceneFiles.Read(sceneFile), Clips = document.Clips
            };
            string target = ContentPaths.Resolve(library.Root, Path.GetFileName(path));
            AtomicFiles.WriteText(target, SequenceJson.Write(copy));
        }
    }
}
