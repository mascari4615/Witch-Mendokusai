using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 하찮은 마법 목록 하나. 두루마리와 마도서 장이 같은 목록을 가리킴 (기획 정본 11-25 23:01). 지금은 두루마리 표만 가리킴.
	/// 이름, 설명, 무엇을 하는지는 사용자 몫. 들어 있는 것은 전부 임시
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_SpellList", menuName = "WM/Aspect/" + nameof(SpellListSO))]
	public class SpellListSO : ScriptableObject
	{
		[field: SerializeField] public List<SpellEntry> Spells { get; private set; } = new();

		public SpellEntry Get(int id)
		{
			foreach (SpellEntry spell in Spells)
			{
				if (spell.Id == id)
				{
					return spell;
				}
			}
			throw new InvalidOperationException($"{name}: 마법 {id} 가 없음");
		}
	}

	[Serializable]
	public class SpellEntry
	{
		[field: SerializeField, Tooltip("두루마리 표가 가리키는 번호. 바꾸면 표도 고쳐야 함")] public int Id { get; private set; }
		[field: SerializeField] public string Name { get; private set; }
		[field: SerializeField, TextArea] public string Description { get; private set; }
		[field: SerializeField, Tooltip("쓸 때 뜨는 한 줄. {0} 은 열린 모습 번호")] public string CastText { get; private set; }
	}
}
