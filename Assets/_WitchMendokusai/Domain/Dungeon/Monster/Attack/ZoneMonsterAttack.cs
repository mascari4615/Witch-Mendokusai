using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum ZoneCenter
	{
		// 준비를 시작한 순간 주인공 자리
		Target = 0,
		// 시전자 둘레
		Caster = 1,
	}

	// 장판. 준비를 시작한 순간 주인공 자리 (예측이 있으면 그만큼 앞) 나 시전자 둘레에 원 예고, 예고가 차면 그 원 안 주인공에게 피해. 시전자는 멈춰 있음
	[CreateAssetMenu(fileName = "MA_Zone", menuName = "WM/Variable/MonsterAttack/" + nameof(ZoneMonsterAttack))]
	public class ZoneMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "장판";

		[field: Header("장판")]
		[field: SerializeField, Tooltip("원 중심")] public ZoneCenter Center { get; private set; } = ZoneCenter.Target;
		[field: SerializeField, Tooltip("원 반지름 (m)"), Min(0.3f)] public float Radius { get; private set; } = 2f;
		[field: SerializeField, Tooltip("터질 때 기본 피해. 시간 상승과 위협 배율은 다른 적 피해와 같게 붙음"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("주인공 이동 예측 (초 뒤 자리). 0 이면 지금 자리"), Min(0f)] public float LeadSeconds { get; private set; } = 0f;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = CenterOf(unitObject, target),
			// 예고 중 밀려도 예고와 판정이 같은 자리
			Follow = Center == ZoneCenter.Caster ? unitObject.transform : null,
			Radius = Radius,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets)
		{
			if (player.IsAlive == false)
				return;
			Vector3 center = CenterOf(unitObject, targets[0]);
			Vector3 offset = player.transform.position - center;
			offset.y = 0f;
			float reach = Radius + player.UnitMovement.BodyRadius;
			if (offset.sqrMagnitude > reach * reach)
				return;
			HitEffect.Strike(unitObject, center, player, MakeDamage(unitObject, Damage));
		}

		private Vector3 CenterOf(UnitObject unitObject, Vector3 target) => Center == ZoneCenter.Caster ? unitObject.transform.position : target;

		public override Vector3 ResolveTarget(Vector3 playerPosition, Vector3 moveVelocity)
		{
			moveVelocity.y = 0f;
			return playerPosition + moveVelocity * LeadSeconds;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 장판은 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			if (Center == ZoneCenter.Caster && LeadSeconds > 0f)
				errors.Add($"{name}: 시전자 둘레 장판은 예측 ({nameof(LeadSeconds)}) 을 안 씀");
			HitEffect.Validate(name, errors);
		}
	}
}
