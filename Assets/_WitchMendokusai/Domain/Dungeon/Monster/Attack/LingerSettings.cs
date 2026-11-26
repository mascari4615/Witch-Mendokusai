using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격이 때린 자리에 남는 물체. 그림은 예고 물체 (WindupFx) 가 이어서 그리고, 피해가 있으면 LingerField 가 판정
	[Serializable]
	public class LingerSettings
	{
		[field: SerializeField, Tooltip("때린 뒤 남는 초. 0 이면 안 남음"), Min(0f)] public float Seconds { get; private set; }
		[field: SerializeField, Tooltip("사라지기 전 식어서 안 아픈 초. 그림이 어두워짐"), Min(0f)] public float CoolSeconds { get; private set; } = 0.4f;
		[field: SerializeField, Tooltip("닿으면 아픈 반지름 (m)"), Min(0f)] public float Radius { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("닿은 피해. 0 이면 닿아도 안 아픔"), Min(0)] public int Damage { get; private set; }
		[field: SerializeField, Tooltip("닿아 있는 동안 피해를 다시 시도하는 간격 (초). 회피에 성공해도 이만큼 쉼"), Min(0.1f)] public float TouchInterval { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("닿아서 피해가 들어간 순간 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();

		[field: Header("터짐 (돌진이 밟거나 끌기가 빨아들임, CracksLinger)")]
		[field: SerializeField, Tooltip("터지는 반지름 (m). 0 이면 안 터짐"), Min(0f)] public float BurstRadius { get; private set; }
		[field: SerializeField, Min(0)] public int BurstDamage { get; private set; }
		[field: SerializeField, Tooltip("터져서 피해가 들어간 순간 거는 것")] public MonsterHitEffect BurstHitEffect { get; private set; } = new();
		[field: SerializeField] public CombatFx BurstTelegraph { get; private set; }
		[field: SerializeField] public CombatFx BurstFx { get; private set; }

		public bool IsDangerous => Seconds > 0f && (Damage > 0 || BurstRadius > 0f);

		// 안 남는 공격은 null
		public FxHold Open(float strikeTime)
		{
			if (Seconds <= 0f)
				return null;
			float endTime = strikeTime + Seconds;
			return new FxHold
			{
				StrikeTime = strikeTime,
				EndTime = endTime,
				HarmlessTime = Damage > 0 ? Mathf.Max(strikeTime, endTime - CoolSeconds) : strikeTime,
			};
		}

		public void Validate(string ownerName, List<string> errors)
		{
			if (Seconds <= 0f)
				return;
			if (Damage > 0 && Radius <= 0f)
				errors.Add($"{ownerName}: 남는 물체가 아픈데 반지름이 0");
			if (Damage > 0 && CoolSeconds >= Seconds)
				errors.Add($"{ownerName}: 식는 초 {CoolSeconds} 가 남는 초 {Seconds} 이상이라 아플 틈이 없음");
			if (BurstRadius > 0f && (BurstDamage <= 0 || BurstTelegraph == null || BurstFx == null))
				errors.Add($"{ownerName}: 터지는 물체는 피해, 예고, 이펙트가 모두 있어야 함");
			HitEffect.Validate(ownerName, errors);
			BurstHitEffect.Validate(ownerName, errors);
		}
	}
}
