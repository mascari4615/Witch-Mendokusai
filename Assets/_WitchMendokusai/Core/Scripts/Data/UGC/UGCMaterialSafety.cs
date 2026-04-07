using UnityEngine;

namespace WitchMendokusai
{
	internal static class UGCMaterialSafety
	{
		private static readonly string[] ShaderCandidates =
		{
			"Universal Render Pipeline/Lit",
			"Standard",
			"HDRP/Lit",
			"Sprites/Default",
		};

		public static void EnsureUsableMaterial(Renderer renderer, Color defaultColor)
		{
			if (renderer == null)
				return;

			Material current = renderer.sharedMaterial;
			if (IsUsableMaterial(current))
				return;

			Shader fallbackShader = FindSupportedShader();
			if (fallbackShader == null)
				return;

			renderer.sharedMaterial = new Material(fallbackShader) { color = defaultColor };
		}

		private static bool IsUsableMaterial(Material material)
		{
			if (material == null)
				return false;

			Shader shader = material.shader;
			if (shader == null)
				return false;

			if (shader.name == "Hidden/InternalErrorShader")
				return false;

			return shader.isSupported;
		}

		private static Shader FindSupportedShader()
		{
			for (int i = 0; i < ShaderCandidates.Length; i++)
			{
				Shader shader = Shader.Find(ShaderCandidates[i]);
				if (shader != null && shader.isSupported)
					return shader;
			}

			return null;
		}
	}
}
