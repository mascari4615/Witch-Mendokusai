using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	[Serializable]
	public class AttackBand
	{
		[field: SerializeField, Tooltip("안 반지름 (m). 0 이면 꽉 찬 원"), Min(0f)] public float Inner { get; private set; }
		[field: SerializeField, Tooltip("바깥 반지름 (m)"), Min(0.3f)] public float Outer { get; private set; } = 2f;
		[field: SerializeField, Tooltip("첫 띠보다 늦어지는 초"), Min(0f)] public float Delay { get; private set; }
	}

	// 고리 띠. 중심은 같고 안팎 반지름이 다른 띠가 차례로 침 (안 다음 밖). 각도가 있으면 준비를 시작한 순간의 주인공 쪽 부채
	// 안쪽이 빈 띠를 크게 하면 안전 섬 (원 밖 전체 피해). 섬은 SafeFx 로 밝게. 엄폐 층을 주면 시전자와 주인공 사이가 막힐 때 안 맞음
	[CreateAssetMenu(fileName = "MA_Band", menuName = "WM/Variable/MonsterAttack/" + nameof(BandMonsterAttack))]
	public class BandMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "고리 띠";

		[field: Header("고리 띠")]
		[field: SerializeField, Tooltip("중심")] public ZoneCenter Center { get; private set; } = ZoneCenter.Caster;
		[field: SerializeField, Tooltip("부채 각도 (도). 360 이면 온 둘레"), Range(10f, 360f)] public float ArcDegrees { get; private set; } = 360f;
		[field: SerializeField] public List<AttackBand> Bands { get; private set; } = new() { new AttackBand() };
		[field: SerializeField, Tooltip("기본 피해"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		[field: Header("안전 섬과 엄폐")]
		[field: SerializeField, Tooltip("주인공 중심만. 중심을 주인공에서 이만큼 무작위 쪽으로 옮김 (m). 0 이면 주인공 자리"), Min(0f)] public float CenterOffset { get; private set; }
		[field: SerializeField, Tooltip("이 층의 콜라이더가 시전자와 주인공 사이를 막으면 안 맞음. 비우면 엄폐 없음")] public LayerMask CoverMask { get; private set; }
		[field: SerializeField, Tooltip("엄폐 선 높이 (m). 낮은 풀과 바닥 굴곡은 넘게"), Min(0f)] public float CoverHeight { get; private set; } = 0.5f;

		// 안 반지름이 가장 작은 띠의 빈 곳. 모든 띠가 비운 곳이라 안전
		public float SafeRadius
		{
			get
			{
				float radius = float.PositiveInfinity;
				foreach (AttackBand band in Bands)
					radius = Mathf.Min(radius, band.Inner);
				return float.IsPositiveInfinity(radius) ? 0f : radius;
			}
		}

		// 시전자 중심이면 자리가 아니라 방향을 고정. 예고 중 밀려도 예고와 판정이 시전자를 같이 따라감
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			Vector3 target = Center == ZoneCenter.Caster
				? ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition)
				: CenterOffset > 0f ? OffsetCenter(playerPosition, CenterOffset, Random.value * 360f) : playerPosition;
			foreach (AttackBand _ in Bands)
				results.Add(target);
		}

		// 띠들이 비운 안쪽이 있으면 그 중심이 안전 자리 (시전자 중심이면 따라감)
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results, List<Vector3> safeSpots)
		{
			ResolveTargets(unitObject, playerPosition, moveVelocity, results);
			if (SafeRadius > 0f && ArcDegrees >= 360f && results.Count > 0)
				safeSpots.Add(results[0]);
		}

		protected override bool HasSafeSpots => true;

		public override CombatFxContext SafeContext(UnitObject unitObject, Vector3 spot)
		{
			float lastDelay = 0f;
			foreach (AttackBand band in Bands)
				lastDelay = Mathf.Max(lastDelay, band.Delay);
			return new CombatFxContext
			{
				Position = CenterOf(unitObject, spot),
				Follow = Center == ZoneCenter.Caster ? unitObject.transform : null,
				Radius = SafeRadius,
				Seconds = WindupSeconds + lastDelay,
			};
		}

		// 주인공 둘레 정한 거리의 한 점. angleDegrees 는 +Z 에서 돈 각도 (시험에서 고정)
		public static Vector3 OffsetCenter(Vector3 playerPosition, float offset, float angleDegrees) =>
			offset <= 0f ? playerPosition : playerPosition + Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward * offset;

		public override float StrikeDelay(int index) => Bands[index].Delay;

		protected override bool TargetsArePositions => Center == ZoneCenter.Target;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index) => new()
		{
			Position = CenterOf(unitObject, target),
			Follow = Center == ZoneCenter.Caster ? unitObject.transform : null,
			Direction = Center == ZoneCenter.Caster ? target : Vector3.forward,
			Radius = Bands[index].Outer,
			Length = Bands[index].Outer,
			InnerRadius = Bands[index].Inner,
			ArcDegrees = ArcDegrees,
			Seconds = WindupSeconds,
		};

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			Vector3 center = CenterOf(unitObject, target);
			if (player.IsAlive == false || IsInBand(center, Bands[index], target, ArcDegrees, player.transform.position, player.UnitMovement.BodyRadius) == false)
				return false;
			if (MonsterCover.Blocks(unitObject.transform.position, player.transform.position, CoverHeight, CoverMask, unitObject.transform, player.transform))
				return false;
			HitEffect.Strike(unitObject, center, player, MakeDamage(unitObject, Damage));
			return true;
		}

		private Vector3 CenterOf(UnitObject unitObject, Vector3 target) => Center == ZoneCenter.Caster ? unitObject.transform.position : target;

		// 띠 안인가. 높이는 무시, direction 은 부채 가운데 (온 둘레면 안 씀). 바깥과 부채 변은 몸이 걸치면 맞고, 안쪽 빈 곳은 중심이 들어가면 안전
		public static bool IsInBand(Vector3 center, AttackBand band, Vector3 direction, float arcDegrees, Vector3 point, float bodyRadius)
		{
			Vector3 offset = point - center;
			offset.y = 0f;
			float distance = offset.magnitude;
			if (distance > band.Outer + bodyRadius || distance < band.Inner)
				return false;
			if (arcDegrees >= 360f || distance <= bodyRadius)
				return true;
			float beyond = Vector3.Angle(direction, offset) - arcDegrees * 0.5f;
			return beyond <= 0f || (beyond < 90f && distance * Mathf.Sin(beyond * Mathf.Deg2Rad) <= bodyRadius);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 고리 띠는 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			if (Bands.Count == 0)
				errors.Add($"{name}: 띠가 비어 있음");
			foreach (AttackBand band in Bands)
			{
				if (band.Inner >= band.Outer)
					errors.Add($"{name}: 안 반지름 {band.Inner} 이 바깥 {band.Outer} 이상");
			}
			if (CenterOffset > 0f && Center != ZoneCenter.Target)
				errors.Add($"{name}: 중심 옮김 ({nameof(CenterOffset)}) 은 주인공 중심만");
			if (SafeFx != null && (SafeRadius <= 0f || ArcDegrees < 360f))
				errors.Add($"{name}: 안전 섬 표시가 있는데 온 둘레 띠에 빈 안쪽이 없음");
			HitEffect.Validate(name, errors);
		}
	}
}
