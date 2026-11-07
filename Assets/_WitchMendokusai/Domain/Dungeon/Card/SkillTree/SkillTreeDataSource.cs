using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 트리 파일이 가리키는 ID 를 게임 자료로 해석
	public interface ISkillTreeDataSource
	{
		bool TryGetCard(int id, out CardData card);
		bool TryGetEquipmentCards(int equipmentId, out IReadOnlyList<CardData> cards);
		bool TryGetEquipmentSprite(int equipmentId, out Sprite sprite);
		bool TryGetEffectTarget(EffectType type, string target, out DataSO data);
	}

	public class RuntimeSkillTreeDataSource : ISkillTreeDataSource
	{
		public bool TryGetCard(int id, out CardData card)
		{
			bool isFound = TryGet(typeof(CardData), id, out DataSO data);
			card = data as CardData;
			return isFound;
		}

		public bool TryGetEquipmentCards(int equipmentId, out IReadOnlyList<CardData> cards)
		{
			cards = null;
			if (TryGet(typeof(ItemData), equipmentId, out DataSO data) == false || data is not EquipmentData equipment)
				return false;
			cards = equipment.EffectCards;
			return true;
		}

		public bool TryGetEquipmentSprite(int equipmentId, out Sprite sprite)
		{
			bool isFound = TryGet(typeof(ItemData), equipmentId, out DataSO data);
			sprite = isFound ? data.Sprite : null;
			return isFound;
		}

		public bool TryGetEffectTarget(EffectType type, string target, out DataSO data)
		{
			data = null;
			switch (type)
			{
				case EffectType.UnitStat:
					return Enum.TryParse(target, out UnitStatType unitStat) && TryGet(typeof(UnitStatData), (int)unitStat, out data);
				case EffectType.GameStat:
					return Enum.TryParse(target, out GameStatType gameStat) && TryGet(typeof(GameStatData), (int)gameStat, out data);
				case EffectType.DungeonStat:
					return Enum.TryParse(target, out DungeonStatType dungeonStat) && TryGet(typeof(DungeonStatData), (int)dungeonStat, out data);
				default:
					return false;
			}
		}

		private static bool TryGet(Type type, int id, out DataSO data)
		{
			data = null;
			return SOManagerBridge.DataSOs.TryGetValue(type, out Dictionary<int, DataSO> dataSOs) && dataSOs.TryGetValue(id, out data);
		}
	}
}
