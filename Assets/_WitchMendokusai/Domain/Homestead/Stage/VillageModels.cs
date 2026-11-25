using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 마을 건물과 소품의 모델 모음. 이름 하나에 메시, 재질 (메시 조각 순서), 막는 상자
	/// Resources 하나. 모델은 자산이라 자리마다 만들지 않고 같이 씀
	/// </summary>
	[CreateAssetMenu(fileName = nameof(VillageModels), menuName = "WM/Homestead/Village Models")]
	public sealed class VillageModels : ScriptableObject
	{
		[Serializable]
		public struct Model
		{
			public string Key;
			public Mesh Mesh;
			public Material[] Materials;
			public bool CastShadow;
			[Tooltip("만들 때 맞춘 1m 칸. 놓는 규칙은 PlaceableDef 의 칸을 씀")] public Vector2Int Cells;
			public Bounds[] Solids;
		}

		[SerializeField] private Model[] models;

		private static VillageModels instance;

		public static VillageModels Instance
		{
			get
			{
				if (instance == null)
					instance = Resources.Load<VillageModels>(nameof(VillageModels));
				return instance;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState() => instance = null;

		public IReadOnlyList<Model> Models => models;

		public GameObject Build(string key, Transform parent, Vector3 localPosition, float yaw, float scale = 1f)
		{
			Model model = Find(key);
			GameObject root = new(key);
			root.transform.SetParent(parent, false);
			root.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
			root.transform.localScale = Vector3.one * scale;
			root.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
			MeshRenderer renderer = root.AddComponent<MeshRenderer>();
			renderer.sharedMaterials = model.Materials;
			renderer.shadowCastingMode = model.CastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
			foreach (Bounds solid in model.Solids)
			{
				BoxCollider box = root.AddComponent<BoxCollider>();
				box.center = solid.center;
				box.size = solid.size;
			}
			return root;
		}

		private Model Find(string key)
		{
			foreach (Model model in models)
			{
				if (model.Key == key)
					return model;
			}
			throw new KeyNotFoundException($"마을 모델 없음: {key}");
		}
	}
}
