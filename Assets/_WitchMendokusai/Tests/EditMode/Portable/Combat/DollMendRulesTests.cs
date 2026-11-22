using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DollMendRulesTests
	{
		[Test]
		public void 실_카드가_주기를_줄이되_최소_주기에서_멈춘다()
		{
			Assert.AreEqual(4, DollMendRules.Interval(4, 0, 1));
			Assert.AreEqual(2, DollMendRules.Interval(4, 2, 1));
			Assert.AreEqual(1, DollMendRules.Interval(4, 9, 1));
			Assert.AreEqual(2, DollMendRules.Interval(4, 9, 2));
			Assert.AreEqual(4, DollMendRules.Interval(4, -3, 1), "음수 감소는 무시");
		}

		[Test]
		public void 주기의_배수_레벨업에서_수선한다()
		{
			Assert.IsFalse(DollMendRules.TriggersAt(0, 3));
			Assert.IsFalse(DollMendRules.TriggersAt(2, 3));
			Assert.IsTrue(DollMendRules.TriggersAt(3, 3));
			Assert.IsTrue(DollMendRules.TriggersAt(6, 3));
			Assert.IsTrue(DollMendRules.TriggersAt(1, 1));
		}

		[Test]
		public void 양은_기본에_세기를_더하고_0_아래로_안_간다()
		{
			Assert.AreEqual(5, DollMendRules.Amount(2, 3));
			Assert.AreEqual(0, DollMendRules.Amount(2, -9));
		}

		[Test]
		public void 가장_덜_키운_장비를_고르고_같으면_앞_번호()
		{
			Assert.AreEqual(1, DollMendRules.PickLeastGrown(new[] { 4, 1, 3 }));
			Assert.AreEqual(0, DollMendRules.PickLeastGrown(new[] { 2, 2, 5 }));
		}

		[Test]
		public void 음수는_후보가_아니고_후보가_없으면_없음()
		{
			Assert.AreEqual(2, DollMendRules.PickLeastGrown(new[] { -1, 5, 0 }));
			Assert.AreEqual(-1, DollMendRules.PickLeastGrown(new[] { -1, -1 }));
			Assert.AreEqual(-1, DollMendRules.PickLeastGrown(new int[0]));
		}
	}
}
