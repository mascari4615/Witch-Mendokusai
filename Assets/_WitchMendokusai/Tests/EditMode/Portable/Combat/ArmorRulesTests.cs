using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ArmorRulesTests
	{
		[TestCase(10, 0, 10)]
		[TestCase(10, -5, 10)]
		[TestCase(10, 100, 5)]
		[TestCase(9, 200, 3)]
		[TestCase(1, 1000, 1)]
		[TestCase(0, 100, 0)]
		public void Armor_reduces_damage(int damage, int armor, int expected) => Assert.AreEqual(expected, ArmorRules.Reduce(damage, armor));
	}
}
