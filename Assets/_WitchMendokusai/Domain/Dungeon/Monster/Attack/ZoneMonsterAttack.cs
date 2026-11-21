using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 장판. 준비를 시작한 순간 주인공 자리 (예측이 있으면 그만큼 앞) 에 원 예고, 예고가 차면 그 원 안 주인공에게 피해. 시전자는 멀리서 멈춰 있음
	[CreateAssetMenu(fileName = "MA_Zone", menuName = "WM/Variable/MonsterAttack/" + nameof(ZoneMonsterAttack))]
	public class ZoneMonsterAttack : WindupMonsterAttack
	{
		[field: Header("장판")]
		[field: SerializeField, Tooltip("원 반지름 (m)"), Min(0.3f)] public float Radius { get; private set; } = 2f;
		[field: SerializeField, Tooltip("터질 때 기본 피해. 시간 상승과 위협 배율은 다른 적 피해와 같게 붙음"), Min(1)] public int Damage { get; private set; } = 4;
		[field: SerializeField, Tooltip("주인공 이동 예측 (초 뒤 자리). 0 이면 지금 자리"), Min(0f)] public float LeadSeconds { get; private set; } = 0f;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = target,
			Radius = Radius,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, Vector3 target)
		{
			if (player.IsAlive == false)
				return;
			Vector3 offset = player.transform.position - target;
			offset.y = 0f;
			float reach = Radius + player.UnitMovement.BodyRadius;
			if (offset.sqrMagnitude > reach * reach)
				return;
			player.ReceiveDamage(MakeDamage(unitObject, Damage));
		}

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
		}
	}
}
