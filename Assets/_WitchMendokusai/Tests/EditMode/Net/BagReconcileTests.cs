using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 화면 가방을 세계에 맞추는 계산 (TASK-WM-218) — 틀리면 아이템이 불어나거나 사라진다.
	/// </summary>
	public sealed class BagReconcileTests
	{
		private static Dictionary<int, int> Bag(params (int id, int amount)[] entries)
		{
			Dictionary<int, int> bag = new Dictionary<int, int>();
			foreach ((int id, int amount) entry in entries)
				bag[entry.id] = entry.amount;

			return bag;
		}

		[Test]
		public void 이미_맞으면_아무것도_안_한다()
		{
			List<BagAdjustment> plan = BagReconcile.Plan(Bag((1, 3)), Bag((1, 3)));

			Assert.That(plan, Is.Empty);
		}

		[Test]
		public void 부족하면_그만큼만_채운다()
		{
			List<BagAdjustment> plan = BagReconcile.Plan(Bag((1, 2)), Bag((1, 5)));

			Assert.That(plan.Count, Is.EqualTo(1));
			Assert.That(plan[0].Add, Is.EqualTo(3));
			Assert.That(plan[0].Remove, Is.EqualTo(0));
		}

		[Test]
		public void 남으면_그만큼만_뺀다()
		{
			List<BagAdjustment> plan = BagReconcile.Plan(Bag((1, 9)), Bag((1, 4)));

			Assert.That(plan[0].Remove, Is.EqualTo(5));
			Assert.That(plan[0].Add, Is.EqualTo(0));
		}

		[Test]
		public void 세계가_모르는_건_뺀다()
		{
			// 안 빼면 쓴 것이 화면에서 되살아난다.
			List<BagAdjustment> plan = BagReconcile.Plan(Bag((7, 2)), Bag((1, 1)));

			Assert.That(plan.Count, Is.EqualTo(2));
			Assert.That(plan.Find(p => p.ItemId == 7).Remove, Is.EqualTo(2));
		}

		[Test]
		public void 처음_들어온_창은_세계_것을_그대로_받는다()
		{
			List<BagAdjustment> plan = BagReconcile.Plan(null, Bag((1, 4), (2, 1)));

			Assert.That(plan.Count, Is.EqualTo(2));
			Assert.That(plan.Find(p => p.ItemId == 1).Add, Is.EqualTo(4));
		}

		[Test]
		public void 음수는_0으로_본다()
		{
			List<BagAdjustment> plan = BagReconcile.Plan(Bag((1, 2)), Bag((1, -5)));

			Assert.That(plan[0].Remove, Is.EqualTo(2));
		}
	}
}
