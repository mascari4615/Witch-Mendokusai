using UnityEngine;
using UnityEngine.Rendering;

namespace SceneContent.Unity
{
    internal static class SurfaceMaterials
    {
        public static Material Create(SurfaceMaterial surface, Shader shader, Texture texture)
        {
            Material material = new Material(shader) { name = "Native surface" };
            Color color = new Color(surface.BaseColor[0], surface.BaseColor[1], surface.BaseColor[2], surface.BaseColor[3]);
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(surface.TextureScale[0], surface.TextureScale[1]));
            material.SetTextureOffset("_BaseMap", new Vector2(surface.TextureOffset[0], surface.TextureOffset[1]));
            material.SetFloat("_Cull", surface.DoubleSided ? 0 : 2);
            bool blend = surface.AlphaMode == "blend";
            bool mask = surface.AlphaMode == "mask";
            material.SetFloat("_Surface", blend ? 1 : 0);
            material.SetFloat("_AlphaClip", mask ? 1 : 0);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_SrcBlend", (float)(blend ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(blend ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", blend ? 0 : 1);
            if (mask) material.EnableKeyword("_ALPHATEST_ON");
            if (blend) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", blend ? "Transparent" : (mask ? "TransparentCutout" : "Opaque"));
            material.renderQueue = blend ? 3000 : (mask ? 2450 : 2000);
            return material;
        }
    }
}
