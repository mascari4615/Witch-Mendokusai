using System;
using System.Collections.Generic;
using System.IO;
using Rigging;
using Rigging.Serialization;
using SceneContent;
using SceneContent.Storage;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.ContentAuthoring
{
    public static class WorkspaceComposition
    {
        public static string AddActor(ContentWorkspace workspace, string actorFile)
        {
            SceneDocument current = SceneSnapshot.Capture(workspace.Document, workspace.Instance);
            SceneDocument actor = SceneFiles.Read(actorFile);
            string currentRig = RigFiles.Read(workspace.FilePath, current);
            string actorRig = RigFiles.Read(actorFile, actor);
            List<string> pinned = new List<string>();
            if (actorRig != null)
            {
                foreach (RigJoint joint in RigJson.Read(actorRig).Joints) pinned.Add(joint.Node);
            }
            actor.Cameras = new SceneCamera[0];
            actor.Lights = new SceneLight[0];
            actor = ScenePruning.Snapshot(actor, pinned);
            int count = 2;
            while (Array.Exists(current.Nodes, node => node.Id.StartsWith("actor-" + count + ":", StringComparison.Ordinal))) count++;
            string prefix = "actor-" + count;
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WitchMendokusai", "ContentLibrary");
            ContentLibrary library = new ContentLibrary(UnityEditor.EditorPrefs.GetString("WM.ContentLibrary.Directory", directory));
            string first = library.Import(current, new DirectoryContentSource(Path.GetDirectoryName(workspace.FilePath)));
            string second = library.Import(actor, new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(actorFile))));
            float offset = Mathf.Max(.8f, workspace.GetBounds().size.x + .3f);
            SceneDocument combined = SceneComposition.Append(SceneFiles.Read(first), SceneFiles.Read(second), prefix, new[] { offset, 0f, 0f });
            string file = library.Import(combined, new DirectoryContentSource(library.Root));
            List<RigJoint> joints = new List<RigJoint>();
            List<RigLimb> limbs = new List<RigLimb>();
            if (currentRig != null)
            {
                RigDefinition rig = RigJson.Read(currentRig);
                joints.AddRange(rig.Joints);
                limbs.AddRange(rig.Limbs);
            }
            if (actorRig != null)
            {
                RigDefinition rig = RigJson.Read(actorRig);
                foreach (RigJoint joint in rig.Joints)
                    joints.Add(new RigJoint { Role = prefix + ":" + joint.Role, Node = prefix + ":" + joint.Node });
                foreach (RigLimb limb in rig.Limbs)
                    limbs.Add(new RigLimb { Id = prefix + "/" + limb.Id, Upper = prefix + ":" + limb.Upper, Lower = prefix + ":" + limb.Lower, End = prefix + ":" + limb.End });
            }
            if (joints.Count > 0)
            {
                RigFiles.Save(file, RigJson.Write(new RigDefinition { Id = "rig-" + combined.Id, Joints = joints.ToArray(), Limbs = limbs.ToArray() }));
            }
            return file;
        }
    }
}
