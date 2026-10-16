using System;
using System.Collections.Generic;

namespace SceneContent
{
    public static class SceneComposition
    {
        public static SceneDocument Append(SceneDocument current, SceneDocument addition, string prefix, float[] position)
        {
            if (string.IsNullOrEmpty(prefix) || position == null || position.Length != 3)
            {
                throw new ArgumentException("Composition requires a namespace and root position");
            }
            SceneDocument left = SceneCloning.Copy(current);
            SceneDocument right = SceneCloning.Copy(addition);
            int nodeOffset = left.Nodes.Length + 1;
            int materialOffset = left.Materials.Length;
            List<SceneNode> nodes = new List<SceneNode>(left.Nodes);
            nodes.Add(new SceneNode
            {
                Id = prefix, Label = "Actor " + prefix, Parent = -1,
                Position = (float[])position.Clone(), Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f }
            });
            foreach (SceneNode node in right.Nodes)
            {
                node.Id = prefix + ":" + node.Id;
                if (string.IsNullOrEmpty(node.Label) == false) node.Label = prefix + "/" + node.Label;
                node.Parent = node.Parent < 0 ? nodeOffset - 1 : node.Parent + nodeOffset;
                nodes.Add(node);
            }
            List<ContentResource> resources = new List<ContentResource>(left.Resources);
            foreach (ContentResource resource in right.Resources)
            {
                resource.Id = prefix + ":" + resource.Id;
                resources.Add(resource);
            }
            List<SurfaceMaterial> materials = new List<SurfaceMaterial>(left.Materials);
            foreach (SurfaceMaterial material in right.Materials)
            {
                if (string.IsNullOrEmpty(material.BaseColorTexture) == false) material.BaseColorTexture = prefix + ":" + material.BaseColorTexture;
                materials.Add(material);
            }
            List<MeshInstance> meshes = new List<MeshInstance>(left.Meshes);
            foreach (MeshInstance mesh in right.Meshes)
            {
                mesh.Node += nodeOffset;
                if (mesh.RootBone >= 0) mesh.RootBone += nodeOffset;
                for (int index = 0; index < mesh.Bones.Length; index++) mesh.Bones[index] += nodeOffset;
                for (int index = 0; index < mesh.Materials.Length; index++) mesh.Materials[index] += materialOffset;
                mesh.Geometry = prefix + ":" + mesh.Geometry;
                meshes.Add(mesh);
            }
            List<SceneCamera> cameras = new List<SceneCamera>(left.Cameras);
            foreach (SceneCamera camera in right.Cameras)
            {
                camera.Node += nodeOffset;
                cameras.Add(camera);
            }
            List<SceneLight> lights = new List<SceneLight>(left.Lights);
            foreach (SceneLight light in right.Lights)
            {
                light.Node += nodeOffset;
                lights.Add(light);
            }
            SceneDocument result = new SceneDocument
            {
                Id = left.Id, Nodes = nodes.ToArray(), Meshes = meshes.ToArray(), Materials = materials.ToArray(),
                Resources = resources.ToArray(), Cameras = cameras.ToArray(), Lights = lights.ToArray(),
                Parameters = SceneParameters.Compose(left.Parameters, right.Parameters, prefix, left.Meshes.Length),
                LimbConstraints = ComposeConstraints(left, right, prefix)
            };
            SceneValidation.Ensure(result);
            return result;
        }

        private static SceneLimbConstraint[] ComposeConstraints(SceneDocument left, SceneDocument right, string prefix)
        {
            List<SceneLimbConstraint> constraints = new List<SceneLimbConstraint>(SceneConstraints.Copy(left.LimbConstraints));
            constraints.AddRange(SceneConstraints.Copy(right.LimbConstraints, prefix));
            return constraints.ToArray();
        }
    }
}
