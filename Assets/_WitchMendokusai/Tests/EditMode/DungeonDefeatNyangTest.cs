using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class DungeonDefeatNyangTest
	{
		private Dungeon dungeon;

		[SetUp]
		public void SetUp()
		{
			dungeon = ScriptableObject.CreateInstance<Dungeon>();
			SerializedObject serialized = new(dungeon);
			serialized.FindProperty("<DefeatNyangPerKill>k__BackingField").floatValue = 0.5f;
			serialized.FindProperty("<DefeatNyangMax>k__BackingField").intValue = 10;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(dungeon);

		[TestCase(0, 0)]
		[TestCase(1, 0)]
		[TestCase(10, 5)]
		[TestCase(100, 10)]
		public void Reward_comes_from_kills_up_to_max(int killCount, int expected)
		{
			Assert.AreEqual(expected, dungeon.CalculateDefeatNyang(killCount));
		}
	}
}
