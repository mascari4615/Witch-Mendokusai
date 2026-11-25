using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 원거리. 멈춰서 발밑 예고 뒤 몬스터 자산의 기본 스킬 (Unit.DefaultSkills) 하나를 씀. 탄 모양과 피해는 스킬 자산
	[CreateAssetMenu(fileName = "MA_Ranged", menuName = "WM/Variable/MonsterAttack/" + nameof(RangedMonsterAttack))]
	public class RangedMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "원거리";

		[field: Header("원거리")]
		[field: SerializeField, Tooltip("쓸 기본 스킬 번호 (몬스터 자산 DefaultSkills 순서)"), Min(0)] public int SkillIndex { get; private set; }
		[field: SerializeField, Tooltip("발밑 예고 원 반지름 (m)"), Min(0.1f)] public float TelegraphRadius { get; private set; } = 1.2f;

		public override bool IsReady(UnitObject unitObject)
		{
			// 스킬이 없으면 몬스터 자산 설정 오류. 조용히 추적만 하지 않게 바로 터뜨림
			Skill skill = unitObject.SkillHandler.SkillDic[SkillIndex];
			return skill.IsReady;
		}

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target) => new()
		{
			Position = unitObject.transform.position,
			Follow = unitObject.transform,
			Radius = TelegraphRadius,
			Seconds = WindupSeconds,
		};

		public override void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets) => unitObject.UseSkill(SkillIndex);

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (SkillIndex >= owner.DefaultSkills.Length)
				errors.Add($"{name}: 스킬 번호 {SkillIndex} 인데 {owner.name} 기본 스킬 {owner.DefaultSkills.Length}개");
		}
	}
}
