using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 숲 나무와 자갈 배치. 나무는 본보기 프리팹 몇 개를 자리 목록대로 복제, 자갈은 메시 몇 개를 한 메시로 합침
	// 자리 목록은 굽는 도구가 숲 바닥 높이 함수로 채움. 수백 그루를 프리팹 인스턴스로 두면 자산이 커져 목록으로 보관
	public class ForestScatter : MonoBehaviour
	{
		[Serializable]
		public struct Placement
		{
			public int Template;
			public Vector3 Position;
			public float Yaw;
			public float Scale;
		}

		[Header("Trees")]
		[SerializeField] private GameObject[] treeTemplates = Array.Empty<GameObject>();
		[SerializeField] private Placement[] trees = Array.Empty<Placement>();

		[Header("Pebbles")]
		[SerializeField] private Mesh[] pebbleMeshes = Array.Empty<Mesh>();
		[SerializeField] private Material pebbleMaterial;
		[Tooltip("Scale 은 가로, Yaw 는 회전. 높이와 깊이 비율은 아래 값")]
		[SerializeField] private Placement[] pebbles = Array.Empty<Placement>();
		[SerializeField] private Vector3 pebbleShape = new(1.3f, 0.7f, 1.1f);

		private bool isBuilt;
		private Mesh pebbleMesh;

		private void Awake() => Build();

		private void OnDestroy()
		{
			if (pebbleMesh != null)
				Destroy(pebbleMesh);
		}

		public void Build()
		{
			if (isBuilt)
				return;
			isBuilt = true;
			Transform treeRoot = new GameObject("Trees").transform;
			treeRoot.SetParent(transform, false);
			foreach (Placement placement in trees)
			{
				GameObject tree = Instantiate(treeTemplates[placement.Template], treeRoot);
				tree.transform.SetLocalPositionAndRotation(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f));
				tree.transform.localScale = Vector3.one * placement.Scale;
			}
			BuildPebbles();
		}

		private void BuildPebbles()
		{
			if (pebbles.Length == 0)
				return;
			List<CombineInstance> parts = new(pebbles.Length);
			foreach (Placement placement in pebbles)
			{
				parts.Add(new CombineInstance
				{
					mesh = pebbleMeshes[placement.Template],
					transform = Matrix4x4.TRS(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f), pebbleShape * placement.Scale)
				});
			}
			pebbleMesh = new() { name = "ForestPebbles", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
			pebbleMesh.CombineMeshes(parts.ToArray(), true, true);
			GameObject pebbleObject = new("Pebbles");
			pebbleObject.transform.SetParent(transform, false);
			pebbleObject.AddComponent<MeshFilter>().sharedMesh = pebbleMesh;
			MeshRenderer pebbleRenderer = pebbleObject.AddComponent<MeshRenderer>();
			pebbleRenderer.sharedMaterial = pebbleMaterial;
			pebbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
		}
	}
}
