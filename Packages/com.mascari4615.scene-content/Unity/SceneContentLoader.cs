using System;
using System.Collections.Generic;
using Geometry;
using Geometry.Serialization;
using Geometry.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneContent.Unity
{
    public static class SceneContentLoader
    {
        public static SceneInstance Load(SceneDocument document, IContentSource source, Shader shader, Transform parent = null, bool allowReducedSkinning = false, bool activateCameras = true)
        {
            return LoadWithSurfaces(document, source, new SingleShaderSurfaceFactory(shader), parent, allowReducedSkinning, activateCameras);
        }

        public static SceneInstance LoadWithSurfaces(SceneDocument document, IContentSource source, ISurfaceFactory surfaces, Transform parent = null, bool allowReducedSkinning = false, bool activateCameras = true)
        {
            SceneValidation.Ensure(document);
            if (source == null || surfaces == null) throw new ArgumentException("Content source and surface factory are required");
            GameObject root = new GameObject(document.Id);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            SceneInstance instance = root.AddComponent<SceneInstance>();
            try
            {
                Transform[] nodes = new Transform[document.Nodes.Length];
                Dictionary<string, Transform> byId = new Dictionary<string, Transform>(StringComparer.Ordinal);
                for (int index = 0; index < nodes.Length; index++)
                {
                    nodes[index] = new GameObject(document.Nodes[index].Id).transform;
                    nodes[index].SetParent(root.transform, false);
                    byId.Add(document.Nodes[index].Id, nodes[index]);
                }
                for (int index = 0; index < nodes.Length; index++)
                {
                    SceneNode node = document.Nodes[index];
                    nodes[index].SetParent(node.Parent < 0 ? root.transform : nodes[node.Parent], false);
                    nodes[index].localPosition = new Vector3(node.Position[0], node.Position[1], -node.Position[2]);
                    nodes[index].localRotation = new Quaternion(-node.Rotation[0], -node.Rotation[1], node.Rotation[2], node.Rotation[3]).normalized;
                    nodes[index].localScale = new Vector3(node.Scale[0], node.Scale[1], node.Scale[2]);
                }
                Dictionary<string, ContentResource> descriptors = new Dictionary<string, ContentResource>(StringComparer.Ordinal);
                foreach (ContentResource resource in document.Resources) descriptors.Add(resource.Id, resource);
                Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
                Material[] materials = new Material[document.Materials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    SurfaceMaterial surface = document.Materials[index];
                    Texture2D texture = null;
                    string textureHash = string.IsNullOrEmpty(surface.BaseColorTexture) ? null : descriptors[surface.BaseColorTexture].Sha256;
                    if (textureHash != null && textures.TryGetValue(textureHash, out texture) == false)
                    {
                        byte[] image = source.Read(descriptors[surface.BaseColorTexture]);
                        PngHeader.ValidateDimensions(image);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        instance.Own(texture);
                        if (ImageConversion.LoadImage(texture, image, true) == false)
                            throw new ArgumentException("Invalid texture image");
                        textures.Add(textureHash, texture);
                    }
                    materials[index] = surfaces.Create(surface, texture);
                    if (materials[index] == null) throw new ArgumentException("Surface factory returned no material");
                    instance.Own(materials[index]);
                }
                Dictionary<string, Mesh> geometries = new Dictionary<string, Mesh>();
                Renderer[] renderers = new Renderer[document.Meshes.Length];
                for (int index = 0; index < document.Meshes.Length; index++)
                {
                    MeshInstance binding = document.Meshes[index];
                    Mesh mesh;
                    string geometryHash = descriptors[binding.Geometry].Sha256;
                    if (geometries.TryGetValue(geometryHash, out mesh) == false)
                    {
                        MeshGeometry geometry = GeometryBinary.Read(source.Read(descriptors[binding.Geometry]));
                        int influences = 0;
                        for (int vertex = 0; vertex < geometry.BoneWeights.Length; vertex += 4)
                        {
                            int count = 0;
                            for (int influence = 0; influence < 4; influence++)
                                if (geometry.BoneWeights[vertex + influence] > 0) count++;
                            influences = Math.Max(influences, count);
                        }
                        if (allowReducedSkinning == false && influences > (int)QualitySettings.skinWeights)
                            throw new InvalidOperationException("Geometry requires " + influences + " skin influences, host supports " + (int)QualitySettings.skinWeights + ". Configure host skin-weight quality before loading.");
                        mesh = UnityGeometry.Create(geometry);
                        instance.Own(mesh);
                        geometries.Add(geometryHash, mesh);
                    }
                    if (binding.Materials.Length != mesh.subMeshCount || binding.MorphWeights.Length != mesh.blendShapeCount || (mesh.boneWeights.Length > 0 && binding.Bones.Length != mesh.bindposes.Length))
                        throw new ArgumentException("Geometry binding count mismatch");
                    GameObject meshObject = new GameObject("mesh-" + index);
                    meshObject.transform.SetParent(nodes[binding.Node], false);
                    Renderer renderer;
                    if (mesh.boneWeights.Length > 0 || mesh.blendShapeCount > 0)
                    {
                        SkinnedMeshRenderer skin = meshObject.AddComponent<SkinnedMeshRenderer>();
                        Transform[] bones = new Transform[binding.Bones.Length];
                        for (int bone = 0; bone < bones.Length; bone++) bones[bone] = nodes[binding.Bones[bone]];
                        skin.sharedMesh = mesh;
                        skin.bones = bones;
                        skin.quality = SkinQuality.Bone4;
                        skin.rootBone = binding.RootBone < 0 ? null : nodes[binding.RootBone];
                        skin.updateWhenOffscreen = true;
                        skin.localBounds = mesh.bounds;
                        for (int morph = 0; morph < binding.MorphWeights.Length; morph++) skin.SetBlendShapeWeight(morph, binding.MorphWeights[morph] * 100);
                        renderer = skin;
                    }
                    else
                    {
                        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        renderer = meshObject.AddComponent<MeshRenderer>();
                    }
                    Material[] boundSurfaces = new Material[binding.Materials.Length];
                    for (int surface = 0; surface < boundSurfaces.Length; surface++) boundSurfaces[surface] = materials[binding.Materials[surface]];
                    renderer.sharedMaterials = boundSurfaces;
                    renderer.shadowCastingMode = binding.ShadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                    renderers[index] = renderer;
                }
                SceneComponents.Attach(document, nodes, activateCameras);
                instance.Initialize(byId, renderers, document);
                root.SetActive(true);
                return instance;
            }
            catch
            {
                instance.DisposeAssets();
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }
    }
}
