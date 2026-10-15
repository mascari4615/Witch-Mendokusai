using System;
using UnityEngine;

namespace SceneContent.Unity
{
    public interface ISurfaceFactory
    {
        Material Create(SurfaceMaterial surface, Texture texture);
    }

    public sealed class SingleShaderSurfaceFactory : ISurfaceFactory
    {
        private readonly Shader shader;

        public SingleShaderSurfaceFactory(Shader value)
        {
            shader = value != null ? value : throw new ArgumentNullException("value");
        }

        public Material Create(SurfaceMaterial surface, Texture texture)
        {
            return SurfaceMaterials.Create(surface, shader, texture);
        }
    }

    public sealed class StandardSurfaceFactory : ISurfaceFactory
    {
        private readonly Shader unlit;
        private readonly Shader lit;

        public StandardSurfaceFactory(Shader unlitShader, Shader litShader)
        {
            unlit = unlitShader != null ? unlitShader : throw new ArgumentNullException("unlitShader");
            lit = litShader != null ? litShader : throw new ArgumentNullException("litShader");
        }

        public Material Create(SurfaceMaterial surface, Texture texture)
        {
            return SurfaceMaterials.Create(surface, surface.LightingModel == "lit" ? lit : unlit, texture);
        }
    }
}
