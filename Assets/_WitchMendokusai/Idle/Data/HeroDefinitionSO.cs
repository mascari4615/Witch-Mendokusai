using UnityEngine;
using UnityEngine.Serialization;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleHero", menuName = "WM/Idle/Hero Definition")]
	public sealed class HeroDefinitionSO : ScriptableObject
	{
		[FormerlySerializedAs("<ID>k__BackingField")]
		[SerializeField] private int id;
		[FormerlySerializedAs("<Name>k__BackingField")]
		[SerializeField] private string displayName;
		[FormerlySerializedAs("<Sprite>k__BackingField")]
		[SerializeField] private Sprite portrait;
		[SerializeField] private IdleHeroAxis axis;
		[SerializeField] private IdleHeroGrade grade;
		[SerializeField, Min(3)] private int sides = 3;

		[Header("스킬 (임시 배정. 인형 컨셉이 서면 여기서 갈아끼운다)")]
		[Tooltip("켜면 축대로 (힘 일제 사격, 기지 긴급 보급, 떨구기 비밀 감정, 속도 가속). 끄면 아래 skill.")]
		[SerializeField] private bool skillByAxis = true;
		[SerializeField] private IdleCardKind skill = IdleCardKind.Volley;

		public int ID => id;
		public string Name => displayName;
		public Sprite Sprite => portrait;
		public IdleHeroAxis Axis => axis;
		public IdleHeroGrade Grade => grade;
		public int Sides => sides;

		public IdleCardKind Skill => skillByAxis ? IdleCards.SkillForAxis(axis) : skill;

		public IdleHeroKind ToDomain() => new IdleHeroKind(id, displayName, axis, grade, sides, Skill);
	}
}
