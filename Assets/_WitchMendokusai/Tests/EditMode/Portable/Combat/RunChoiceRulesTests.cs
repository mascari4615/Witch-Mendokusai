using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class RunChoiceRulesTests
	{
		[TestCase(3, 0, 3)]
		[TestCase(3, 2, 5)]
		[TestCase(2, 5, 7)]
		[TestCase(0, 0, 0)]
		public void StartChargesAddsUpgradeBonus(int baseCharges, int bonus, int expected)
		{
			Assert.AreEqual(expected, RunChoiceRules.StartCharges(baseCharges, bonus));
		}

		[Test]
		public void StartChargesIgnoresNegativeValues()
		{
			Assert.AreEqual(2, RunChoiceRules.StartCharges(2, -3));
			Assert.AreEqual(1, RunChoiceRules.StartCharges(-2, 1));
		}

		[Test]
		public void GuaranteedChestIgnoresChance()
		{
			Assert.IsTrue(RunChoiceRules.DropsEliteChest(true, 0.99f, 0f));
		}

		[Test]
		public void NormalChestFollowsChance()
		{
			Assert.IsTrue(RunChoiceRules.DropsEliteChest(false, 0.5f, 1f));
			Assert.IsFalse(RunChoiceRules.DropsEliteChest(false, 0.5f, 0.25f));
			Assert.IsFalse(RunChoiceRules.DropsEliteChest(false, 0f, 0f));
		}
	}
}
