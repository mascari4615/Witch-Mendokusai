using UnityEngine;
using UnityEngine.Rendering;

namespace SceneContent.Unity
{
    internal static class SurfaceMaterials
    {
        public static Material Create(SurfaceMaterial surface, Shader shader, Texture texture)
        {
            if (surface.LightingModel == "toon")
            {
                if (surface.ToonSurface == null) throw new System.ArgumentException("Missing toon surface block");
                surface.ToonSurface.Validate();
                foreach (string property in new[] { "_ShadowColor", "_ShadowExtend", "_ShadeSoftness", "_ShadeRichness", "_LightStrength", "_AmbientStrength",
                    "_RimStrength", "_RimPower", "_BandStrength", "_BandHeight", "_BandWidth", "_OutlineWidth", "_OutlineTint" })
                    if (shader.FindPropertyIndex(property) < 0) throw new System.NotSupportedException("Shader does not support required toon input: " + property);
            }
            Material material = new Material(shader) { name = "Native surface" };
            if (surface.LightingModel == "toon") ApplyToon(material, surface.ToonSurface);
            Color color = new Color(surface.BaseColor[0], surface.BaseColor[1], surface.BaseColor[2], surface.BaseColor[3]);
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(surface.TextureScale[0], surface.TextureScale[1]));
            material.SetTextureOffset("_BaseMap", new Vector2(surface.TextureOffset[0], surface.TextureOffset[1]));
            material.SetFloat("_Cull", surface.DoubleSided ? 0 : 2);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", surface.Metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1 - surface.Roughness);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(surface.Emission[0], surface.Emission[1], surface.Emission[2]));
                if (surface.Emission[0] + surface.Emission[1] + surface.Emission[2] > 0) material.EnableKeyword("_EMISSION");
            }
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

        private static void ApplyToon(Material material, ToonSurface surface)
        {
            material.SetColor("_ShadowColor", new Color(surface.ShadeTint[0], surface.ShadeTint[1], surface.ShadeTint[2], 1));
            material.SetFloat("_ShadowExtend", surface.ShadeExtent);
            material.SetFloat("_ShadeSoftness", surface.ShadeSoftness);
            material.SetFloat("_ShadeRichness", surface.ShadeRichness);
            material.SetFloat("_LightStrength", surface.LightStrength);
            material.SetFloat("_AmbientStrength", surface.AmbientStrength);
            material.SetFloat("_RimStrength", surface.RimStrength);
            material.SetFloat("_RimPower", surface.RimPower);
            material.SetFloat("_BandStrength", surface.BandStrength);
            material.SetFloat("_BandHeight", surface.BandHeight);
            material.SetFloat("_BandWidth", surface.BandWidth);
            material.SetFloat("_OutlineWidth", surface.OutlineWidth);
            material.SetColor("_OutlineTint", new Color(surface.OutlineTint[0], surface.OutlineTint[1], surface.OutlineTint[2], 1));
        }
    }
}
