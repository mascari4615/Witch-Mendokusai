using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 연구 성좌 화면의 *상태* 회귀 잠금 (TASK-WM-194).
	///
	/// ★ 왜 화면 클래스에 시험을 다나: 이어하기(저장 → 복원)의 정본이 이 화면이다. 화면이 자국을
	///   들고 있고 매치가 물어서 적으므로, 여기가 어긋나면 **판을 이어할 때 연구가 사라지거나
	///   두 번 쌓인다** — 사람이 눈으로 잡기 가장 어려운 종류다.
	/// ★ 패널 없이 검사한다 — Build 는 붙일 곳만 있으면 도므로 시험에서 화면 없이 세울 수 있다.
	///   (클릭 경로는 패널이 필요해 EditorWindow 미리보기에서 따로 확인한다.)
	/// </summary>
	public class TowerDefenseResearchViewTests
	{
		private static TowerDefenseResearchView Build()
		{
			VisualElement host = new VisualElement();
			TowerDefenseResearchView view = new TowerDefenseResearchView();
			view.Build(host, branchCount: 6, ringCount: 5, majorAmount: 0.35f, minorAmount: 0.08f, nodeCost: 2,
				essenceFromRing: 2, resourceNodeCost: 45);
			return view;
		}

		[Test]
		public void 새로_세우면_찍힌_것이_없다()
		{
			TowerDefenseResearchView view = Build();

			List<int> taken = new();
			view.CollectTaken(taken);

			Assert.AreEqual(0, taken.Count, "코어는 늘 있는 것이라 자국에 안 들어간다.");
		}

		[Test]
		public void 되돌리면_그대로_돌아온다()
		{
			TowerDefenseResearchView view = Build();
			List<int> saved = new() { 1, 2, 3 };

			view.RestoreTaken(saved);
			List<int> loaded = new();
			view.CollectTaken(loaded);

			CollectionAssert.AreEquivalent(saved, loaded);
		}

		[Test]
		public void 되돌리기는_앞의_것을_지운다()
		{
			// 지우지 않으면 이어할 때마다 자국이 쌓여, 두 번 이어한 판이 저절로 다 뚫린다.
			TowerDefenseResearchView view = Build();

			view.RestoreTaken(new List<int> { 1, 2, 3 });
			view.RestoreTaken(new List<int> { 7 });

			List<int> loaded = new();
			view.CollectTaken(loaded);
			CollectionAssert.AreEquivalent(new List<int> { 7 }, loaded);
		}

		[Test]
		public void 처음으로_되돌리면_코어만_남는다()
		{
			TowerDefenseResearchView view = Build();
			view.RestoreTaken(new List<int> { 1, 2, 3 });

			view.ResetTaken();

			List<int> loaded = new();
			view.CollectTaken(loaded);
			Assert.AreEqual(0, loaded.Count);
		}

		[Test]
		public void 값을_못_치르면_찍은_것이_지워진다()
		{
			// 「찍힌 척」이 남으면 그 뒤 마디가 잘못 열려 화면과 규칙이 갈라진다.
			TowerDefenseResearchView view = Build();
			view.RestoreTaken(new List<int> { 1 });

			view.Undo(1);

			List<int> loaded = new();
			view.CollectTaken(loaded);
			Assert.AreEqual(0, loaded.Count);
		}

		[Test]
		public void 마디를_번호로_찾을_수_있다()
		{
			TowerDefenseResearchView view = Build();

			Assert.IsTrue(view.TryGetNode(1, out TowerDefenseResearchGraph.Node node));
			Assert.AreEqual(1, node.Id);
			Assert.Greater(node.Cost, 0, "값이 0이면 무한히 찍을 수 있다.");
		}
	}
}
