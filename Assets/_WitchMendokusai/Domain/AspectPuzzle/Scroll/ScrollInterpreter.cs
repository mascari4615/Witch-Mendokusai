using System;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>두루마리를 풀 때 쓰는 퍼즐. 겉에서 보임 (기획 정본 결정 2-4 의 임시값)</summary>
	public enum ScrollPuzzleKind
	{
		Paper = 0,
		Turn = 1,
	}

	/// <summary>
	/// 봉인된 두루마리 하나를 퍼즐로 풀고 결과를 남기는 자리. 규칙은 DomainSDK 의 ScrollRules, 여기는 가방과 세이브를 잇기만.
	/// 퍼즐을 다 푼 뒤에만 부름. 풀다 닫으면 두루마리는 그대로.
	/// 마법은 받을 곳 (하찮은 마법 목록과 마법 창) 이 아직 없어 이 갈래 세이브에만 남음. 다음 단계에서 그 목록으로 옮김
	/// </summary>
	public sealed class ScrollInterpreter
	{
		private readonly ScrollTableSO table;
		private readonly Func<Inventory> inventory;
		private readonly DataManager dataManager;
		private readonly Func<int, int> pick;
		private readonly List<ScrollEntry> entries;

		/// <param name="inventory">가방. 세이브를 다시 불러와도 맞게 매번 물어봄</param>
		public ScrollInterpreter(ScrollTableSO table, Func<Inventory> inventory, DataManager dataManager, Func<int, int> pick)
		{
			this.table = table;
			this.inventory = inventory;
			this.dataManager = dataManager;
			this.pick = pick;
			entries = table.ToEntries();
		}

		// 세이브를 다시 불러오면 조각의 Knowledge 가 바뀌므로 매번 읽음
		private ScrollKnowledge Knowledge
		{
			get
			{
				ScrollKnowledge knowledge = dataManager.FeatureSave<ScrollSaveSlice>().Knowledge;
				LearnUnlockedRecipes(knowledge);
				return knowledge;
			}
		}

		public int Count(ScrollPuzzleKind kind) => inventory().CountByID(ScrollOf(kind).ID);

		/// <summary>새로 열 것이 남았는지. 없으면 두루마리는 팔거나 분해할 아이템일 뿐</summary>
		public bool HasAnythingLeft => ScrollRules.CanInterpret(entries, Knowledge);

		public bool CanStart(ScrollPuzzleKind kind) => Count(kind) > 0 && HasAnythingLeft;

		/// <summary>퍼즐을 푼 뒤. 든 것을 뽑아 남기고 두루마리 하나를 씀</summary>
		public ScrollOutcome Complete(ScrollPuzzleKind kind)
		{
			if (CanStart(kind) == false)
			{
				throw new InvalidOperationException($"두루마리 {kind}: 가진 것 {Count(kind)}, 남은 것 {HasAnythingLeft}. 시작 전에 CanStart 를 봐야 함");
			}
			ScrollKnowledge knowledge = Knowledge;
			if (ScrollRules.TryDraw(entries, knowledge, pick, out ScrollDraw draw) == false)
			{
				throw new InvalidOperationException($"두루마리 {kind}: CanStart 는 됐는데 뽑을 것이 없음");
			}
			// 기록이 먼저. 쓰기에서 던지면 두루마리만 사라지고 얻은 것이 없어짐
			ScrollRules.Apply(knowledge, draw);
			if (draw.Entry.Kind == ScrollResultKind.Recipe)
			{
				dataManager.IsRecipeUnlocked[draw.Entry.Target] = true;
			}
			int missing = inventory().Consume(ScrollOf(kind).ID, 1);
			if (missing != 0)
			{
				throw new InvalidOperationException($"두루마리 {kind}: 하나를 쓰려 했는데 {missing} 개 모자람");
			}
			return new ScrollOutcome(table.Row(draw.Entry.Id), draw);
		}

		// 다른 길 (대화의 레시피 보상 등) 로 이미 연 만드는 법은 배운 것으로 침. 아니면 두루마리가 "배움" 으로 같은 것을 다시 줌
		private void LearnUnlockedRecipes(ScrollKnowledge knowledge)
		{
			foreach (ScrollEntry entry in entries)
			{
				if (entry.Kind == ScrollResultKind.Recipe && knowledge.IsLearned(entry.Id) == false && dataManager.IsItemUnlocked(entry.Target))
				{
					knowledge.OpenedForms[entry.Id] = 1;
				}
			}
		}

		private ItemData ScrollOf(ScrollPuzzleKind kind) => kind == ScrollPuzzleKind.Paper ? table.PaperScroll : table.TurnScroll;
	}

	public readonly struct ScrollOutcome
	{
		public ScrollOutcome(ScrollTableRow row, ScrollDraw draw)
		{
			Row = row;
			Draw = draw;
		}

		public ScrollTableRow Row { get; }
		public ScrollDraw Draw { get; }
	}
}
