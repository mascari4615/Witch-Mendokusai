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
		private readonly Dictionary<UnitObject, float> damageCarry = new();
		private float effectTimer;
		private PlayerObject player;

		// 다 탄 칸의 그을음. RemainsWorld 가 자국 캔버스로 이음
		public event System.Action<Vector3, float, Color> OnScorch = delegate { };

		public int CellCount => cells.Count;

		public GoopField(Transform parent, RemainsSettings settings)
		{
			this.settings = settings;
			CreateRenderer(parent);
			CreateParticles(parent);
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
					float filled = amount * Mathf.Lerp(settings.GoopEdgeAmount, 1f, edge);
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
					{
						MarkDirty();
						return;
					}
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

		// 닿은 칸에서 이어진 전기 통하는 칸 전체. 피해는 퍼지는 순간 한 번 (그 웅덩이 위 유닛마다), 빛은 ShockSeconds 동안
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
			if (floodSeen.Count > 0)
				HurtShocked();
			MarkDirty();
		}

		private void HurtShocked()
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
			{
				GameObject monsterObject = monsters[i];
				if (monsterObject.activeInHierarchy && floodSeen.Contains(CellOf(monsterObject.transform.position)) && monsterObject.TryGetComponent(out MonsterObject monster))
					HurtMonster(monster, settings.ShockMonsterDamage, UnitStatusType.Shock);
			}
			if (player != null && floodSeen.Contains(CellOf(player.transform.position)))
				HurtPlayer(player, settings.ShockPlayerDamage);
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

		public void Tick(float deltaTime, Vector3 focus, float groundHeight, PlayerObject currentPlayer)
		{
			player = currentPlayer;
			removed.Clear();
			igniteNext.Clear();
			burningCells.Clear();
			shockedCells.Clear();
			foreach (KeyValuePair<Vector2Int, Cell> pair in cells)
			{
				Cell cell = pair.Value;
				if (cell.ShockLeft > 0f)
					shockedCells.Add(pair.Key);
				if (cell.BurnLeft > 0f)
					burningCells.Add(pair.Key);
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
				ApplyEffects(effectTimer);
				effectTimer = 0f;
			}
			TickRender(deltaTime, focus, groundHeight);
			TickParticles(deltaTime, groundHeight);
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
			OnScorch(CenterOf(key, groundHeight), settings.ScorchRadius, settings.ScorchColor);
		}

		// 칸 위 유닛 하나에 이번 간격 몫. 몬스터 수가 많아도 칸 찾기는 사전 한 번
		private void ApplyEffects(float interval)
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
				SpreadFromStatus(monster, cell);
				if (cell.BurnLeft > 0f)
					HurtMonster(monster, Accrue(monster, settings.FireMonsterDamagePerSecond * interval), UnitStatusType.Burn);
				else if (cell.Kind.MonsterDamagePerSecond > 0)
					HurtMonster(monster, Accrue(monster, cell.Kind.MonsterDamagePerSecond * interval), UnitStatusType.Poison);
				Slow(monster, cell);
			}
			if (player != null && cells.TryGetValue(CellOf(player.transform.position), out Cell playerCell))
			{
				if (playerCell.BurnLeft > 0f)
					HurtPlayer(player, Accrue(player, settings.FirePlayerDamagePerSecond * interval));
				else if (playerCell.Kind.PlayerDamagePerSecond > 0)
					HurtPlayer(player, Accrue(player, playerCell.Kind.PlayerDamagePerSecond * interval));
				Slow(player, playerCell);
			}
		}

		// 불붙은 몬스터가 기름에 들어오면 불. 감전은 상태로 번지지 않음 (감전된 몬스터 하나가 웅덩이를 계속 감전시키는 고리 방지)
		private void SpreadFromStatus(MonsterObject monster, Cell cell)
		{
			if (cell.Kind.Flammable && cell.BurnLeft <= 0f && UnitStatus.Has(monster, UnitStatusType.Burn))
				StartBurning(cell);
		}

		private void Slow(UnitObject unit, Cell cell)
		{
			if (cell.BurnLeft <= 0f && cell.Kind.MoveSpeedMultiplier < 1f)
				UnitStatus.ApplySlow(unit, this, settings.GoopSlowLingerSeconds, 1f - cell.Kind.MoveSpeedMultiplier);
		}

		// 초당 피해를 간격으로 나눈 소수 몫을 유닛마다 모아 정수가 될 때만 줌 (작은 값이 부풀지 않게)
		private int Accrue(UnitObject unit, float amount)
		{
			damageCarry.TryGetValue(unit, out float carried);
			carried += amount;
			int whole = Mathf.FloorToInt(carried);
			damageCarry[unit] = carried - whole;
			return whole;
		}

		// 몬스터는 상태이상 틱 (무적 무시, 상태 숫자)
		private void HurtMonster(MonsterObject monster, int damage, UnitStatusType statusType)
		{
			if (damage <= 0)
				return;
			monster.ReceiveDamage(new DamageInfo
			{
				type = DamageType.StatusTick,
				statusType = statusType,
				damage = damage,
				damageSource = this,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			});
		}

		// 주인공은 보통 피해 (피격 무적과 클리어 무적이 막음)
		private void HurtPlayer(PlayerObject target, int damage)
		{
			if (damage <= 0)
				return;
			target.ReceiveDamage(new DamageInfo
			{
				type = DamageType.Normal,
				damage = damage,
				damageSource = this,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			});
		}

		public void Clear()
		{
			cells.Clear();
			damageCarry.Clear();
			effectTimer = 0f;
			ClearParticles();
			MarkDirty();
		}
	}
}
