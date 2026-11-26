using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 보스 공격이 피해를 준 순간 주인공에 낙인 (UnitStatusType.Brand) 한 겹. 겹 수, 지속, 머리 위 칸은 상태이상 그대로
	// 문턱은 상태이상 설정의 낙인 쌓임 상한 하나. 닿으면 낙인을 지우고 기절과 터짐 이펙트. 즉사 없음. 해제 물체는 CleanseAffix
	// 정본 memo wm/design/boss-arena-20261126
	[Serializable]
	public class BrandSettings
	{
		[field: SerializeField, Tooltip("맞을 때마다 낙인 한 겹")] public bool Enabled { get; private set; }
		[field: SerializeField, Tooltip("문턱에 닿았을 때 기절 초"), Min(0f)] public float StunSeconds { get; private set; } = 1.5f;
		[field: SerializeField, Tooltip("문턱에 닿은 순간 주인공 자리 이펙트. 비우면 없음")] public CombatFx BurstFx { get; private set; }
		[field: SerializeField, Min(0.1f)] public float BurstFxRadius { get; private set; } = 1.4f;

		// 한 겹 더하고 문턱이면 터뜨림. 터졌으면 true
		public bool Stack(UnitObject source, PlayerObject player)
		{
			if (Enabled == false || player.IsAlive == false)
				return false;
			UnitStatus.Apply(player, UnitStatusType.Brand, source);
			int maxStacks = ResourceManager.Instance.UnitStatus.Style(UnitStatusType.Brand).MaxStacks;
			if (IsFull(UnitStatus.StackCount(player, UnitStatusType.Brand), maxStacks) == false)
				return false;
			UnitStatus.Consume(player, UnitStatusType.Brand);
			if (StunSeconds > 0f)
				UnitStatus.Apply(player, UnitStatusType.Stun, source, StunSeconds);
			if (BurstFx != null)
				BurstFx.Play(new CombatFxContext { Position = player.transform.position, Follow = player.transform, Radius = BurstFxRadius });
			return true;
		}

		// 문턱. 상한이 1 이하면 쌓이지 않으니 터지지도 않음 (설정 실수가 매번 기절이 되지 않게)
		public static bool IsFull(int stacks, int maxStacks) => maxStacks > 1 && stacks >= maxStacks;

		public void Validate(string ownerName, List<string> errors)
		{
			if (Enabled && StunSeconds <= 0f && BurstFx == null)
				errors.Add($"{ownerName}: 낙인이 문턱에서 아무 일도 안 함 (기절 0, 이펙트 없음)");
		}
	}
}
