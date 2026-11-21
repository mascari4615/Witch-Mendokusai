using UnityEngine;

namespace WitchMendokusai
{
	public readonly struct GroundObstaclePlacement
	{
		public readonly Vector2Int Cell;
		public readonly Vector2 Position;
		public readonly float Yaw;
		public readonly float Scale;
		public readonly int Kind;
		// 충돌 반지름 (m, 배율 반영)
		public readonly float Radius;

		public GroundObstaclePlacement(Vector2Int cell, Vector2 position, float yaw, float scale, int kind, float radius)
		{
			Cell = cell; Position = position; Yaw = yaw; Scale = scale; Kind = kind; Radius = radius;
		}
	}

	// 칸 하나의 장애물을 좌표와 판 시드만으로 정함. 청크 왕복과 생성 순서에 무관하고 전투 난수 상태 보존
	// 간격 규칙: 점은 칸 가장자리에서 MinSpacing / 2 안쪽 사각형에만 놓여 이웃 칸 점과 MinSpacing 이상 떨어짐
	public static class GroundObstaclePlanner
	{
		public static Vector2Int CellOf(GroundObstacleRules rules, float x, float z) =>
			new(Mathf.FloorToInt(x / rules.CellSize), Mathf.FloorToInt(z / rules.CellSize));

		public static float[] KindRadii(GroundObstacleRules rules)
		{
			float[] radii = new float[rules.Kinds.Length];
			for (int i = 0; i < radii.Length; i++)
				radii[i] = rules.Kinds[i].ColliderRadius > 0f ? rules.Kinds[i].ColliderRadius : GroundObstacleRules.PrefabRadius(rules.Kinds[i]);
			return radii;
		}

		public static bool TryPlace(GroundObstacleRules rules, float[] kindRadii, int runSeed, Vector2 start, Vector2Int cell, out GroundObstaclePlacement placement)
		{
			placement = default;
			// 판정과 무관하게 난수를 같은 순서로 소비해 규칙을 바꿔도 다른 칸이 연쇄로 바뀌지 않음
			System.Random random = new(unchecked(runSeed ^ rules.SeedSalt * 83492791 ^ cell.x * 73856093 ^ cell.y * 19349663));
			double chance = random.NextDouble();
			double px = random.NextDouble();
			double pz = random.NextDouble();
			double kindRoll = random.NextDouble();
			double yaw = random.NextDouble();
			double scaleRoll = random.NextDouble();
			if (chance >= rules.Probability)
				return false;
			float inner = rules.CellSize - rules.MinSpacing;
			Vector2 position = new(
				cell.x * rules.CellSize + rules.MinSpacing * .5f + (float)px * inner,
				cell.y * rules.CellSize + rules.MinSpacing * .5f + (float)pz * inner);
			if ((position - start).sqrMagnitude < rules.StartClearRadius * rules.StartClearRadius)
				return false;
			if (InPath(rules, runSeed, position))
				return false;
			int kind = PickKind(rules, kindRoll);
			if (kind < 0)
				return false;
			float scale = Mathf.Lerp(rules.Kinds[kind].ScaleRange.x, rules.Kinds[kind].ScaleRange.y, (float)scaleRoll);
			placement = new GroundObstaclePlacement(cell, position, (float)yaw * 360f, scale, kind, kindRadii[kind] * scale);
			return true;
		}

		// 길: 저주파 무늬 값이 0.5 근처인 띠. 등고선이라 끊기지 않고 굽은 길이 됨
		public static bool InPath(GroundObstacleRules rules, int runSeed, Vector2 position)
		{
			if (rules.PathHalfWidth <= 0f)
				return false;
			System.Random offsetRandom = new(unchecked(runSeed ^ rules.SeedSalt * 2654435));
			float offsetX = (float)offsetRandom.NextDouble() * 1000f;
			float offsetZ = (float)offsetRandom.NextDouble() * 1000f;
			float value = Mathf.PerlinNoise(position.x / rules.PathNoiseScale + offsetX, position.y / rules.PathNoiseScale + offsetZ);
			return Mathf.Abs(value - .5f) < rules.PathHalfWidth;
		}

		private static int PickKind(GroundObstacleRules rules, double roll)
		{
			float total = 0f;
			foreach (GroundObstacleKind kind in rules.Kinds)
				total += kind.Weight;
			if (total <= 0f)
				return -1;
			float target = (float)roll * total;
			for (int i = 0; i < rules.Kinds.Length; i++)
			{
				target -= rules.Kinds[i].Weight;
				if (target < 0f)
					return i;
			}
			return rules.Kinds.Length - 1;
		}

		// 반지름 radius 원 + 여유가 장애물 가장자리와 겹치는 가장 가까운 장애물. 칸 계산만이라 물리와 생성 상태에 무관
		public static bool TryNearest(GroundObstacleRules rules, float[] kindRadii, int runSeed, Vector2 start, Vector2 position, float reach, out GroundObstaclePlacement nearest)
		{
			nearest = default;
			float maxRadius = 0f;
			for (int i = 0; i < rules.Kinds.Length; i++)
				maxRadius = Mathf.Max(maxRadius, kindRadii[i] * rules.MaxScale(rules.Kinds[i]));
			int span = Mathf.CeilToInt((reach + maxRadius) / rules.CellSize) + 1;
			Vector2Int center = CellOf(rules, position.x, position.y);
			float best = float.PositiveInfinity;
			bool found = false;
			for (int x = -span; x <= span; x++)
				for (int z = -span; z <= span; z++)
				{
					if (TryPlace(rules, kindRadii, runSeed, start, center + new Vector2Int(x, z), out GroundObstaclePlacement placement) == false)
						continue;
					float edge = Vector2.Distance(placement.Position, position) - placement.Radius;
					if (edge < reach && edge < best)
					{
						best = edge;
						nearest = placement;
						found = true;
					}
				}
			return found;
		}

		// 자리를 장애물 밖으로 밀어냄. 세 번 돌려 이웃 장애물에 걸친 자리도 풀림
		public static Vector2 PushOut(GroundObstacleRules rules, float[] kindRadii, int runSeed, Vector2 start, Vector2 position, float bodyRadius)
		{
			float clearance = bodyRadius + rules.SpawnClearance;
			for (int pass = 0; pass < 3; pass++)
			{
				if (TryNearest(rules, kindRadii, runSeed, start, position, clearance, out GroundObstaclePlacement placement) == false)
					return position;
				Vector2 away = position - placement.Position;
				if (away.sqrMagnitude < .0001f)
					away = Vector2.right;
				position = placement.Position + away.normalized * (placement.Radius + clearance);
			}
			return position;
		}
	}
}
