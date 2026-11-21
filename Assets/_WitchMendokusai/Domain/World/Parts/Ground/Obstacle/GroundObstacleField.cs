using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 플레이어 주변 칸의 장애물 물체 (모델과 캡슐 충돌체) 배치와 회수. 위치는 GroundObstaclePlanner 결정
	// 몸 이동은 Motor 의 캡슐 쓸기가 충돌체를 벽으로 읽어 미끄러짐 (넉백과 돌진 포함)
	public sealed class GroundObstacleField : IDisposable
	{
		private sealed class Instance
		{
			public GameObject Object;
			public int Kind;
		}

		private readonly Transform root;
		private readonly Dictionary<Vector2Int, Instance> active = new();
		private readonly List<Vector2Int> releaseBuffer = new();
		private readonly List<Stack<GameObject>> pools = new();
		private GroundObstacleRules rules;
		private float[] kindRadii = Array.Empty<float>();
		private int runSeed;
		private Vector2 start;
		private Vector2Int lastCell;
		private bool hasLastCell;

		public int ActiveCount => active.Count;
		public bool HasRules => rules != null;

		public GroundObstacleField(Transform parent)
		{
			root = new GameObject("GroundObstacles").transform;
			root.SetParent(parent, false);
		}

		// 판이 새로 시작할 때. 규칙과 시드와 시작 위치를 바꾸고 놓인 물체를 모두 거둠
		public void Reset(GroundObstacleRules newRules, int newRunSeed, Vector3 startPosition)
		{
			ReleaseAll();
			if (ReferenceEquals(rules, newRules) == false)
				DestroyPools();
			rules = newRules;
			runSeed = newRunSeed;
			start = new Vector2(startPosition.x, startPosition.z);
			kindRadii = GroundObstaclePlanner.KindRadii(newRules);
			while (pools.Count < newRules.Kinds.Length)
				pools.Add(new Stack<GameObject>());
		}

		public void Tick(Vector3 playerPosition)
		{
			if (rules == null)
				return;
			Vector2Int cell = GroundObstaclePlanner.CellOf(rules, playerPosition.x, playerPosition.z);
			if (hasLastCell && cell == lastCell)
				return;
			lastCell = cell;
			hasLastCell = true;
			int span = Mathf.CeilToInt(rules.ActiveRadius / rules.CellSize);
			bool changed = false;
			releaseBuffer.Clear();
			foreach (Vector2Int key in active.Keys)
				if (Mathf.Abs(key.x - cell.x) > span + 1 || Mathf.Abs(key.y - cell.y) > span + 1)
					releaseBuffer.Add(key);
			foreach (Vector2Int key in releaseBuffer)
			{
				Release(key);
				changed = true;
			}
			for (int x = -span; x <= span; x++)
				for (int z = -span; z <= span; z++)
				{
					Vector2Int key = cell + new Vector2Int(x, z);
					if (active.ContainsKey(key))
						continue;
					if (GroundObstaclePlanner.TryPlace(rules, kindRadii, runSeed, start, key, out GroundObstaclePlacement placement) == false)
						continue;
					active.Add(key, Create(placement));
					changed = true;
				}
			// 같은 틱의 이동 계산이 옮긴 충돌체를 보게
			if (changed)
				Physics.SyncTransforms();
		}

		public void ReleaseAll()
		{
			releaseBuffer.Clear();
			releaseBuffer.AddRange(active.Keys);
			foreach (Vector2Int key in releaseBuffer)
				Release(key);
			hasLastCell = false;
		}

		// 몹 생성 자리를 장애물 밖으로. 놓인 물체와 무관하게 배치 규칙으로 계산해 화면 밖 자리도 같은 답
		public Vector3 PushOut(Vector3 position, float bodyRadius)
		{
			if (rules == null)
				return position;
			Vector2 moved = GroundObstaclePlanner.PushOut(rules, kindRadii, runSeed, start, new Vector2(position.x, position.z), bodyRadius);
			return new Vector3(moved.x, position.y, moved.y);
		}

		private Instance Create(GroundObstaclePlacement placement)
		{
			GroundObstacleKind kind = rules.Kinds[placement.Kind];
			Stack<GameObject> pool = pools[placement.Kind];
			GameObject obstacle = pool.Count > 0 ? pool.Pop() : Build(kind);
			obstacle.transform.SetPositionAndRotation(new Vector3(placement.Position.x, 0f, placement.Position.y), Quaternion.Euler(0f, placement.Yaw, 0f));
			obstacle.transform.localScale = Vector3.one * placement.Scale;
			obstacle.SetActive(true);
			return new Instance { Object = obstacle, Kind = placement.Kind };
		}

		private GameObject Build(GroundObstacleKind kind)
		{
			GameObject obstacle = UnityEngine.Object.Instantiate(kind.Prefab, root);
			obstacle.name = kind.Name;
			if (kind.MaterialOverride != null)
				foreach (Renderer renderer in obstacle.GetComponentsInChildren<Renderer>(true))
				{
					Material[] materials = renderer.sharedMaterials;
					for (int i = 0; i < materials.Length; i++)
						materials[i] = kind.MaterialOverride;
					renderer.sharedMaterials = materials;
				}
			if (kind.ColliderRadius > 0f)
			{
				foreach (Collider existing in obstacle.GetComponentsInChildren<Collider>(true))
					UnityEngine.Object.Destroy(existing);
				CapsuleCollider capsule = obstacle.AddComponent<CapsuleCollider>();
				capsule.direction = 1;
				capsule.radius = kind.ColliderRadius;
				capsule.height = Mathf.Max(kind.ColliderHeight, kind.ColliderRadius * 2f);
				capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
			}
			obstacle.SetActive(false);
			if (kind.CameraFade && obstacle.TryGetComponent(out OcclusionFadeTarget _) == false)
				obstacle.AddComponent<OcclusionFadeTarget>();
			return obstacle;
		}

		private void Release(Vector2Int key)
		{
			Instance instance = active[key];
			active.Remove(key);
			if (instance.Object == null)
				return;
			instance.Object.SetActive(false);
			pools[instance.Kind].Push(instance.Object);
		}

		private void DestroyPools()
		{
			foreach (Stack<GameObject> pool in pools)
				foreach (GameObject obstacle in pool)
					if (obstacle != null)
						UnityEngine.Object.Destroy(obstacle);
			pools.Clear();
		}

		public void Dispose()
		{
			active.Clear();
			pools.Clear();
			if (root != null)
				UnityEngine.Object.Destroy(root.gameObject);
		}
	}
}
