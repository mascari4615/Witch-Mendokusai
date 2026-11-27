using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 봉인된 두루마리 두 종류 (종이, 번갈아) 와 그 안에 들 수 있는 것 목록.
	/// 마법은 하찮은 마법 목록 (SpellListSO) 의 번호를 가리킴.
	/// 이름, 마법, 모습 수는 전부 임시값 (사용자 몫)
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_ScrollTable", menuName = "WM/Aspect/" + nameof(ScrollTableSO))]
	public class ScrollTableSO : ScriptableObject
	{
		[field: Header("두루마리")]
		[field: SerializeField, Tooltip("풀면 육각 종이 문제 하나")] public ItemData PaperScroll { get; private set; }
		[field: SerializeField, Tooltip("풀면 번갈아 문제 하나")] public ItemData TurnScroll { get; private set; }

		[field: Header("떨어지는 곳 (처치 드롭 표와 따로 굴림)")]
		[field: SerializeField, Range(0f, 100f), Tooltip("정예 처치 때 백분율")] public float EliteDropPercent { get; private set; } = 10f;
		[field: SerializeField, Range(0f, 100f), Tooltip("보스 처치 때 백분율")] public float BossDropPercent { get; private set; } = 50f;
		[field: SerializeField, Range(0f, 1f), Tooltip("떨어질 때 종이 두루마리일 몫. 나머지는 번갈아")] public float PaperShare { get; private set; } = 0.5f;

		[field: Header("든 것")]
		[field: SerializeField, Tooltip("마법 칸이 가리키는 하찮은 마법 목록")] public SpellListSO Spells { get; private set; }
		[field: SerializeField] public List<ScrollTableRow> Rows { get; private set; } = new();

		/// <summary>만드는 법이면 아이템 이름, 마법이면 목록의 이름</summary>
		public string DisplayName(ScrollTableRow row) => row.Kind == ScrollResultKind.Recipe ? row.RecipeItem.Name : Spells.Get(row.SpellId).Name;

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
		[field: SerializeField, Tooltip("마법일 때 하찮은 마법 목록의 번호")] public int SpellId { get; private set; }
		[field: SerializeField, Tooltip("만드는 법일 때 레시피를 가진 아이템")] public ItemData RecipeItem { get; private set; }
		[field: SerializeField, Min(1), Tooltip("배운 것까지 합친 모습 수")] public int Forms { get; private set; } = 1;

		public ScrollEntry ToEntry() => new()
		{
			Id = Id,
			Kind = Kind,
			Target = Kind == ScrollResultKind.Recipe ? RecipeItem.ID : SpellId,
			Forms = Forms,
		};
	}
}
