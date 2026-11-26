using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 치유 파동. 몇 초마다 둘레에 고리가 퍼지며 근처 다친 몬스터의 체력을 채움. 자기는 안 채움
	// 시키는 행동: 이것부터 잡기 (우선순위)
	[CreateAssetMenu(fileName = "AF_HealPulse", menuName = "WM/Monster/Affix/HealPulse")]
	public sealed class HealPulseAffix : MonsterAffix
	{
		[SerializeField, Tooltip("파동 간격 (초)"), Min(0.5f)] private float intervalSeconds = 3f;
		[SerializeField, Tooltip("파동 반지름 (m)"), Min(0.5f)] private float radius = 5f;
		[SerializeField, Tooltip("한 번에 채우는 양 (최대 체력 비율)"), Range(0f, 1f)] private float healRatio = 0.2f;
		[SerializeField, Tooltip("퍼지는 고리")] private CombatFx pulseFx;
		[SerializeField, Tooltip("고리가 보이는 초"), Min(0.05f)] private float pulseSeconds = 0.45f;

		public override string KindName => "치유 파동";

		public static int HealAmount(int maxHealth, int currentHealth, float ratio) => Mathf.Clamp(Mathf.CeilToInt(maxHealth * ratio), 0, Mathf.Max(0, maxHealth - currentHealth));

		// 기억의 Timer 는 다음 파동까지 남은 초. 처음은 간격 한 번을 기다림
		public override void OnSpawn(MonsterObject monster, AffixMemory memory) => memory.Timer = intervalSeconds;

		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			memory.Timer -= deltaTime;
			if (memory.Timer > 0f)
				return;
			memory.Timer += intervalSeconds;
			Vector3 center = monster.transform.position;
			if (pulseFx != null)
				pulseFx.Play(new CombatFxContext { Position = center, Follow = monster.transform, Radius = radius, Seconds = pulseSeconds });
			foreach (GameObject candidate in ObjectBufferManager.GetObjectsWithDistance(ObjectType.Monster, center, radius))
			{
				if (candidate == monster.gameObject || candidate.TryGetComponent(out MonsterObject ally) == false || ally.IsAlive == false)
					continue;
				int amount = HealAmount(ally.UnitStat[UnitStatType.HP_MAX], ally.UnitStat[UnitStatType.HP_CUR], healRatio);
				if (amount > 0)
					ally.ReceiveHeal(amount);
			}
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (pulseFx == null)
				errors.Add($"{name}: 고리 ({nameof(pulseFx)}) 없이 채우면 누가 채우는지 안 보임");
		}
	}
}
