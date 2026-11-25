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
			[Tooltip("차지하는 1m 칸. 건물은 앞마당 한 줄 포함 (BuildingFootprint 와 같은 단위)")] public Vector2Int Cells;
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

		// 복셀과 같은 1m 격자에 바닥이 맞는 자리. 반 바퀴가 아닌 방향은 가로세로가 바뀜
		public Vector3 Snap(string key, Vector3 position, float yaw)
		{
			Vector2Int cells = Find(key).Cells;
			bool isSideways = Mathf.RoundToInt(yaw / 90f) % 2 != 0;
			float halfX = (isSideways ? cells.y : cells.x) * 0.5f;
			float halfZ = (isSideways ? cells.x : cells.y) * 0.5f;
			return new Vector3(Mathf.Round(position.x - halfX) + halfX, position.y, Mathf.Round(position.z - halfZ) + halfZ);
		}

		// 칸에 맞춰 세움. 앞마당이 있는 건물은 몸이 그만큼 뒤로
		public GameObject BuildOnGrid(string key, Transform parent, Vector3 position, float yaw)
		{
			Vector3 center = Snap(key, position, yaw);
			float yard = Find(key).Cells.y >= 5 ? 0.5f : 0f;
			return Build(key, parent, center + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, yard), yaw);
		}

		// 격자에 맞춘 몸이 차지하는 네모 (x, z)
		public Rect Footprint(string key, Vector3 position, float yaw)
		{
			Vector2Int cells = Find(key).Cells;
			Vector3 center = Snap(key, position, yaw);
			Vector2 size = Mathf.RoundToInt(yaw / 90f) % 2 != 0 ? new Vector2(cells.y, cells.x) : cells;
			return new Rect(center.x - size.x * 0.5f, center.z - size.y * 0.5f, size.x, size.y);
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
