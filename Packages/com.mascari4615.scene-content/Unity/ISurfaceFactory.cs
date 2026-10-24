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
        private readonly Shader toon;

        public StandardSurfaceFactory(Shader unlitShader, Shader litShader)
            : this(unlitShader, litShader, null)
        {
        }

        public StandardSurfaceFactory(Shader unlitShader, Shader litShader, Shader toonShader)
        {
            unlit = unlitShader != null ? unlitShader : throw new ArgumentNullException("unlitShader");
            lit = litShader != null ? litShader : throw new ArgumentNullException("litShader");
            toon = toonShader;
        }

        public Material Create(SurfaceMaterial surface, Texture texture)
        {
            if (surface == null) throw new ArgumentNullException("surface");
            switch (surface.LightingModel)
            {
                case "unlit": return SurfaceMaterials.Create(surface, unlit, texture);
                case "lit": return SurfaceMaterials.Create(surface, lit, texture);
                case "toon":
                    if (toon == null) throw new NotSupportedException("This host has no toon surface shader");
                    return SurfaceMaterials.Create(surface, toon, texture);
                default: throw new ArgumentException("Unsupported surface lighting model");
            }
        }
    }
}
