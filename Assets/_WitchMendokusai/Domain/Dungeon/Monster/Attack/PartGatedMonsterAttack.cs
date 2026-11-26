using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 부위에 걸린 공격. 낱개 공격 하나를 감싸고, 시전자의 그 부위 (PartsAffix 가 세운 몸) 가 꺾였으면 순서가 건너뜀
	// 박자, 예고, 실행은 감싼 공격 그대로. 정본 memo wm/design/boss-arena-20261126
	[CreateAssetMenu(fileName = "MA_PartGated", menuName = "WM/Variable/MonsterAttack/" + nameof(PartGatedMonsterAttack))]
	public class PartGatedMonsterAttack : MonsterAttack
	{
		public override string KindName => "부위 공격";

		[field: Header("부위")]
		[field: SerializeField, Tooltip("감싼 공격. 낱개만")] public MonsterAttack Inner { get; private set; }
		[field: SerializeField, Tooltip("이 공격을 쥔 부위 몸 (보스의 PartsAffix 칸과 같은 몬스터)")] public Monster Part { get; private set; }

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) => Inner.CreateBehaviour(unitObject, playerProvider);

		public override void Interrupt(BTRunner behaviour) => Inner.Interrupt(behaviour);

		public override bool IsPointless(UnitObject unitObject) => BossParts.HasLivingPart(unitObject, Part) == false || Inner.IsPointless(unitObject);

		// 감싼 쪽의 박자 칸은 안 씀. 검사는 감싼 공격과 부위 짝만
		public override void Validate(Monster owner, List<string> errors)
		{
			if (Inner == null)
			{
				errors.Add($"{name}: 감싼 공격이 비어 있음");
				return;
			}
			if (Inner is SequenceMonsterAttack or PackMonsterAttack or PhaseMonsterAttack or PartGatedMonsterAttack)
				errors.Add($"{name}: {Inner.name} 은 감쌀 수 없음 (낱개 공격만)");
			Inner.Validate(owner, errors);
			if (Part == null)
			{
				errors.Add($"{name}: 부위 몸이 비어 있음");
				return;
			}
			bool hasSlot = false;
			foreach (MonsterAffix affix in owner.Affixes)
			{
				if (affix is not PartsAffix parts)
					continue;
				foreach (BossPartSlot slot in parts.Slots)
					hasSlot |= slot.Part == Part;
			}
			if (hasSlot == false)
				errors.Add($"{name}: {owner.name} 의 {nameof(PartsAffix)} 에 부위 {Part.name} 칸이 없음 (늘 건너뜀)");
		}
	}
}
