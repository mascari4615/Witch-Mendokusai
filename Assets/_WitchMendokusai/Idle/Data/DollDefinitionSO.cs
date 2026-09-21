using UnityEngine;
using UnityEngine.Serialization;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleDoll", menuName = "WM/Idle/Doll Definition")]
	public sealed class DollDefinitionSO : ScriptableObject
	{
		[FormerlySerializedAs("<ID>k__BackingField")]
		[SerializeField] private int id;
		[FormerlySerializedAs("<Name>k__BackingField")]
		[SerializeField] private string displayName;
		[FormerlySerializedAs("<Sprite>k__BackingField")]
		[SerializeField] private Sprite portrait;

		[Tooltip("카드와 편성 자리용 정사각 얼굴 (머리와 어깨). 비면 portrait 를 씀. 2026-09-21 Codex 일러")]
		[SerializeField] private Sprite cardPortrait;
		[SerializeField] private IdleDollAxis axis;
		[SerializeField] private IdleDollGrade grade;
		[SerializeField, Min(3)] private int sides = 3;

		[Header("스킬 (임시 배정. 인형 컨셉이 서면 여기서 갈아끼운다)")]
		[Tooltip("켜면 축대로 (힘 일제 사격, 기지 긴급 보급, 떨구기 비밀 감정, 속도 가속). 끄면 아래 skill.")]
		[SerializeField] private bool skillByAxis = true;
		[SerializeField] private IdleCardKind skill = IdleCardKind.Volley;

		public int ID => id;
		public string Name => displayName;
		public Sprite Sprite => portrait;

		/// <summary>카드, 자리 아이콘용 얼굴. 없으면 반신 초상</summary>
		public Sprite Face => cardPortrait != null ? cardPortrait : portrait;
		public IdleDollAxis Axis => axis;
		public IdleDollGrade Grade => grade;
		public int Sides => sides;

		public IdleCardKind Skill => skillByAxis ? IdleCards.SkillForAxis(axis) : skill;

		public IdleDollKind ToDomain() => new IdleDollKind(id, displayName, axis, grade, sides, Skill);
	}
}
