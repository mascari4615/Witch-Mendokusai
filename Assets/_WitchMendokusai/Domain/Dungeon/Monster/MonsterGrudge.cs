using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 특정 인형에게만 더 사나운 몬스터. 조종 중인 인형이 맞으면 능력치를 올림
	[Serializable]
	public class MonsterGrudge
	{
		public const int NONE = -1;

		// 자산 참조 대신 번호. Addressables 로 따로 올라온 같은 인형도 같다고 보게
		[field: SerializeField, Tooltip("미워하는 인형 번호 (Doll ID). -1 이면 없음")] public int DollId { get; private set; } = NONE;
		[field: SerializeField, Tooltip("이동 속도 증가 퍼센트"), Min(0)] public int SpeedPercent { get; private set; }
		[field: SerializeField, Tooltip("주는 피해 증가 퍼센트"), Min(0)] public int DamagePercent { get; private set; }

		public bool Hates(Unit unit) => DollId != NONE && unit is Doll && unit.ID == DollId;

		public void Apply(UnitStat unitStat)
		{
			unitStat[UnitStatType.MOVEMENT_SPEED] = Mathf.RoundToInt(unitStat[UnitStatType.MOVEMENT_SPEED] * (1f + SpeedPercent / 100f));
			unitStat[UnitStatType.DAMAGE_BONUS] += DamagePercent;
		}
	}
}
