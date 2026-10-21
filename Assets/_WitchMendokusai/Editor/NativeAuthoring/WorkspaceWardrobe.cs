using System;
using System.Collections.Generic;
using System.IO;
using Rigging.Serialization;
using SceneContent;
using SceneContent.Storage;
using SceneContent.Unity;
using Wardrobe;
using Wardrobe.Serialization;

namespace WitchMendokusai.ContentAuthoring
{
    public static class WorkspaceWardrobe
    {
        // The characters that have body role bindings: an empty name for the first, then each prefix such as "actor-2".
        public static string[] Actors(ContentWorkspace workspace)
        {
            if (cachedFile == workspace.FilePath && cachedActors != null) return cachedActors;
            string rigJson = RigFiles.Read(workspace.FilePath, workspace.Document);
            List<string> actors = new List<string>();
            if (rigJson == null) return Remember(workspace.FilePath, actors);
            foreach (Rigging.RigJoint joint in RigJson.Read(rigJson).Joints)
            {
                int separator = joint.Role.IndexOf(':');
                string actor = separator < 0 ? "" : joint.Role.Substring(0, separator);
                if (actors.Contains(actor) == false) actors.Add(actor);
            }
            return Remember(workspace.FilePath, actors);
        }

        // The window repaints often and a scene file never changes under its path, so the answer is kept per path.
        private static string cachedFile;
        private static string[] cachedActors;

        private static string[] Remember(string file, List<string> actors)
        {
            cachedFile = file;
            cachedActors = actors.ToArray();
            return cachedActors;
        }

        // The outfit of the wardrobe that a character in the loaded scene wears, or null when it cannot be told.
        public static string Wearing(ContentWorkspace workspace, WardrobeDocument wardrobe, string actor)
        {
            return WardrobeApplication.Wearing(SceneSnapshot.Capture(workspace.Document, workspace.Instance), wardrobe, actor);
        }

        // Returns a new library scene file for the dressed character. A null outfit takes everything off.
        // Edits already made to the scene are kept, the way adding an actor keeps them.
        public static string Dress(ContentWorkspace workspace, string wardrobeFile, string outfitId, string actor = "")
        {
            SceneDocument current = SceneSnapshot.Capture(workspace.Document, workspace.Instance);
            string rigJson = RigFiles.Read(workspace.FilePath, current);
            if (rigJson == null) throw new InvalidOperationException("This scene has no body role bindings, so parts cannot be attached");
            WardrobeDocument wardrobe = WardrobeJson.Read(File.ReadAllText(wardrobeFile));
            SceneDocument dressed = outfitId == null
                ? WardrobeApplication.Undress(current, wardrobe, actor)
                : WardrobeApplication.Dress(current, RigJson.Read(rigJson), wardrobe, outfitId, actor);
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WitchMendokusai", "ContentLibrary");
            ContentLibrary library = new ContentLibrary(UnityEditor.EditorPrefs.GetString("WM.ContentLibrary.Directory", directory));
            IContentSource source = new FallbackContentSource(
                new DirectoryContentSource(Path.GetDirectoryName(workspace.FilePath)),
                new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(wardrobeFile))));
            string file = library.Import(dressed, source);
            RigFiles.Save(file, rigJson);
            return file;
        }

        // Reads from the first source that has the file. A file that exists but fails its checksum is an error, not a reason to try the next.
        private sealed class FallbackContentSource : IContentSource
        {
            private readonly IContentSource[] sources;

            public FallbackContentSource(params IContentSource[] sources)
            {
                this.sources = sources;
            }

            public byte[] Read(ContentResource resource)
            {
                foreach (IContentSource source in sources)
                {
                    try
                    {
                        return source.Read(resource);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    catch (DirectoryNotFoundException)
                    {
                    }
                }
                throw new FileNotFoundException("Resource is in none of the sources: " + resource.Path);
            }
        }
    }
}
