using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 일반 적 공격이 주인공에게 실제로 피해를 준 순간 거는 것 (상태이상, 밀기). 공격 자산의 칸으로 끼움
	// 거는 일은 상태이상 (UnitStatus) 과 이동 (UnitMovement) 에 맡기고 여기는 값만
	[Serializable]
	public class MonsterHitEffect
	{
		[field: SerializeField, Tooltip("상태이상을 걺")] public bool AppliesStatus { get; private set; }
		[field: SerializeField] public UnitStatusType Status { get; private set; } = UnitStatusType.Slow;
		[field: SerializeField, Tooltip("상태이상 초"), Min(0f)] public float StatusSeconds { get; private set; }
		[field: SerializeField, Tooltip("둔화만. 이동 속도를 줄이는 비율"), Range(0f, 1f)] public float SlowRatio { get; private set; }
		[field: SerializeField, Tooltip("미는 속도 (m/s). 0 이면 안 밂"), Min(0f)] public float PushSpeed { get; private set; }
		[field: SerializeField, Tooltip("미는 초. 길이는 속도 x 초"), Min(0f)] public float PushSeconds { get; private set; }

		[field: SerializeField, Tooltip("밀지 않고 중심 쪽으로 당김")] public bool Pulls { get; private set; }

		public bool Pushes => PushSpeed > 0f && PushSeconds > 0f;

		// 피해를 주고, 체력이 실제로 줄었을 때만 효과를 걺. 무적, 회피, 보호막에 막히면 효과도 없음
		public void Strike(UnitObject source, Vector3 center, PlayerObject player, DamageInfo damageInfo)
		{
			int healthBefore = player.UnitStat[UnitStatType.HP_CUR];
			player.ReceiveDamage(damageInfo);
			if (player.IsAlive && player.UnitStat[UnitStatType.HP_CUR] < healthBefore)
				Apply(source, center, player);
		}

		private void Apply(UnitObject source, Vector3 center, PlayerObject player)
		{
			if (AppliesStatus && Status == UnitStatusType.Slow)
				UnitStatus.ApplySlow(player, source, StatusSeconds, SlowRatio);
			else if (AppliesStatus)
				UnitStatus.Apply(player, Status, source, StatusSeconds);

			if (Pushes == false)
				return;
			Vector3 direction = PushDirection(center, player.transform.position);
			if (direction == Vector3.zero)
				return;
			if (Pulls == false)
			{
				player.UnitMovement.ApplyImpulse(direction * PushSpeed, PushSeconds);
				return;
			}
			// 당길 때는 시전자 몸 앞까지만
			float gap = Vector3.Distance(center, player.transform.position) - source.UnitMovement.BodyRadius - player.UnitMovement.BodyRadius;
			if (gap > 0f)
				player.UnitMovement.ApplyImpulse(-direction * PushSpeed, Mathf.Min(PushSeconds, gap / PushSpeed));
		}

		// 중심에서 바깥으로. 중심에 겹쳐 있으면 방향 없음
		public static Vector3 PushDirection(Vector3 center, Vector3 target)
		{
			Vector3 away = target - center;
			away.y = 0f;
			return away.sqrMagnitude < 0.0001f ? Vector3.zero : away.normalized;
		}

		public void Validate(string ownerName, List<string> errors)
		{
			if (AppliesStatus && StatusSeconds <= 0f)
				errors.Add($"{ownerName}: 상태이상 {Status} 인데 초가 0");
			if (AppliesStatus && Status == UnitStatusType.Slow && SlowRatio <= 0f)
				errors.Add($"{ownerName}: 둔화인데 비율이 0");
			if (PushSpeed > 0f != PushSeconds > 0f)
				errors.Add($"{ownerName}: 밀기는 속도와 초가 같이 있어야 함 ({PushSpeed}, {PushSeconds})");
		}
	}
}
