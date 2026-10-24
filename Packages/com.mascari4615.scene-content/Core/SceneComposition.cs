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
                Id = left.Id, Version = Math.Max(left.Version, right.Version), Nodes = nodes.ToArray(), Meshes = meshes.ToArray(), Materials = materials.ToArray(),
                Resources = resources.ToArray(), Cameras = cameras.ToArray(), Lights = lights.ToArray(),
                Parameters = SceneParameters.Compose(left.Parameters, right.Parameters, prefix, left.Meshes.Length),
                LimbConstraints = ComposeConstraints(left, right, prefix),
                Springs = Combine(left.Springs, SceneDynamics.Copy(right.Springs, prefix + ":")),
                Colliders = Combine(left.Colliders, SceneDynamics.Copy(right.Colliders, prefix + ":"))
            };
            SceneValidation.Ensure(result);
            return result;
        }

        // Adds a self-contained piece under an existing node. Nodes listed in sharedNodes are not copied:
        // every reference to them is redirected to the named node of the current scene (skinned pieces bind to the host's bones).
        public static SceneDocument Attach(SceneDocument current, SceneDocument addition, string prefix, string parentNode,
            IDictionary<string, string> sharedNodes)
        {
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(parentNode) || sharedNodes == null)
            {
                throw new ArgumentException("Attach requires a namespace, a parent node and a shared node map");
            }
            SceneDocument result = SceneCloning.Copy(current);
            SceneDocument piece = SceneCloning.Copy(addition);
            if (piece.Cameras.Length > 0 || piece.Lights.Length > 0 || piece.Parameters != null || piece.LimbConstraints.Length > 0)
            {
                throw new ArgumentException("An attached piece may contain nodes, meshes, materials, resources, springs and colliders only");
            }
            foreach (SceneSpring spring in piece.Springs)
                if (sharedNodes.ContainsKey(spring.Joint) || sharedNodes.ContainsKey(spring.Tip))
                    throw new ArgumentException("A spring of an attached piece cannot use a shared host node: " + spring.Id);
            foreach (SceneCollider collider in piece.Colliders)
                if (sharedNodes.ContainsKey(collider.Node)) throw new ArgumentException("A collider of an attached piece cannot sit on a shared host node: " + collider.Id);
            Dictionary<string, int> hostIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < result.Nodes.Length; index++) hostIndices[result.Nodes[index].Id] = index;
            int parent;
            if (hostIndices.TryGetValue(parentNode, out parent) == false) throw new ArgumentException("Missing parent node: " + parentNode);
            int[] map = new int[piece.Nodes.Length];
            int added = 0;
            for (int index = 0; index < piece.Nodes.Length; index++)
            {
                string host;
                if (sharedNodes.TryGetValue(piece.Nodes[index].Id, out host))
                {
                    if (hostIndices.TryGetValue(host, out map[index]) == false) throw new ArgumentException("Missing shared host node: " + host);
                }
                else
                {
                    map[index] = result.Nodes.Length + added++;
                }
            }
            foreach (string shared in sharedNodes.Keys)
                if (Array.Exists(piece.Nodes, node => node.Id == shared) == false) throw new ArgumentException("Shared node is not in the piece: " + shared);
            List<SceneNode> nodes = new List<SceneNode>(result.Nodes);
            for (int index = 0; index < piece.Nodes.Length; index++)
            {
                SceneNode node = piece.Nodes[index];
                if (sharedNodes.ContainsKey(node.Id)) continue;
                node.Id = prefix + ":" + node.Id;
                if (string.IsNullOrEmpty(node.Label) == false) node.Label = prefix + "/" + node.Label;
                node.Parent = node.Parent < 0 ? parent : map[node.Parent];
                nodes.Add(node);
            }
            int materialOffset = result.Materials.Length;
            List<ContentResource> resources = new List<ContentResource>(result.Resources);
            foreach (ContentResource resource in piece.Resources)
            {
                resource.Id = prefix + ":" + resource.Id;
                resources.Add(resource);
            }
            List<SurfaceMaterial> materials = new List<SurfaceMaterial>(result.Materials);
            foreach (SurfaceMaterial material in piece.Materials)
            {
                if (string.IsNullOrEmpty(material.BaseColorTexture) == false) material.BaseColorTexture = prefix + ":" + material.BaseColorTexture;
                materials.Add(material);
            }
            List<MeshInstance> meshes = new List<MeshInstance>(result.Meshes);
            foreach (MeshInstance mesh in piece.Meshes)
            {
                mesh.Node = map[mesh.Node];
                if (mesh.RootBone >= 0) mesh.RootBone = map[mesh.RootBone];
                for (int index = 0; index < mesh.Bones.Length; index++) mesh.Bones[index] = map[mesh.Bones[index]];
                for (int index = 0; index < mesh.Materials.Length; index++) mesh.Materials[index] += materialOffset;
                mesh.Geometry = prefix + ":" + mesh.Geometry;
                meshes.Add(mesh);
            }
            result.Nodes = nodes.ToArray();
            result.Version = Math.Max(result.Version, piece.Version);
            result.Resources = resources.ToArray();
            result.Materials = materials.ToArray();
            result.Meshes = meshes.ToArray();
            result.Springs = Combine(result.Springs, SceneDynamics.Copy(piece.Springs, prefix + ":"));
            result.Colliders = Combine(result.Colliders, SceneDynamics.Copy(piece.Colliders, prefix + ":"));
            SceneValidation.Ensure(result);
            return result;
        }

        private static T[] Combine<T>(T[] left, T[] right)
        {
            List<T> values = new List<T>(left);
            values.AddRange(right);
            return values.ToArray();
        }

        private static SceneLimbConstraint[] ComposeConstraints(SceneDocument left, SceneDocument right, string prefix)
        {
            List<SceneLimbConstraint> constraints = new List<SceneLimbConstraint>(SceneConstraints.Copy(left.LimbConstraints));
            constraints.AddRange(SceneConstraints.Copy(right.LimbConstraints, prefix));
            return constraints.ToArray();
        }
    }
}
