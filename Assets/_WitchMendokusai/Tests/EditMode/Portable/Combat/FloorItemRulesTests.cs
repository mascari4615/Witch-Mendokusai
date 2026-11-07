using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class FloorItemRulesTests
	{
		[Test]
		public void FreezeStartsFromNow()
		{
			Assert.AreEqual(18f, FloorItemRules.FreezeEndTime(0f, 10f, 8f), 0.0001f);
		}

		[Test]
		public void FreezeRefreshDoesNotStack()
		{
			float end = FloorItemRules.FreezeEndTime(0f, 10f, 8f);
			Assert.AreEqual(21f, FloorItemRules.FreezeEndTime(end, 13f, 8f), 0.0001f);
			Assert.AreEqual(18f, FloorItemRules.FreezeEndTime(end, 10f, 4f), 0.0001f);
		}

		[Test]
		public void ZeroSecondsKeepsCurrentEnd()
		{
			Assert.AreEqual(5f, FloorItemRules.FreezeEndTime(5f, 10f, 0f), 0.0001f);
		}

		[Test]
		public void FrozenUntilEndTime()
		{
			Assert.IsTrue(FloorItemRules.IsFrozen(17.9f, 18f));
			Assert.IsFalse(FloorItemRules.IsFrozen(18f, 18f));
		}

		[Test]
		public void BossFreezeIsShorterOrImmune()
		{
			Assert.AreEqual(2f, FloorItemRules.BossFreezeSeconds(8f, 0.25f), 0.0001f);
			Assert.AreEqual(0f, FloorItemRules.BossFreezeSeconds(8f, 0f), 0.0001f);
			Assert.AreEqual(8f, FloorItemRules.BossFreezeSeconds(8f, 3f), 0.0001f);
		}

		[Test]
		public void ClearSkipsBossAndElite()
		{
			Assert.IsTrue(FloorItemRules.ClearsTarget(false, false));
			Assert.IsFalse(FloorItemRules.ClearsTarget(true, false));
			Assert.IsFalse(FloorItemRules.ClearsTarget(false, true));
		}
	}
}
