using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public static class DataSODefine
	{
		public const int ID_MAX = 100_000_000;

		// 갈래 전용 SO 는 갈래가 담는다 (아래 static 생성자). 여기 목록은 어느 갈래에도 안 매인 것들
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
			{ typeof(MinigameEntrySO), "MGE" },    // TASK-WM-195 — 티메토 허브 엔트리. 미등록 시 DataLoader 순회 제외 → 허브가 무음으로 텅 빔.
		};

		static DataSODefine()
		{
			for (int index = 0; index < FeatureManifest.Installers.Count; index++)
			{
				FeatureManifest.Installers[index].RegisterDataTypes(AssetPrefixes);
			}
		}

		public static readonly Dictionary<Type, string> AssetFolderOverride = new()
		{
			{ typeof(UpgradeData), "Assets/_WitchMendokusai/Domain/Upgrade/ScriptableObject" },
		};
	}
}