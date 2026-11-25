using System;
using System.Collections.Generic;
using UnityEngine;

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

		// 시전자 중심이면 자리가 아니라 방향을 고정. 예고 중 밀려도 예고와 판정이 시전자를 같이 따라감
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			Vector3 target = Center == ZoneCenter.Caster ? ScatterZoneMonsterAttack.FlatDirection(unitObject.transform.position, playerPosition) : playerPosition;
			foreach (AttackBand _ in Bands)
				results.Add(target);
		}

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
			HitEffect.Validate(name, errors);
		}
	}
}
