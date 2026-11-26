using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public enum ScatterArrangement
	{
		// 시전자 앞에서 주인공 쪽으로 한 줄
		Line = 0,
		// 시전자 둘레 고리
		Ring = 1,
		// 주인공 자리 하나와 그 둘레
		Scatter = 2,
		// 주인공 둘레만. 가운데가 안전
		Around = 3,
		// 주인공 뒤를 막는 벽. 시전자 쪽에서 본 주인공 너머에 가로로 한 줄
		Wall = 4,
		// 주인공 자리를 지나는 가로줄. 시전자 쪽에서 본 주인공 옆으로, 줄 수만큼 앞뒤로 겹침 (빈칸 하나 벽)
		Across = 5,
	}

	// 줄에서 비우는 칸. 빈칸은 안전 자리로 밝게 표시
	public enum ScatterGap
	{
		None = 0,
		Random = 1,
		// 양 끝을 뺀 안쪽 칸 가운데 주인공에서 가장 먼 칸. 같으면 그 가운데 무작위. 끝을 비우면 짧은 벽일 뿐이라 안쪽만
		FarFromPlayer = 2,
	}

	// 여러 자리 장판. 준비를 시작한 순간 자리를 모두 고정, 예고가 찬 원 안에 있으면 피해 한 번 (자리마다 늦추면 때마다 한 번)
	[CreateAssetMenu(fileName = "MA_Scatter", menuName = "WM/Variable/MonsterAttack/" + nameof(ScatterZoneMonsterAttack))]
	public class ScatterZoneMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "여러 자리 장판";

		[field: Header("여러 자리")]
		[field: SerializeField] public ScatterArrangement Arrangement { get; private set; } = ScatterArrangement.Scatter;
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 5;
		[field: SerializeField, Tooltip("원 반지름 (m)"), Min(0.3f)] public float Radius { get; private set; } = 1.2f;
		[field: SerializeField, Tooltip("줄과 벽은 칸 간격, 고리와 흩뿌림은 중심에서 거리 (m). 벽은 주인공에서 벽까지도 같은 거리 + 흔듦"), Min(0.1f)] public float Distance { get; private set; } = 2.6f;
		[field: SerializeField, Tooltip("고리와 흩뿌림. 거리를 이만큼까지 더 멀리 흔듦 (m)"), Min(0f)] public float DistanceJitter { get; private set; }
		[field: SerializeField, Tooltip("자리마다 늦어지는 초. 0 이면 동시"), Min(0f)] public float StaggerSeconds { get; private set; }
		[field: SerializeField, Tooltip("터질 때 기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();
		[field: SerializeField, Tooltip("줄, 벽, 가로줄만. 한 칸 (가로줄은 한 세로 줄) 을 비워 안전 자리로 밝게")] public ScatterGap Gap { get; private set; } = ScatterGap.None;
		[field: SerializeField, Tooltip("가로줄만. 앞뒤로 겹친 줄 수 (간격은 Distance). 한 줄이면 앞뒤로 비켜도 피해져 빈칸이 뜻이 없음"), Min(1)] public int Rows { get; private set; } = 1;

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			if (Arrangement == ScatterArrangement.Across)
				ArrangeAcross(unitObject.transform.position, playerPosition, Count, Rows, Distance, results);
			else
				Arrange(Arrangement, unitObject.transform.position, playerPosition, Count, Distance, DistanceJitter, Random.value * 360f, () => Random.value, results);
		}

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results, List<Vector3> safeSpots)
		{
			int first = results.Count;
			ResolveTargets(unitObject, playerPosition, moveVelocity, results);
			if (Gap != ScatterGap.None && results.Count - first >= 2)
				TakeGap(results, first, Arrangement == ScatterArrangement.Across ? Rows : 1, playerPosition, Gap, Random.value, safeSpots);
		}

		// 빈칸 하나 (rows 칸씩 묶인 세로 줄 하나) 를 공격 자리에서 빼 안전 자리로. 묶음 순서는 ArrangeAcross 와 같음
		public static void TakeGap(List<Vector3> results, int first, int rows, Vector3 player, ScatterGap gap, float random01, List<Vector3> safeSpots)
		{
			List<Vector3> columns = new();
			int columnCount = (results.Count - first) / rows;
			for (int i = 0; i < columnCount; i++)
				columns.Add(results[first + i * rows + rows / 2]);
			int start = first + PickGap(columns, 0, player, gap, random01) * rows;
			for (int row = 0; row < rows; row++)
				safeSpots.Add(results[start + row]);
			results.RemoveRange(start, rows);
		}

		// 세로 줄마다 rows 칸 (뒤에서 앞으로) 을 이어 붙임. 가운데 줄이 주인공 자리를 지남
		public static void ArrangeAcross(Vector3 caster, Vector3 player, int count, int rows, float distance, List<Vector3> results)
		{
			Vector3 away = FlatDirection(caster, player);
			Vector3 across = Vector3.Cross(Vector3.up, away);
			for (int i = 0; i < count; i++)
			{
				for (int row = 0; row < rows; row++)
					results.Add(player + across * (distance * (i - (count - 1) * 0.5f)) + away * (distance * (row - (rows - 1) * 0.5f)));
			}
		}

		protected override bool HasSafeSpots => true;

		// 빈칸 원은 이웃 원 가장자리까지 (간격 - 반지름). 몸 전체가 이 원 안이면 안전
		public float GapRadius => Distance - Radius;

		public override CombatFxContext SafeContext(UnitObject unitObject, Vector3 spot) => new()
		{
			Position = spot,
			Radius = GapRadius,
			Seconds = WindupSeconds + StaggerSeconds * Mathf.Max(0, Count * Rows - Rows - 1),
		};

		// 비울 칸의 차례 (first 부터 센 값). random01 은 0~1 (시험에서 고정)
		public static int PickGap(List<Vector3> slots, int first, Vector3 player, ScatterGap gap, float random01)
		{
			int count = slots.Count - first;
			int pick = Mathf.Min(count - 1, Mathf.FloorToInt(random01 * count));
			if (gap != ScatterGap.FarFromPlayer)
				return pick;
			// 세 칸 이상이면 양 끝은 후보에서 뺌
			int from = count >= 3 ? 1 : 0;
			int to = count >= 3 ? count - 1 : count;
			float farthest = 0f;
			for (int i = from; i < to; i++)
				farthest = Mathf.Max(farthest, FlatDistance(slots[first + i], player));
			// 같은 거리면 그 가운데 무작위
			int ties = 0;
			for (int i = from; i < to; i++)
				ties += IsFarthest(slots[first + i], player, farthest) ? 1 : 0;
			int wanted = Mathf.Min(ties - 1, Mathf.FloorToInt(random01 * ties));
			for (int i = from; i < to; i++)
			{
				if (IsFarthest(slots[first + i], player, farthest) && wanted-- == 0)
					return i;
			}
			return pick;
		}

		private static bool IsFarthest(Vector3 slot, Vector3 player, float farthest) => FlatDistance(slot, player) >= farthest - SlimeKingPatternMath.DIRECTION_EPSILON_SQR;

		private static float FlatDistance(Vector3 a, Vector3 b)
		{
			Vector3 offset = a - b;
			offset.y = 0f;
			return offset.magnitude;
		}

		public override float StrikeDelay(int index) => StaggerSeconds * index;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index) => new()
		{
			Position = target,
			Radius = Radius,
			Seconds = WindupSeconds,
		};

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			if (player.IsAlive == false || SlimeKingPatternMath.IsWithin(target, player.transform.position, Radius + player.UnitMovement.BodyRadius) == false)
				return false;
			HitEffect.Strike(unitObject, target, player, MakeDamage(unitObject, Damage));
			return true;
		}

		// 배치 계산만. 난수는 0~1 을 밖에서 받아 시험에서 고정
		public static void Arrange(ScatterArrangement arrangement, Vector3 caster, Vector3 player, int count, float distance, float jitter, float startAngle, Func<float> random01, List<Vector3> results)
		{
			switch (arrangement)
			{
				case ScatterArrangement.Line:
					Vector3 toPlayer = FlatDirection(caster, player);
					for (int i = 0; i < count; i++)
						results.Add(caster + toPlayer * (distance * (i + 1)));
					return;
				case ScatterArrangement.Ring:
					AddRing(caster, count, distance, jitter, startAngle, random01, results);
					return;
				case ScatterArrangement.Around:
					AddRing(player, count, distance, jitter, startAngle, random01, results);
					return;
				case ScatterArrangement.Wall:
					Vector3 away = FlatDirection(caster, player);
					Vector3 side = Vector3.Cross(Vector3.up, away);
					for (int i = 0; i < count; i++)
						results.Add(player + away * (distance + jitter) + side * (distance * (i - (count - 1) * 0.5f)));
					return;
				case ScatterArrangement.Across:
					ArrangeAcross(caster, player, count, 1, distance, results);
					return;
				default:
					results.Add(player);
					AddRing(player, count - 1, distance, jitter, startAngle, random01, results);
					return;
			}
		}

		public static Vector3 FlatDirection(Vector3 from, Vector3 to)
		{
			Vector3 direction = to - from;
			direction.y = 0f;
			return direction.sqrMagnitude < SlimeKingPatternMath.DIRECTION_EPSILON_SQR ? Vector3.right : direction.normalized;
		}

		private static void AddRing(Vector3 center, int count, float distance, float jitter, float startAngle, Func<float> random01, List<Vector3> results)
		{
			for (int i = 0; i < count; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, startAngle + 360f / count * i, 0f) * Vector3.forward;
				results.Add(center + direction * (distance + jitter * random01()));
			}
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 여러 자리 장판은 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			int ringCount = Arrangement == ScatterArrangement.Scatter ? Count - 1 : Count;
			bool ringOverlaps = ringCount > 1 && 2f * Distance * Mathf.Sin(Mathf.PI / ringCount) < Radius * 2f;
			bool centerOverlaps = Arrangement == ScatterArrangement.Scatter && Count > 1 && Distance < Radius * 2f;
			bool isLine = Arrangement is ScatterArrangement.Line or ScatterArrangement.Wall or ScatterArrangement.Across;
			if (isLine == false && (ringOverlaps || centerOverlaps))
				errors.Add($"{name}: 원이 겹쳐 사이 틈이 없음 (거리 {Distance}, 반지름 {Radius}, {Count}개)");
			if (isLine && Distance > Radius * 2f)
				errors.Add($"{name}: 줄 간격 {Distance} 이 지름보다 커 줄이 끊김");
			if (Gap == ScatterGap.FarFromPlayer && Count < 4)
				errors.Add($"{name}: 먼 쪽 빈칸은 양 끝을 빼고 고르므로 네 칸 이상 ({Count}개)");
			if (Gap != ScatterGap.None && (isLine == false || Count < 3))
				errors.Add($"{name}: 빈칸은 줄, 벽, 가로줄에 세 칸 이상일 때만 ({Arrangement}, {Count}개)");
			if (Rows > 1 && Arrangement != ScatterArrangement.Across)
				errors.Add($"{name}: 줄 수 ({nameof(Rows)}) 는 가로줄만");
			if (SafeFx != null && Gap == ScatterGap.None)
				errors.Add($"{name}: 빈칸 표시가 있는데 빈칸이 없음");
			HitEffect.Validate(name, errors);
		}
	}
}
