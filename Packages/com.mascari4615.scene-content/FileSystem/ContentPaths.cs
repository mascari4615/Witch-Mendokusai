using System;
using System.IO;

namespace SceneContent.Storage
{
    public static class ContentPaths
    {
        public static string Resolve(string directory, string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative))
            {
                throw new ArgumentException("Content path must be relative");
            }
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (path.StartsWith(root, comparison) == false)
            {
                throw new ArgumentException("Content escaped its directory");
            }
            string current = Path.GetPathRoot(path);
            foreach (string component in path.Substring(current.Length).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, component);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked content paths are not supported");
                }
            }
            return path;
        }
    }
}
