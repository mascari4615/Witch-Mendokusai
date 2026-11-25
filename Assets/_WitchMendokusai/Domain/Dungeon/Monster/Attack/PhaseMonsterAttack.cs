using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class MonsterAttackPhase
	{
		[field: SerializeField, Tooltip("체력 비율이 이 값 이하가 되면 이 공격으로. 첫 단계는 1"), Range(0f, 1f)] public float HealthRatio { get; private set; } = 1f;
		[field: SerializeField] public MonsterAttack Attack { get; private set; }
	}

	// 체력 단계. 비율이 낮아지면 다음 공격으로 바꾸고, 바뀌는 순간 하던 예고를 치움. 회복해도 앞 단계로 안 돌아감
	[CreateAssetMenu(fileName = "MA_Phase", menuName = "WM/Variable/MonsterAttack/" + nameof(PhaseMonsterAttack))]
	public class PhaseMonsterAttack : MonsterAttack
	{
		[field: Header("체력 단계")]
		[field: SerializeField, Tooltip("비율이 큰 것부터")] public List<MonsterAttackPhase> Phases { get; private set; } = new();

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_PhaseAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_PhaseAttack)behaviour).Interrupt();

		// 체력 비율에 맞는 단계. current 보다 앞으로는 안 감
		public int PhaseIndex(float healthRatio, int current)
		{
			int index = current;
			for (int i = current + 1; i < Phases.Count; i++)
			{
				if (healthRatio <= Phases[i].HealthRatio)
					index = i;
			}
			return index;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (Phases.Count == 0)
				errors.Add($"{name}: 단계가 비어 있음");
			for (int i = 0; i < Phases.Count; i++)
			{
				MonsterAttack attack = Phases[i].Attack;
				if (attack == null)
					errors.Add($"{name}: {i}번 단계 공격이 비어 있음");
				else if (attack is PhaseMonsterAttack)
					errors.Add($"{name}: 단계 안에 단계를 넣을 수 없음");
				else
					attack.Validate(owner, errors);
				if (i > 0 && Phases[i].HealthRatio >= Phases[i - 1].HealthRatio)
					errors.Add($"{name}: 단계 비율은 큰 것부터 ({Phases[i - 1].HealthRatio} 뒤 {Phases[i].HealthRatio})");
			}
		}
	}
}
