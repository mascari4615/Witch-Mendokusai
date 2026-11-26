using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 치유 파동. 몇 초마다 둘레에 고리가 퍼지며 근처 다친 일반 몬스터의 체력을 채움
	// 자기, 보스, 숨은 몬스터, 다른 치유 몬스터는 안 채움 (서로 채워 끝나지 않는 일을 막음)
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

		public static bool CanReceive(MonsterObject healer, MonsterObject target)
		{
			if (target == healer || target.IsAlive == false || target.IsHidden || target.UnitData.Type == MonsterType.Boss)
				return false;
			foreach (MonsterAffix affix in target.UnitData.Affixes)
			{
				if (affix is HealPulseAffix)
					return false;
			}
			return true;
		}

		// 기억의 Timer 는 다음 파동까지 남은 초. 처음은 간격 한 번을 기다림
		public override void OnSpawn(MonsterObject monster, AffixMemory memory) => memory.Timer = intervalSeconds;

		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			memory.Timer -= deltaTime;
			if (memory.Timer > 0f)
				return;
			memory.Timer += intervalSeconds;
			Vector3 center = monster.transform.position;
			// 고리는 그 자리에 남김. 몸을 따라가면 0.45초 안에 풀에서 다른 몬스터로 다시 나온 몸을 따라감
			if (pulseFx != null)
				pulseFx.Play(new CombatFxContext { Position = center, Radius = radius, Seconds = pulseSeconds });
			foreach (GameObject candidate in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (IsNear(center, candidate.transform.position, radius) == false || candidate.TryGetComponent(out MonsterObject ally) == false || CanReceive(monster, ally) == false)
					continue;
				int amount = HealAmount(ally.UnitStat[UnitStatType.HP_MAX], ally.UnitStat[UnitStatType.HP_CUR], healRatio);
				if (amount > 0)
					ally.ReceiveHeal(amount);
			}
		}

		private static bool IsNear(Vector3 center, Vector3 position, float radius)
		{
			Vector3 offset = position - center;
			offset.y = 0f;
			return offset.sqrMagnitude <= radius * radius;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (pulseFx == null)
				errors.Add($"{name}: 고리 ({nameof(pulseFx)}) 없이 채우면 누가 채우는지 안 보임");
		}
	}
}
