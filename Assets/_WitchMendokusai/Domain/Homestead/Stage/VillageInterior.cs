using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 닫힌 실내의 벽, 바닥, 천장. 마을 재질은 카메라 가까운 면을 점무늬로 비우는데 (나뭇잎용), 실내에서는 벽 너머 허공이 보임
	// 같은 재질의 비우기를 끈 사본으로 바꿈. 사본은 재질마다 하나
	public static class VillageInterior
	{
		private const string OCCLUDABLE = "_WM_OCCLUDABLE";
		private static readonly int OCCLUDABLE_ID = Shader.PropertyToID("_WM_Occludable");
		private static readonly Dictionary<Material, Material> solid = new();

		public static void Solidify(GameObject root)
		{
			foreach (MeshRenderer target in root.GetComponentsInChildren<MeshRenderer>(true))
			{
				Material[] materials = target.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
					materials[i] = SolidOf(materials[i]);
				target.sharedMaterials = materials;
			}
		}

		private static Material SolidOf(Material source)
		{
			if (source.IsKeywordEnabled(OCCLUDABLE) == false)
				return source;
			if (solid.TryGetValue(source, out Material made) && made != null)
				return made;
			made = new Material(source) { name = source.name + " (Interior)", hideFlags = HideFlags.DontSave };
			made.DisableKeyword(OCCLUDABLE);
			made.SetFloat(OCCLUDABLE_ID, 0f);
			solid[source] = made;
			return made;
		}
	}
}
