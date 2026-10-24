using System;
using System.Collections.Generic;

namespace SceneContent
{
    public static class SceneValidation
    {
        public static void Ensure(SceneDocument scene)
        {
            if (scene == null || scene.Format != "scene-content" || (scene.Version != 1 && scene.Version != 2) ||
                scene.CoordinateFrame != "right-handed-y-up-z-back" || string.IsNullOrEmpty(scene.Id))
                throw new ArgumentException("Unsupported or missing scene contract");
            if (scene.Nodes == null || scene.Nodes.Length == 0 || scene.Nodes.Length > 100000 ||
                scene.Meshes == null || scene.Meshes.Length > 10000 || scene.Materials == null ||
                scene.Materials.Length > 10000 || scene.Resources == null || scene.Resources.Length > 20000)
                throw new ArgumentException("Invalid scene collection size");
            HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < scene.Nodes.Length; index++)
            {
                SceneNode node = scene.Nodes[index];
                if (node == null || string.IsNullOrEmpty(node.Id) || nodeIds.Add(node.Id) == false ||
                    node.Parent < -1 || node.Parent >= scene.Nodes.Length || node.Parent == index || (node.Label != null && node.Label.Length > 1024))
                    throw new ArgumentException("Invalid scene node or parent");
                Vector(node.Position, 3);
                Vector(node.Rotation, 4);
                Vector(node.Scale, 3);
                float norm = 0;
                foreach (float value in node.Rotation) norm += value * value;
                if (Math.Abs(norm - 1) > 0.01f) throw new ArgumentException("Quaternion must be normalized");
            }
            int[] states = new int[scene.Nodes.Length];
            ComponentValidation.Ensure(scene);
            for (int start = 0; start < states.Length; start++)
            {
                int node = start;
                while (node >= 0 && states[node] == 0)
                {
                    states[node] = 1;
                    node = scene.Nodes[node].Parent;
                }
                if (node >= 0 && states[node] == 1) throw new ArgumentException("Scene graph cycle");
                node = start;
                while (node >= 0 && states[node] == 1)
                {
                    states[node] = 2;
                    node = scene.Nodes[node].Parent;
                }
            }
            Dictionary<string, ContentResource> resources = new Dictionary<string, ContentResource>(StringComparer.Ordinal);
            foreach (ContentResource resource in scene.Resources)
            {
                if (resource == null || string.IsNullOrEmpty(resource.Id) || resources.ContainsKey(resource.Id) ||
                    string.IsNullOrEmpty(resource.Path) || resource.Sha256 == null || resource.Sha256.Length != 64 ||
                    (resource.MediaType != "image/png" && resource.MediaType != "application/vnd.native.geometry"))
                    throw new ArgumentException("Invalid resource descriptor");
                foreach (char character in resource.Sha256)
                    if ((character >= '0' && character <= '9') == false && (character >= 'a' && character <= 'f') == false)
                        throw new ArgumentException("SHA256 must be lowercase hexadecimal");
                resources.Add(resource.Id, resource);
            }
            foreach (SurfaceMaterial material in scene.Materials)
            {
                if (material == null) throw new ArgumentException("Missing material");
                Vector(material.BaseColor, 4);
                Vector(material.TextureScale, 2);
                Vector(material.TextureOffset, 2);
                Vector(material.Emission, 3);
                if ((material.LightingModel != "unlit" && material.LightingModel != "lit" && material.LightingModel != "toon") || material.Roughness < 0 || material.Roughness > 1 ||
                    float.IsNaN(material.Roughness) || material.Metallic < 0 || material.Metallic > 1 || float.IsNaN(material.Metallic))
                    throw new ArgumentException("Invalid surface lighting model or physical properties");
                if (scene.Version == 1 && (material.LightingModel == "toon" || material.ToonSurface != null || material.MaterialClass != null))
                    throw new ArgumentException("Toon surfaces and material classes require scene version 2");
                if ((material.LightingModel == "toon") != (material.ToonSurface != null))
                    throw new ArgumentException("Toon lighting requires exactly one toon surface block");
                if (material.ToonSurface != null) material.ToonSurface.Validate();
                if (material.MaterialClass != null && material.MaterialClass != "generic" && material.MaterialClass != "hair" &&
                    material.MaterialClass != "skin" && material.MaterialClass != "clothes" && material.MaterialClass != "face-overlay")
                    throw new ArgumentException("Unsupported material class");
                foreach (float emission in material.Emission)
                    if (emission < 0) throw new ArgumentException("Emission must be nonnegative");
                if (material.AlphaMode != "opaque" && material.AlphaMode != "mask" && material.AlphaMode != "blend")
                    throw new ArgumentException("Unsupported alpha mode");
                if (string.IsNullOrEmpty(material.BaseColorTexture) == false)
                    RequireResource(resources, material.BaseColorTexture, "image/png");
            }
            foreach (MeshInstance mesh in scene.Meshes)
            {
                if (mesh == null || mesh.Node < 0 || mesh.Node >= scene.Nodes.Length || mesh.Bones == null ||
                    mesh.Bones.Length > 16384 || mesh.Materials == null || mesh.Materials.Length == 0 || mesh.Materials.Length > 128)
                    throw new ArgumentException("Invalid mesh binding");
                if (mesh.RootBone < -1 || mesh.RootBone >= scene.Nodes.Length || mesh.MorphWeights == null || mesh.MorphWeights.Length > 4096)
                    throw new ArgumentException("Invalid root bone or morph bindings");
                Vector(mesh.MorphWeights, mesh.MorphWeights.Length);
                RequireResource(resources, mesh.Geometry, "application/vnd.native.geometry");
                foreach (int bone in mesh.Bones)
                    if (bone < 0 || bone >= scene.Nodes.Length) throw new ArgumentException("Bone node out of range");
                foreach (int material in mesh.Materials)
                    if (material < 0 || material >= scene.Materials.Length) throw new ArgumentException("Material out of range");
            }
            SceneParameters.Ensure(scene);
            SceneConstraints.Order(scene);
            SceneDynamics.Order(scene);
            HashSet<string> rigid = SceneConstraints.RigidNodes(scene);
            foreach (SceneNode node in scene.Nodes) if (rigid.Contains(node.Id)) SceneConstraints.EnsureScale(node.Scale);
        }

        private static void RequireResource(Dictionary<string, ContentResource> resources, string id, string type)
        {
            ContentResource value;
            if (id == null || resources.TryGetValue(id, out value) == false || value.MediaType != type)
                throw new ArgumentException("Missing or mistyped resource reference");
        }

        private static void Vector(float[] values, int count)
        {
            if (values == null || values.Length != count) throw new ArgumentException("Invalid vector size");
            foreach (float value in values)
                if (float.IsInfinity(value) || float.IsNaN(value)) throw new ArgumentException("Non-finite scene value");
        }
    }
}
