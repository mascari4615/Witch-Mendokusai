using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WitchMendokusai
{
	public static class SOHelper
	{
		public static ItemData GetItemData(int id) => Get<ItemData>(id);
		public static Doll GetDoll(int id) => Get<Doll>(id);
		public static QuestSO GetQuestSO(int id) => Get<QuestSO>(id);
		public static DungeonConstraint GetDungeonConstraint(int id) => Get<DungeonConstraint>(id);
		public static Dungeon GetDungeon(int id) => Get<Dungeon>(id);
		public static NPC GetNPC(int id) => Get<NPC>(id);
		public static UnitStatData GetUnitStatData(UnitStatType unitStatType) => Get<UnitStatData>((int)unitStatType);
		public static UnitStatData GetUnitStatData(int id) => Get<UnitStatData>(id);
		public static GameStatData GetGameStatData(GameStatType gameStatType) => Get<GameStatData>((int)gameStatType);
		public static GameStatData GetGameStatData(int id) => Get<GameStatData>(id);
		public static DungeonStatData GetDungeonStatData(DungeonStatType dungeonStatType) => Get<DungeonStatData>((int)dungeonStatType);
		public static DungeonStatData GetDungeonStatData(int id) => Get<DungeonStatData>(id);
		public static CardData GetCardData(int id) => Get<CardData>(id);

		public static void ForEach<T>(Action<T> action) where T : DataSO
		{
			foreach (DataSO dataSO in SOManager.Instance.DataSOs[typeof(T)].Values)
				action(dataSO as T);
		}

		public static int CountOf<T>() where T : DataSO => SOManager.Instance.DataSOs[typeof(T)].Count;

		/// <summary>
		/// 주어진 타입의 DataSO 스크립터블 오브젝트를 가져옵니다
		/// </summary>
		public static T Get<T>(int id) where T : DataSO => SOManager.Instance.DataSOs[typeof(T)][id] as T;

		// 아래 코드는 불가능
		// 왜 WHY : 제네릭 타입의 변환에 제한, C#의 타입 안전성을 보장하기 위한.
		// i.e. Dic<int, DataSO>를 Dic<int, ItemData>로 캐스팅하고, DataSO를 Add하려고 하면, 이는 ItemData 타입이 아니므로 문제가 발생
		// public static Dictionary<int, T> GetDictionary<T>() where T : DataSO => SOManager.Instance.DataSOs[typeof(T)] as Dictionary<int, T>;
	}
}