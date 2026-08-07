using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「그 물건을 몇 개 이상 가졌나」를 대화가 묻는 통로 (TASK-WM-052).
	///
	/// ★ 왜 <see cref="ItemCountCriteria"/> 를 안 쓰나: 그쪽은 만들 때 `CriteriaContext`(SOManager 등)를
	///   받아야 한다. 그런데 원고에서 태어나는 조건은 **자산에 박힌 채로** 생기므로 생성자에 아무것도 못 받는다.
	///   그래서 「가진 개수를 알려 주는 좁은 구멍」 하나만 두고, 셈은 게임 쪽 그대로 쓴다(로직 복제 0).
	/// </summary>
	public interface IDialogueItemCountSource
	{
		int GetItemAmount(int itemId);
	}

	/// <summary>
	/// 물건 개수 창구 static accessor — `DialogueHistoryBridge` 동형.
	/// 등록 전이면 <see cref="TryGetAmount"/> 가 false — 부르는 쪽이 판단한다.
	/// </summary>
	public static class DialogueItemBridge
	{
		private static IDialogueItemCountSource source;

		public static void Register(IDialogueItemCountSource itemCountSource) => source = itemCountSource;

		public static void Clear(IDialogueItemCountSource itemCountSource)
		{
			if (source == itemCountSource)
			{
				source = null;
			}
		}

		public static IDialogueItemCountSource Current => source;

		public static bool TryGetAmount(int itemId, out int amount)
		{
			amount = 0;
			if (source == null)
			{
				return false;
			}
			amount = source.GetItemAmount(itemId);
			return true;
		}
	}

	/// <summary>
	/// 「물건 N개 이상 가졌나」 조건. 분기 노드·선택지에 그대로 꽂힌다(기존 <see cref="Criteria"/> 계열).
	///
	/// 쓰임새: 열쇠를 보여주는 선택지 · 재료가 있을 때만 나오는 대사.
	///
	/// ★ 창구가 아직 없으면 **「없다」로 친다.** 그쪽이 안전하다 — 조건부 선택지는 *안 뜨는* 쪽으로 넘어지고,
	///   있다고 우겼다가 없는 물건을 가져가는 대사가 나오는 것보다 낫다. (터뜨리지 않는 이유는
	///   저장 로딩 전 첫 프레임에 대화가 통째로 죽는 걸 막기 위해서 — 이력 조건과 같은 판단.)
	/// </summary>
	[Serializable]
	public class DialogueItemCriteria : Criteria
	{
		[SerializeField] private int itemId;

		[Tooltip("이만큼 이상이면 참. 1 이면 「가지고 있으면」.")]
		[SerializeField] private int minimumCount = 1;

		[Tooltip("체크 해제하면 「그만큼 없으면」이 참이 된다.")]
		[SerializeField] private bool expectedHave = true;

		public int ItemId { get => itemId; set => itemId = value; }
		public int MinimumCount { get => minimumCount; set => minimumCount = value; }
		public bool ExpectedHave { get => expectedHave; set => expectedHave = value; }

		public override int GetCurValue() => DialogueItemBridge.TryGetAmount(itemId, out int amount) ? amount : 0;

		public override int GetTargetValue() => minimumCount;

		public override bool Evaluate() => (GetCurValue() >= minimumCount) == expectedHave;
	}
}
