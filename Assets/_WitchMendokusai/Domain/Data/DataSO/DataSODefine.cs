using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DataSODefine
	{
		public const int ID_MAX = 100_000_000;

		public static readonly Dictionary<Type, string> AssetPrefixes = new()
		{
			{ typeof(QuestSO), "Q" },
			{ typeof(CardData), "C" },
			{ typeof(ItemData), "I" },
			// { typeof(ObjectData), "O"},
			{ typeof(SkillData), "SKL" },
			{ typeof(UnitStatData), "USD"},
			{ typeof(AspectData), "AD" },
			{ typeof(GameStatData), "GSD"},
			{ typeof(WorldStage), "WS" },
			{ typeof(Dungeon), "D" },
			{ typeof(DungeonStatData), "DSD" },
			{ typeof(DungeonConstraint), "DC" },
			{ typeof(Doll), "DOL" },
			{ typeof(NPC), "NPC" },
			{ typeof(Monster), "MOB" },
			{ typeof(Building), "BD"},
			{ typeof(UpgradeData), "UPG" },
			{ typeof(EntityData), "ENT" },
			{ typeof(ChapterSO), "Chapter" },
			{ typeof(WitchPlantSO), "PLANT" },
			{ typeof(TowerDefenseStageSO), "TDS" }, // TASK-WM-194 증분4 — 미등록 시 DataSOAddressableSync 가 매 import 마다 LogError(무음 실패 트랩 회피).
		};

		public static readonly Dictionary<Type, string> AssetFolderOverride = new()
		{
			{ typeof(UpgradeData), "Assets/_WitchMendokusai/Domain/Upgrade/ScriptableObject" },
		};
	}
}