using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class MendRulesTests
	{
		[Test]
		public void 실_카드가_주기를_줄이되_최소_주기에서_멈춘다()
		{
			Assert.AreEqual(4, MendRules.Interval(4, 0, 1));
			Assert.AreEqual(2, MendRules.Interval(4, 2, 1));
			Assert.AreEqual(1, MendRules.Interval(4, 9, 1));
			Assert.AreEqual(2, MendRules.Interval(4, 9, 2));
			Assert.AreEqual(4, MendRules.Interval(4, -3, 1), "음수 감소는 무시");
		}

		[Test]
		public void 실_카드를_받아야_켜지고_첫_장은_기본_주기()
		{
			Assert.IsFalse(MendRules.IsActive(0));
			Assert.IsTrue(MendRules.IsActive(1));
			Assert.AreEqual(4, MendRules.IntervalFromCards(4, 1, 1));
			Assert.AreEqual(3, MendRules.IntervalFromCards(4, 2, 1));
			Assert.AreEqual(2, MendRules.IntervalFromCards(4, 3, 1));
		}

		[Test]
		public void 주기의_배수_레벨업에서_수선한다()
		{
			Assert.IsFalse(MendRules.TriggersAt(0, 3));
			Assert.IsFalse(MendRules.TriggersAt(2, 3));
			Assert.IsTrue(MendRules.TriggersAt(3, 3));
			Assert.IsTrue(MendRules.TriggersAt(6, 3));
			Assert.IsTrue(MendRules.TriggersAt(1, 1));
		}

		[Test]
		public void 양은_기본에_세기를_더하고_0_아래로_안_간다()
		{
			Assert.AreEqual(5, MendRules.Amount(2, 3));
			Assert.AreEqual(0, MendRules.Amount(2, -9));
		}

		[Test]
		public void 가장_덜_키운_장비를_고르고_같으면_뒤_번호()
		{
			Assert.AreEqual(1, MendRules.PickLeastGrown(new[] { 4, 1, 3 }));
			Assert.AreEqual(1, MendRules.PickLeastGrown(new[] { 2, 2, 5 }));
			Assert.AreEqual(2, MendRules.PickLeastGrown(new[] { 0, 0, 0 }), "판 초반 동률은 전용 무기보다 뒤 장비");
		}

		[Test]
		public void 양은_대상_스탯_수로_나누고_나머지는_앞부터()
		{
			CollectionAssert.AreEqual(new[] { 1, 1 }, MendRules.SplitAmount(2, 2));
			CollectionAssert.AreEqual(new[] { 2, 1 }, MendRules.SplitAmount(3, 2));
			CollectionAssert.AreEqual(new[] { 3 }, MendRules.SplitAmount(3, 1));
			CollectionAssert.AreEqual(new[] { 1, 0, 0 }, MendRules.SplitAmount(1, 3));
			CollectionAssert.AreEqual(new int[0], MendRules.SplitAmount(2, 0));
		}

		[Test]
		public void 음수는_후보가_아니고_후보가_없으면_없음()
		{
			Assert.AreEqual(2, MendRules.PickLeastGrown(new[] { -1, 5, 0 }));
			Assert.AreEqual(1, MendRules.PickLeastGrown(new[] { 3, 3, -1 }));
			Assert.AreEqual(-1, MendRules.PickLeastGrown(new[] { -1, -1 }));
			Assert.AreEqual(-1, MendRules.PickLeastGrown(new int[0]));
		}
	}
}
