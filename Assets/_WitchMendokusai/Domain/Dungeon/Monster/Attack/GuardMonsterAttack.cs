using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 웅크리기. 멈춰서 방어력을 올렸다가 되돌림. 피해는 없고 때리지 말아야 할 때를 만듦. 예고 없이 바로 웅크림 (WindupSeconds 0)
	[CreateAssetMenu(fileName = "MA_Guard", menuName = "WM/Variable/MonsterAttack/" + nameof(GuardMonsterAttack))]
	public class GuardMonsterAttack : MonsterAttack
	{
		public override string KindName => "웅크리기";

		[field: Header("웅크리기")]
		[field: SerializeField, Tooltip("올릴 방어력"), Min(1)] public int Armor { get; private set; } = 300;
		[field: SerializeField, Tooltip("웅크린 초"), Min(0.1f)] public float GuardSeconds { get; private set; } = 3f;
		[field: SerializeField, Tooltip("웅크린 동안 발밑 표시. 비우면 없음")] public CombatFx GuardFx { get; private set; }
		[field: SerializeField, Min(0.1f)] public float GuardFxRadius { get; private set; } = 2f;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_Guard(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_Guard)behaviour).Interrupt();

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds > 0f)
				errors.Add($"{name}: 웅크리기는 예고를 안 씀 ({nameof(WindupSeconds)} {WindupSeconds})");
		}
	}
}
