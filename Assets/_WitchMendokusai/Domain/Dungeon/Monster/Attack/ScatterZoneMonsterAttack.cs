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
	}

	// 여러 자리 장판. 준비를 시작한 순간 자리를 모두 고정, 예고가 차면 어느 원이든 안에 있으면 피해 한 번
	[CreateAssetMenu(fileName = "MA_Scatter", menuName = "WM/Variable/MonsterAttack/" + nameof(ScatterZoneMonsterAttack))]
	public class ScatterZoneMonsterAttack : WindupMonsterAttack
	{
		[field: Header("여러 자리")]
		[field: SerializeField] public ScatterArrangement Arrangement { get; private set; } = ScatterArrangement.Scatter;
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 5;
		[field: SerializeField, Tooltip("원 반지름 (m)"), Min(0.3f)] public float Radius { get; private set; } = 1.2f;
		[field: SerializeField, Tooltip("줄은 칸 간격, 고리와 흩뿌림은 중심에서 거리 (m)"), Min(0.1f)] public float Distance { get; private set; } = 2.6f;
		[field: SerializeField, Tooltip("고리와 흩뿌림. 거리를 이만큼까지 더 멀리 흔듦 (m)"), Min(0f)] public float DistanceJitter { get; private set; }
		[field: SerializeField, Tooltip("터질 때 기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results) =>
			Arrange(Arrangement, unitObject.transform.position, playerPosition, Count, Distance, DistanceJitter, Random.value * 360f, () => Random.value, results);

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = target,
			Radius = Radius,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets)
		{
			if (player.IsAlive == false)
				return;
			foreach (Vector3 target in targets)
			{
				if (SlimeKingPatternMath.IsWithin(target, player.transform.position, Radius + player.UnitMovement.BodyRadius) == false)
					continue;
				HitEffect.Strike(unitObject, target, player, MakeDamage(unitObject, Damage));
				return;
			}
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
			if (Arrangement != ScatterArrangement.Line && (ringOverlaps || centerOverlaps))
				errors.Add($"{name}: 원이 겹쳐 사이 틈이 없음 (거리 {Distance}, 반지름 {Radius}, {Count}개)");
			if (Arrangement == ScatterArrangement.Line && Distance > Radius * 2f)
				errors.Add($"{name}: 줄 간격 {Distance} 이 지름보다 커 줄이 끊김");
			HitEffect.Validate(name, errors);
		}
	}
}
