using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥 액체 격자. 칸마다 종류와 양, 불과 감전 상태
	/// - 쏟기 (Spill): 원 안 칸을 채움. 다른 종류 위면 덮어씀
	/// - 불 (Ignite): 불붙는 칸이 타고 이웃 불붙는 칸으로 번짐. 다 타면 칸이 사라지고 그을음
	/// - 감전 (Shock): 전기가 통하는 칸에서 이어진 같은 웅덩이 전체로 즉시 퍼짐. 나중에 쏟은 칸에는 안 남음
	/// - 밟은 몬스터와 주인공에게 간격마다 피해, 상태, 느려짐
	/// - 그림은 GoopField.Render (낮은 해상도 텍스처 두 장과 바닥 판)
	/// </summary>
	public sealed partial class GoopField : IDamageSource
	{
		private sealed class Cell
		{
			public GoopKind Kind;
			public float Amount;
			public float Age;
			public float BurnLeft;
			public float SpreadIn;
			public float ShockLeft;
		}

		private static readonly Vector2Int[] NEIGHBORS = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

		private readonly RemainsSettings settings;
		private readonly Dictionary<Vector2Int, Cell> cells = new();
		private readonly List<Vector2Int> removed = new();
		private readonly List<Vector2Int> igniteNext = new();
		private readonly Stack<Vector2Int> floodStack = new();
		private readonly HashSet<Vector2Int> floodSeen = new();
		private float effectTimer;

		public int CellCount => cells.Count;

		public GoopField(Transform parent, RemainsSettings settings)
		{
			this.settings = settings;
			CreateRenderer(parent);
		}

		private Vector2Int CellOf(Vector3 position) => new(Mathf.FloorToInt(position.x / settings.GoopCellSize), Mathf.FloorToInt(position.z / settings.GoopCellSize));

		private Vector3 CenterOf(Vector2Int cell, float groundHeight) => new((cell.x + .5f) * settings.GoopCellSize, groundHeight, (cell.y + .5f) * settings.GoopCellSize);

		public void Spill(GoopKind kind, Vector3 center, float radius, float amount)
		{
			Vector2Int middle = CellOf(center);
			int reach = Mathf.CeilToInt(radius / settings.GoopCellSize);
			float radiusSquared = radius * radius;
			for (int x = -reach; x <= reach; x++)
			{
				for (int z = -reach; z <= reach; z++)
				{
					Vector2Int key = middle + new Vector2Int(x, z);
					Vector3 offset = CenterOf(key, center.y) - center;
					float distanceSquared = offset.x * offset.x + offset.z * offset.z;
					if (distanceSquared > radiusSquared)
						continue;
					// 가장자리는 옅게
					float edge = 1f - Mathf.Sqrt(distanceSquared) / Mathf.Max(radius, .0001f);
					float filled = amount * Mathf.Lerp(.5f, 1f, edge);
					if (cells.TryGetValue(key, out Cell cell))
					{
						if (cell.BurnLeft > 0f)
							continue;
						if (cell.Kind != kind)
						{
							cell.Kind = kind;
							cell.ShockLeft = -settings.ShockCooldownSeconds;
						}
						cell.Amount = Mathf.Max(cell.Amount, filled);
						cell.Age = 0f;
						continue;
					}
					if (cells.Count >= settings.MaxGoopCells)
						return;
					cells.Add(key, new Cell { Kind = kind, Amount = filled, ShockLeft = -settings.ShockCooldownSeconds });
				}
			}
			MarkDirty();
		}

		public void Ignite(Vector3 center, float radius)
		{
			Vector2Int middle = CellOf(center);
			int reach = Mathf.CeilToInt(radius / settings.GoopCellSize);
			for (int x = -reach; x <= reach; x++)
				for (int z = -reach; z <= reach; z++)
					if (cells.TryGetValue(middle + new Vector2Int(x, z), out Cell cell))
						StartBurning(cell);
		}

		private void StartBurning(Cell cell)
		{
			if (cell.Kind.Flammable == false || cell.BurnLeft > 0f)
				return;
			cell.BurnLeft = settings.FireBurnSeconds;
			cell.SpreadIn = settings.FireSpreadSeconds;
			MarkDirty();
		}

		// 닿은 칸에서 이어진 전기 통하는 칸 전체
		public void Shock(Vector3 center, float radius)
		{
			Vector2Int middle = CellOf(center);
			int reach = Mathf.CeilToInt(radius / settings.GoopCellSize);
			floodSeen.Clear();
			for (int x = -reach; x <= reach; x++)
			{
				for (int z = -reach; z <= reach; z++)
				{
					Vector2Int start = middle + new Vector2Int(x, z);
					if (floodSeen.Contains(start) || cells.TryGetValue(start, out Cell cell) == false || cell.Kind.Conductive == false)
						continue;
					Flood(start);
				}
			}
			MarkDirty();
		}

		private void Flood(Vector2Int start)
		{
			floodStack.Clear();
			floodStack.Push(start);
			floodSeen.Add(start);
			while (floodStack.Count > 0)
			{
				Vector2Int key = floodStack.Pop();
				Cell shocked = cells[key];
				if (shocked.ShockLeft <= -settings.ShockCooldownSeconds)
					shocked.ShockLeft = settings.ShockSeconds;
				foreach (Vector2Int step in NEIGHBORS)
				{
					Vector2Int next = key + step;
					if (floodSeen.Contains(next) || cells.TryGetValue(next, out Cell neighbor) == false || neighbor.Kind.Conductive == false)
						continue;
					floodSeen.Add(next);
					floodStack.Push(next);
				}
			}
		}

		public void Tick(float deltaTime, Vector3 focus, float groundHeight, PlayerObject player)
		{
			removed.Clear();
			igniteNext.Clear();
			foreach (KeyValuePair<Vector2Int, Cell> pair in cells)
			{
				Cell cell = pair.Value;
				// 감전이 끝난 뒤에도 음수로 내려가며 다시 감전될 때까지 기다림 (ShockCooldownSeconds)
				if (cell.ShockLeft > -settings.ShockCooldownSeconds)
					cell.ShockLeft -= deltaTime;
				if (cell.BurnLeft > 0f)
				{
					TickBurning(pair.Key, cell, deltaTime, groundHeight);
					continue;
				}
				cell.Age += deltaTime;
				if (cell.Age >= cell.Kind.DrySeconds)
					removed.Add(pair.Key);
			}
			foreach (Vector2Int key in igniteNext)
				if (cells.TryGetValue(key, out Cell neighbor))
					StartBurning(neighbor);
			foreach (Vector2Int key in removed)
				cells.Remove(key);

			effectTimer += deltaTime;
			if (effectTimer >= settings.GoopEffectInterval)
			{
				ApplyEffects(effectTimer, player);
				effectTimer = 0f;
			}
			TickRender(deltaTime, focus, groundHeight);
		}

		private void TickBurning(Vector2Int key, Cell cell, float deltaTime, float groundHeight)
		{
			cell.BurnLeft -= deltaTime;
			if (cell.SpreadIn > 0f)
			{
				cell.SpreadIn -= deltaTime;
				if (cell.SpreadIn <= 0f)
					foreach (Vector2Int step in NEIGHBORS)
						igniteNext.Add(key + step);
			}
			if (cell.BurnLeft > 0f)
				return;
			removed.Add(key);
			RemainsWorld.Current?.Stain(CenterOf(key, groundHeight), settings.ScorchRadius, settings.ScorchColor);
		}

		// 칸 위 유닛 하나에 이번 간격 몫. 몬스터 수가 많아도 칸 찾기는 사전 한 번
		private void ApplyEffects(float interval, PlayerObject player)
		{
			if (cells.Count == 0)
				return;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
			{
				GameObject monsterObject = monsters[i];
				if (monsterObject.activeInHierarchy == false || cells.TryGetValue(CellOf(monsterObject.transform.position), out Cell cell) == false)
					continue;
				if (monsterObject.TryGetComponent(out MonsterObject monster) == false)
					continue;
				SpreadFromStatus(monster, monsterObject.transform.position, cell);
				ApplyCell(monster, cell, interval, cell.Kind.MonsterDamagePerSecond, settings.FireMonsterDamagePerSecond, settings.ShockMonsterDamage);
			}
			if (player != null && cells.TryGetValue(CellOf(player.transform.position), out Cell playerCell))
				ApplyCell(player, playerCell, interval, playerCell.Kind.PlayerDamagePerSecond, settings.FirePlayerDamagePerSecond, settings.ShockPlayerDamage);
		}

		// 불붙은 몬스터가 기름에 들어오면 불, 감전된 몬스터가 물에 들어오면 감전
		private void SpreadFromStatus(MonsterObject monster, Vector3 position, Cell cell)
		{
			if (cell.Kind.Flammable && cell.BurnLeft <= 0f && UnitStatus.Has(monster, UnitStatusType.Burn))
				StartBurning(cell);
			if (cell.Kind.Conductive && cell.ShockLeft <= -settings.ShockCooldownSeconds && UnitStatus.Has(monster, UnitStatusType.Shock))
				Shock(position, 0f);
		}

		private void ApplyCell(UnitObject unit, Cell cell, float interval, int kindDamagePerSecond, int fireDamagePerSecond, int shockDamage)
		{
			if (cell.BurnLeft > 0f)
			{
				Hurt(unit, Mathf.CeilToInt(fireDamagePerSecond * interval), UnitStatusType.Burn);
				return;
			}
			if (cell.ShockLeft > 0f)
			{
				Hurt(unit, shockDamage, UnitStatusType.Shock);
				// 한 번 맞으면 그 칸의 감전은 끝 (같은 감전에 여러 번 맞지 않게), 쉬는 시간 시작
				cell.ShockLeft = 0f;
				MarkDirty();
			}
			if (kindDamagePerSecond > 0)
				Hurt(unit, Mathf.CeilToInt(kindDamagePerSecond * interval), UnitStatusType.Poison);
			if (cell.Kind.MoveSpeedMultiplier < 1f)
				UnitStatus.ApplySlow(unit, this, settings.GoopSlowLingerSeconds, 1f - cell.Kind.MoveSpeedMultiplier);
		}

		private void Hurt(UnitObject unit, int damage, UnitStatusType statusType)
		{
			if (damage <= 0)
				return;
			unit.ReceiveDamage(new DamageInfo
			{
				type = DamageType.StatusTick,
				statusType = statusType,
				damage = damage,
				damageSource = this,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			});
		}

		public void Clear()
		{
			cells.Clear();
			effectTimer = 0f;
			MarkDirty();
		}
	}
}
