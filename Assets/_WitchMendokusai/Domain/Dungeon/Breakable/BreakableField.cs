using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 주인공 주변 칸에만 부서지는 물건을 씨앗으로 배치, 멀어진 칸은 풀로 반납. 소유자 GroundGenerator
	// 칸 좌표 기반 난수라 왕복해도 같은 자리에 같은 물건. 부순 것은 이번 판 동안 다시 놓지 않음
	public sealed class BreakableField
	{
		private readonly ObjectPoolManager objectPoolManager;
		private readonly GameLogic gameLogic;
		private readonly ResourceManager resourceManager;
		private readonly Transform root;
		private readonly BreakableShardPool shards;
		private readonly Dictionary<Vector2Int, List<BreakableObject>> cells = new();
		private readonly Stack<BreakableObject> pool = new();
		private readonly HashSet<long> brokenKeys = new();
		private readonly HashSet<Vector2Int> requiredCells = new();
		private readonly List<Vector2Int> retiredCells = new();
		private readonly List<Vector3> placedPositions = new();
		// 칸 배치와 별개로 고정 자리에 둔 것 (개발자 진열장). 부수면 반납만, 부순 기록 없음
		private readonly HashSet<BreakableObject> fixedPlaced = new();
		private BreakableLibrary library;
		private Vector2Int center;
		private bool centerValid;

		public BreakableField(ObjectPoolManager objectPoolManager, GameLogic gameLogic, ResourceManager resourceManager)
		{
			this.objectPoolManager = objectPoolManager;
			this.gameLogic = gameLogic;
			this.resourceManager = resourceManager;
			root = new GameObject("Breakables").transform;
			shards = new BreakableShardPool(root);
		}

		private BreakableSettings loadedSettings;

		// Resources 의 BreakableSettings 하나. 공용 ResourceManager 대신 이 기능 폴더에 둠
		public BreakableSettings Settings
		{
			get
			{
				if (loadedSettings == null)
					loadedSettings = Resources.Load<BreakableSettings>(nameof(BreakableSettings));
				if (loadedSettings == null)
					throw new System.InvalidOperationException($"Resources/{nameof(BreakableSettings)} 없음");
				return loadedSettings;
			}
		}
		public int ActiveCount { get; private set; }
		// 부모는 그때 씬에 생겨 씬과 함께 파괴됨. 소유자 GroundGenerator 는 풀 (DontDestroyOnLoad) 의 스테이지라 남음 (월드에서 로비로)
		public bool IsRootLost => root == null;
		public int BrokenCount => brokenKeys.Count;

		public void Tick(Vector3 focus)
		{
			BreakableSettings settings = EnsureReady();
			shards.Tick(settings, Time.deltaTime);

			Vector2Int next = new(Mathf.FloorToInt(focus.x / settings.CellSize), Mathf.FloorToInt(focus.z / settings.CellSize));
			if (centerValid && next == center)
				return;
			center = next;
			centerValid = true;

			requiredCells.Clear();
			for (int x = -settings.CellRadius; x <= settings.CellRadius; x++)
				for (int z = -settings.CellRadius; z <= settings.CellRadius; z++)
					requiredCells.Add(center + new Vector2Int(x, z));

			retiredCells.Clear();
			foreach (Vector2Int cell in cells.Keys)
				if (requiredCells.Contains(cell) == false)
					retiredCells.Add(cell);
			foreach (Vector2Int cell in retiredCells)
			{
				foreach (BreakableObject breakable in cells[cell])
					Return(breakable);
				cells.Remove(cell);
			}

			foreach (Vector2Int cell in requiredCells)
				if (cells.ContainsKey(cell) == false)
					FillCell(settings, cell);
		}

		// 메시 묶음과 부모를 준비. 칸 배치 없이 고정 배치만 쓸 때도 호출
		public BreakableSettings EnsureReady()
		{
			BreakableSettings settings = Settings;
			if (library == null)
			{
				Validate(settings);
				library = BreakableLibrary.Build(settings);
			}
			if (root.gameObject.activeSelf == false)
				root.gameObject.SetActive(true);
			return settings;
		}

		// 정해진 종류와 모양을 정해진 자리에. 부서지면 IsFixed 가 false
		public BreakableObject PlaceFixed(Vector3 position, float yaw, int kindIndex, int variantIndex)
		{
			BreakableSettings settings = EnsureReady();
			BreakableKind kind = settings.Kinds[kindIndex];
			BreakableObject breakable = pool.Count > 0 ? pool.Pop() : BreakableObject.Create(this, root, library.Material);
			breakable.Place(position, yaw, Vector2Int.zero, 0L, kindIndex, variantIndex, library.Variants[kindIndex][variantIndex].Mesh, kind.Health);
			breakable.gameObject.SetActive(true);
			fixedPlaced.Add(breakable);
			ActiveCount++;
			return breakable;
		}

		public bool IsFixed(BreakableObject breakable) => breakable != null && fixedPlaced.Contains(breakable);

		public void ReturnFixed(BreakableObject breakable)
		{
			if (fixedPlaced.Remove(breakable))
				ReturnOrForget(breakable);
		}

		// 씬 정리 순서상 이미 파괴된 몸이 올 수 있음 (Play 종료 때 개발자 창 OnDestroy 의 진열장 해제). 셈만 빼고 풀에는 안 넣음
		private void ReturnOrForget(BreakableObject breakable)
		{
			if (breakable != null)
				Return(breakable);
			else
				ActiveCount--;
		}

		// 판 밖 (결과 화면) 에서는 날던 조각만 마저 떨어뜨림
		public void TickShards()
		{
			if (library != null)
				shards.Tick(Settings, Time.deltaTime);
		}

		// 설정 누락은 부술 때가 아니라 첫 배치 전에 실패
		private void Validate(BreakableSettings settings)
		{
			if (settings.CountPerCell.x < 0 || settings.CountPerCell.x > settings.CountPerCell.y)
				throw new System.InvalidOperationException($"{nameof(BreakableSettings)}.{nameof(BreakableSettings.CountPerCell)} 범위 오류 {settings.CountPerCell}");
			if (settings.NyangPrefab == null)
				throw new System.InvalidOperationException($"{nameof(BreakableSettings)}.{nameof(BreakableSettings.NyangPrefab)} 미연결");
			float totalWeight = 0f;
			foreach (BreakableKind kind in settings.Kinds)
			{
				totalWeight += kind.Weight;
				int required = BreakableShapes.RequiredPalette(kind.Shape);
				if (kind.Palette.Length < required)
					throw new System.InvalidOperationException($"{nameof(BreakableKind)} {kind.Name} 팔레트 {kind.Palette.Length}칸, {kind.Shape} 는 {required}칸 이상");
				if (kind.Table == null)
					throw new System.InvalidOperationException($"{nameof(BreakableKind)} {kind.Name} {nameof(BreakableKind.Table)} 미연결");
				if (kind.Table.TotalWeight <= 0f)
					throw new System.InvalidOperationException($"{kind.Table.name} 가중치 합 0");
				foreach (BreakableDrop drop in kind.Table.Drops)
					if (drop.Type != BreakableDropType.Nyang && drop.Type != BreakableDropType.Exp && ItemPrefab(drop.Type) == null)
						throw new System.InvalidOperationException($"{kind.Table.name}: {drop.Type} 프리팹이 {nameof(ResourceManager)} 에 없음");
			}
			if (totalWeight <= 0f)
				throw new System.InvalidOperationException($"{nameof(BreakableSettings)}.{nameof(BreakableSettings.Kinds)} 가중치 합 0");
		}

		private GameItemObject ItemPrefab(BreakableDropType type) => type switch
		{
			BreakableDropType.Heal => resourceManager.HealObjectPrefab,
			BreakableDropType.Magnet => resourceManager.MagnetObjectPrefab,
			BreakableDropType.Freeze => resourceManager.FreezeObjectPrefab,
			BreakableDropType.ClearScreen => resourceManager.ClearScreenObjectPrefab,
			_ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
		};

		private void FillCell(BreakableSettings settings, Vector2Int cell)
		{
			List<BreakableObject> list = new();
			cells[cell] = list;
			// 판 시드에서 갈라 쓴 값 + 칸 좌표. 같은 판 왕복은 같은 배치, 판이 바뀌면 다른 배치
			System.Random random = new(unchecked(RunSeed.Derive(settings.Seed) ^ cell.x * 73856093 ^ cell.y * 19349663));
			int count = random.Next(settings.CountPerCell.x, settings.CountPerCell.y + 1);
			float totalWeight = 0f;
			foreach (BreakableKind kind in settings.Kinds)
				totalWeight += kind.Weight;
			placedPositions.Clear();
			for (int index = 0; index < count; index++)
			{
				// 난수 소비 순서를 고정해 부순 자리와 아닌 자리가 섞여도 같은 배치
				float usable = settings.CellSize - settings.CellMargin * 2f;
				Vector3 position = new(
					cell.x * settings.CellSize + settings.CellMargin + (float)random.NextDouble() * usable,
					settings.GroundHeight,
					cell.y * settings.CellSize + settings.CellMargin + (float)random.NextDouble() * usable);
				float yaw = (float)random.NextDouble() * 360f;
				int kindIndex = PickKind(settings, (float)random.NextDouble() * totalWeight);
				int variantIndex = random.Next(settings.VariantsPerKind);
				long key = ((long)cell.x << 40) ^ ((long)(cell.y & 0xFFFFF) << 20) ^ index;

				if (new Vector2(position.x, position.z).sqrMagnitude < settings.ClearRadius * settings.ClearRadius)
					continue;
				if (TooClose(position, settings.MinSpacing))
					continue;
				placedPositions.Add(position);
				if (brokenKeys.Contains(key))
					continue;

				BreakableObject breakable = pool.Count > 0 ? pool.Pop() : BreakableObject.Create(this, root, library.Material);
				BreakableKind kind = settings.Kinds[kindIndex];
				breakable.Place(position, yaw, cell, key, kindIndex, variantIndex, library.Variants[kindIndex][variantIndex].Mesh, kind.Health);
				breakable.gameObject.SetActive(true);
				list.Add(breakable);
				ActiveCount++;
			}
		}

		private static int PickKind(BreakableSettings settings, float roll)
		{
			for (int i = 0; i < settings.Kinds.Length; i++)
			{
				roll -= settings.Kinds[i].Weight;
				if (roll < 0f)
					return i;
			}
			return settings.Kinds.Length - 1;
		}

		private bool TooClose(Vector3 position, float spacing)
		{
			foreach (Vector3 placed in placedPositions)
				if ((placed - position).sqrMagnitude < spacing * spacing)
					return true;
			return false;
		}

		private void Return(BreakableObject breakable)
		{
			breakable.gameObject.SetActive(false);
			pool.Push(breakable);
			ActiveCount--;
		}

		public void OnHit(BreakableObject breakable)
		{
			if (breakable.IsBroken == false)
				CombatSfx.Play(Settings.HitSfx, breakable.transform.position);
		}

		public void Break(BreakableObject breakable)
		{
			BreakableSettings settings = Settings;
			BreakableKind kind = settings.Kinds[breakable.KindIndex];
			Bounds bounds = breakable.LocalBounds;
			Vector3 worldCenter = breakable.transform.TransformPoint(bounds.center);
			shards.Burst(settings, library.Material, library.Variants[breakable.KindIndex][breakable.VariantIndex].ShardMeshes, worldCenter, bounds.size, breakable.HitDirection);
			CombatSfx.Play(settings.BreakSfx, worldCenter);

			if (fixedPlaced.Remove(breakable) == false)
			{
				brokenKeys.Add(breakable.Key);
				cells[breakable.Cell].Remove(breakable);
			}
			Return(breakable);
			Drop(settings, kind.Table, breakable.transform.position);
		}

		private void Drop(BreakableSettings settings, BreakableTable table, Vector3 position)
		{
			int pick = table.Pick(Random.value, Random.value);
			if (pick < 0)
				return;
			BreakableDrop drop = table.Drops[pick];
			int count = Random.Range(drop.Count.x, drop.Count.y + 1);
			for (int i = 0; i < count; i++)
			{
				switch (drop.Type)
				{
					case BreakableDropType.Nyang:
						SpawnNyang(settings, table, position);
						break;
					case BreakableDropType.Heal:
					case BreakableDropType.Magnet:
					case BreakableDropType.Freeze:
					case BreakableDropType.ClearScreen:
						objectPoolManager.Spawn(ItemPrefab(drop.Type).gameObject, GameLogic.GetRandomSpawnPosOffset(position)).SetActive(true);
						break;
					case BreakableDropType.Exp:
						gameLogic.SpawnExpOrb(position);
						break;
					default:
						throw new System.ArgumentOutOfRangeException(nameof(drop.Type), drop.Type, null);
				}
			}
		}

		private void SpawnNyang(BreakableSettings settings, BreakableTable table, Vector3 position)
		{
			GameObject coin = objectPoolManager.Spawn(settings.NyangPrefab.gameObject, GameLogic.GetRandomSpawnPosOffset(position));
			coin.GetComponent<NyangObject>().Init(table.NyangPerCoin);
			coin.SetActive(true);
		}

		// 판 시작과 종료. 칸과 조각을 비우고 부순 기록도 지움
		public void ReleaseAll()
		{
			centerValid = false;
			brokenKeys.Clear();
			// 장면 해제 중 root 가 먼저 사라졌으면 자식도 함께 사라짐
			if (root == null)
			{
				cells.Clear();
				fixedPlaced.Clear();
				pool.Clear();
				ActiveCount = 0;
				return;
			}
			foreach (List<BreakableObject> list in cells.Values)
				foreach (BreakableObject breakable in list)
					ReturnOrForget(breakable);
			cells.Clear();
			foreach (BreakableObject breakable in fixedPlaced)
				ReturnOrForget(breakable);
			fixedPlaced.Clear();
			shards.Clear();
			root.gameObject.SetActive(false);
		}

		public void Dispose()
		{
			library?.Destroy();
			library = null;
			if (root != null)
				Object.Destroy(root.gameObject);
		}
	}
}
