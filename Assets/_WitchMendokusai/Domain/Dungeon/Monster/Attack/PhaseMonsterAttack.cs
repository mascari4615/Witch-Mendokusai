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
		[field: SerializeField, Tooltip("이 단계로 바뀔 때 한 번 하는 공격 (포효, 껍질 터뜨리기). 비우면 바로 바뀜")] public MonsterAttack Entrance { get; private set; }
		[field: SerializeField, Tooltip("이 단계부터 원래 그림. 비우면 그대로")] public Sprite BodySprite { get; private set; }
	}

	// 체력 단계. 비율이 낮아지면 하던 공격과 반격 창을 끝낸 뒤 다음 공격으로 바꿈. 회복해도 앞 단계로 안 돌아감
	[CreateAssetMenu(fileName = "MA_Phase", menuName = "WM/Variable/MonsterAttack/" + nameof(PhaseMonsterAttack))]
	public class PhaseMonsterAttack : MonsterAttack
	{
		public override string KindName => "단계";

		[field: Header("등장")]
		[field: SerializeField, Tooltip("나타난 뒤 공격하지 않고 서 있는 초 (이름 배너가 예고를 가리지 않게). 체력이 가득일 때만, 재배치 뒤에는 없음"), Min(0f)] public float OpeningSeconds { get; private set; } = 2.5f;

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
			else if (Phases[0].HealthRatio < 1f)
				errors.Add($"{name}: 첫 단계 비율은 1 ({Phases[0].HealthRatio})");
			for (int i = 0; i < Phases.Count; i++)
			{
				MonsterAttack attack = Phases[i].Attack;
				if (attack == null)
					errors.Add($"{name}: {i}번 단계 공격이 비어 있음");
				else if (attack is PhaseMonsterAttack or PackMonsterAttack)
					errors.Add($"{name}: 단계 안에 {attack.name} 을 넣을 수 없음 (순서나 낱개 공격만)");
				else
					attack.Validate(owner, errors);
				if (i == 0 && Phases[i].Entrance != null)
					errors.Add($"{name}: 첫 단계는 들어오는 공격을 안 씀 (등장은 {nameof(OpeningSeconds)})");
				if (Phases[i].Entrance != null && Phases[i].Entrance == attack)
					errors.Add($"{name}: {i}번 단계의 들어오는 공격과 단계 공격이 같음");
				if (Phases[i].Entrance is PhaseMonsterAttack or PackMonsterAttack or SequenceMonsterAttack)
					errors.Add($"{name}: 들어오는 공격은 낱개만 ({Phases[i].Entrance.name})");
				else if (Phases[i].Entrance != null)
					Phases[i].Entrance.Validate(owner, errors);
				if (Phases[i].BodySprite != null && (owner.Sprites == null || owner.Sprites.Count == 0))
					errors.Add($"{name}: 단계 그림이 있는데 {owner.name} 이 애니메이터 몸");
				if (i > 0 && Phases[i].HealthRatio >= Phases[i - 1].HealthRatio)
					errors.Add($"{name}: 단계 비율은 큰 것부터 ({Phases[i - 1].HealthRatio} 뒤 {Phases[i].HealthRatio})");
			}
		}
	}
}
