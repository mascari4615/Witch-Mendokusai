using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DataSODefine
	{
		public const string BASE_DIR = "Assets/_WitchMendokusai/";
		public const string EDITOR_DIR = BASE_DIR + "Editor/";
		public const int ID_MAX = 100_000_000;

		public static readonly Dictionary<Type, string> AssetPrefixes = new()
		{
			{ typeof(QuestSO), "Q" },
			{ typeof(CardData), "C" },
			{ typeof(ItemData), "I" },
			{ typeof(MonsterWave), "MW" },
			{ typeof(ObjectData), "O"},
			{ typeof(SkillData), "SKL" },
			{ typeof(UnitStatData), "USD"},
			{ typeof(GameStatData), "GSD"},
			{ typeof(WorldStage), "WS" },
			{ typeof(Dungeon), "D" },
			{ typeof(DungeonStatData), "DSD" },
			{ typeof(DungeonConstraint), "DC" },
			{ typeof(Doll), "DOL" },
			{ typeof(NPC), "NPC" },
			{ typeof(Monster), "MOB" },
			{ typeof(Building), "BD"}
		};
	}
}