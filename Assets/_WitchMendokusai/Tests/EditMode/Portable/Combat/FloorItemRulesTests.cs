using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class FloorItemRulesTests
	{
		[Test]
		public void ClearSkipsBossAndElite()
		{
			Assert.IsTrue(FloorItemRules.ClearsTarget(false, false));
			Assert.IsFalse(FloorItemRules.ClearsTarget(true, false));
			Assert.IsFalse(FloorItemRules.ClearsTarget(false, true));
		}
	}
}
