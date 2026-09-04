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

		public int ID => id;
		public string Name => displayName;
		public Sprite Sprite => portrait;
		public IdleHeroAxis Axis => axis;
		public IdleHeroGrade Grade => grade;
		public int Sides => sides;

		public IdleHeroKind ToDomain() => new IdleHeroKind(id, displayName, axis, grade, sides);
	}
}
