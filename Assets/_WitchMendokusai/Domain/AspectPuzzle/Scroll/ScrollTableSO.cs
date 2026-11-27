using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 봉인된 두루마리 두 종류 (종이, 번갈아) 와 그 안에 들 수 있는 것 목록.
	/// 마도서와 두루마리가 같은 하찮은 마법 목록을 쓰기로 했으나 (기획 정본 11-25 23:01) 그 목록이 코드에 아직 없어 마법은 여기 임시 이름으로.
	/// 이름, 마법, 모습 수는 전부 임시값 (사용자 몫)
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_ScrollTable", menuName = "WM/Aspect/" + nameof(ScrollTableSO))]
	public class ScrollTableSO : ScriptableObject
	{
		[field: Header("두루마리")]
		[field: SerializeField, Tooltip("풀면 육각 종이 문제 하나")] public ItemData PaperScroll { get; private set; }
		[field: SerializeField, Tooltip("풀면 번갈아 문제 하나")] public ItemData TurnScroll { get; private set; }

		[field: Header("든 것")]
		[field: SerializeField] public List<ScrollTableRow> Rows { get; private set; } = new();

		public List<ScrollEntry> ToEntries()
		{
			List<ScrollEntry> entries = new();
			foreach (ScrollTableRow row in Rows)
			{
				entries.Add(row.ToEntry());
			}
			return entries;
		}

		public ScrollTableRow Row(int id)
		{
			foreach (ScrollTableRow row in Rows)
			{
				if (row.Id == id)
				{
					return row;
				}
			}
			throw new InvalidOperationException($"{name}: 두루마리 항목 {id} 가 없음");
		}
	}

	[Serializable]
	public class ScrollTableRow
	{
		[field: SerializeField, Tooltip("세이브 열쇠. 바꾸면 이미 연 것을 잃음")] public int Id { get; private set; }
		[field: SerializeField] public ScrollResultKind Kind { get; private set; }
		[field: SerializeField, Tooltip("마법일 때 보일 이름 (임시)")] public string SpellName { get; private set; }
		[field: SerializeField, Tooltip("만드는 법일 때 레시피를 가진 아이템")] public ItemData RecipeItem { get; private set; }
		[field: SerializeField, Min(1), Tooltip("배운 것까지 합친 모습 수")] public int Forms { get; private set; } = 1;

		public string DisplayName => Kind == ScrollResultKind.Recipe ? RecipeItem.Name : SpellName;

		public ScrollEntry ToEntry() => new()
		{
			Id = Id,
			Kind = Kind,
			Target = Kind == ScrollResultKind.Recipe ? RecipeItem.ID : Id,
			Forms = Forms,
		};
	}
}
