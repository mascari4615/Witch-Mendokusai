using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 여러 공격을 차례로 한 바퀴. 다 돌면 이 자산의 대기 (Cooldown) 동안 추적하고 처음부터
	// 사거리, 예고, 숨 고르기는 낱개 공격의 값. 낱개의 대기는 안 씀
	[CreateAssetMenu(fileName = "MA_Sequence", menuName = "WM/Variable/MonsterAttack/" + nameof(SequenceMonsterAttack))]
	public class SequenceMonsterAttack : MonsterAttack
	{
		public override string KindName => "연속 공격";

		[field: SerializeField, Tooltip("차례. 순서나 무리를 다시 넣지 않음")] public List<MonsterAttack> Steps { get; private set; } = new();

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_SequenceAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_SequenceAttack)behaviour).Interrupt();

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (Steps.Count == 0)
				errors.Add($"{name}: 차례가 비어 있음");
			foreach (MonsterAttack step in Steps)
			{
				if (step == null)
					errors.Add($"{name}: 차례에 빈 칸");
				else if (step is SequenceMonsterAttack or PackMonsterAttack)
					errors.Add($"{name}: 차례 안에 {step.name} 을 넣을 수 없음 (낱개 공격만)");
				else
					step.Validate(owner, errors);
			}
		}
	}
}
