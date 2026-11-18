using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class BreakableTableTest
	{
		private BreakableTable table;

		[SetUp]
		public void SetUp()
		{
			table = ScriptableObject.CreateInstance<BreakableTable>();
			table.DropChance = .8f;
			table.Drops = new[]
			{
				new BreakableDrop { Type = BreakableDropType.Nyang, Weight = 70f, Count = new Vector2Int(1, 3) },
				new BreakableDrop { Type = BreakableDropType.Heal, Weight = 20f, Count = Vector2Int.one },
				new BreakableDrop { Type = BreakableDropType.Exp, Weight = 10f, Count = Vector2Int.one },
			};
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(table);

		[TestCase(.8f, 0f)]
		[TestCase(.99f, .5f)]
		public void Chance_roll_above_drop_chance_drops_nothing(float chanceRoll, float weightRoll)
		{
			Assert.AreEqual(-1, table.Pick(chanceRoll, weightRoll));
		}

		[TestCase(0f, 0)]
		[TestCase(.69f, 0)]
		[TestCase(.71f, 1)]
		[TestCase(.89f, 1)]
		[TestCase(.91f, 2)]
		[TestCase(.999f, 2)]
		public void Weight_roll_picks_by_share(float weightRoll, int expected)
		{
			Assert.AreEqual(expected, table.Pick(0f, weightRoll));
		}

		[Test]
		public void Zero_total_weight_fails_fast()
		{
			table.Drops = new[] { new BreakableDrop { Type = BreakableDropType.Nyang, Weight = 0f } };
			Assert.Throws<System.InvalidOperationException>(() => table.Pick(0f, .5f));
		}

		[Test]
		public void Shipped_settings_have_links_and_weight()
		{
			BreakableSettings settings = Resources.Load<BreakableSettings>(nameof(BreakableSettings));
			Assert.IsNotNull(settings);
			Assert.IsNotNull(settings.NyangPrefab);
			Assert.IsNotNull(settings.BaseMaterial);
			foreach (BreakableKind kind in settings.Kinds)
			{
				Assert.IsNotNull(kind.Table, kind.Name);
				Assert.Greater(kind.Table.TotalWeight, 0f, kind.Name);
				Assert.GreaterOrEqual(kind.Palette.Length, BreakableShapes.RequiredPalette(kind.Shape), kind.Name);
			}
		}
	}
}
