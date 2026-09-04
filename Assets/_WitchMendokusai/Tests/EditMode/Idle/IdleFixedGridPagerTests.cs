using NUnit.Framework;
using WitchMendokusai.Idle.UI;

namespace WitchMendokusai.Tests.Idle
{
	public sealed class IdleFixedGridPagerTests
	{
		[TestCase(0, 1)]
		[TestCase(24, 1)]
		[TestCase(25, 2)]
		[TestCase(48, 2)]
		public void PageCountKeepsTwentyFourFixedCells(int itemCount, int expectedPages)
		{
			Assert.AreEqual(expectedPages, FixedGridPager.PageCount(itemCount, 24));
		}

		[Test]
		public void SecondPageStartsAfterFirstTwentyFourCells()
		{
			Assert.AreEqual(24, FixedGridPager.ItemIndex(1, 0, 24));
			Assert.AreEqual(47, FixedGridPager.ItemIndex(1, 23, 24));
		}

		[Test]
		public void PageClampsWhenItemCountShrinks()
		{
			Assert.AreEqual(0, FixedGridPager.ClampPage(3, 8, 24));
		}
	}
}
