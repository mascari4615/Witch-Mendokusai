using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>런타임 머티리얼과 작은 메시만. 적 도형은 <see cref="IdleGeometry"/> 소관</summary>
	internal static class IdleBattleVisualFactory
	{
		public static Material Paint(GameObject piece, Color color)
		{
			Material made = MakeMaterial(color);
			piece.GetComponent<MeshRenderer>().sharedMaterial = made;
			return made;
		}

		/// <summary>발광하는 머티리얼. 보스만 쓴다 (visual.md 6)</summary>
		public static Material MakeGlowing(Color color, float strength)
		{
			Material made = MakeMaterial(color);
			made.EnableKeyword("_EMISSION");
			made.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

			if (made.HasProperty("_EmissionColor"))
			{
				made.SetColor("_EmissionColor", color * strength);
			}

			return made;
		}

		public static Material MakeMaterial(Color color)
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
			{
				shader = Shader.Find("Standard");
			}

			Material made = new Material(shader);
			made.hideFlags = HideFlags.DontSave;
			made.color = color;
			if (made.HasProperty("_BaseColor"))
			{
				made.SetColor("_BaseColor", color);
			}

			return made;
		}

		/// <summary>충격 알갱이 하나. 작은 정팔면체</summary>
		public static Mesh BuildImpactMesh()
		{
			return IdleGeometry.Build(IdleGeometry.Shape.Octahedron, 0.5f);
		}
	}
}
