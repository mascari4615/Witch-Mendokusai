using System;
using System.IO;
using Rigging;
using Rigging.Serialization;
using SceneContent;
using SceneContent.Storage;

namespace WitchMendokusai.ContentAuthoring
{
    public static class RigFiles
    {
        public static string Find(string sceneFile)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(sceneFile));
            string sidecar = Path.Combine(directory, Path.GetFileNameWithoutExtension(sceneFile) + ".rig.json");
            return File.Exists(sidecar) ? sidecar : Path.Combine(directory, "rig.json");
        }

        public static string Read(string sceneFile, SceneDocument document)
        {
            string file = Find(sceneFile);
            if (File.Exists(file) == false)
            {
                return null;
            }
            RigDefinition rig = RigJson.Read(File.ReadAllText(file));
            foreach (RigJoint joint in rig.Joints)
            {
                if (Array.Exists(document.Nodes, node => node.Id == joint.Node) == false)
                {
                    throw new IOException("Rig binding references a missing scene node: " + joint.Role);
                }
            }
            return RigJson.Write(rig);
        }

        public static void Save(string sceneFile, string rig)
        {
            if (rig == null)
            {
                return;
            }
            string target = Path.Combine(Path.GetDirectoryName(sceneFile), Path.GetFileNameWithoutExtension(sceneFile) + ".rig.json");
            if (File.Exists(target) && RigJson.Write(RigJson.Read(File.ReadAllText(target))) != rig)
            {
                throw new IOException("Destination already has different rig bindings");
            }
            AtomicFiles.WriteText(target, rig);
        }
    }
}
