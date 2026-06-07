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
		public static AspectData GetAspectData(AspectType aspectType) => Get<AspectData>((int)aspectType);
		public static AspectData GetAspectData(int id) => Get<AspectData>(id);
		public static CardData GetCardData(int id) => Get<CardData>(id);

		public static void ForEach<T>(Action<T> action) where T : DataSO
		{
			// 그 타입이 미로드(테스트/부분 컨텍스트 — MPPM 가상 플레이어 등)면 순회할 것 0 = no-op.
			// 종료 세이브(SaveManager.SaveData)서 미로드 타입에 KeyNotFound 던져 세이브 전체 크래시하던 것 방지.
			// 단건 Get<T>(id) 는 FastFail 유지(특정 SO 부재 = 실 버그).
			if (SOManagerBridge.DataSOs.TryGetValue(typeof(T), out Dictionary<int, DataSO> dataSOs) == false)
				return;
			foreach (DataSO dataSO in dataSOs.Values)
				action(dataSO as T);
		}

		public static int CountOf<T>() where T : DataSO =>
			SOManagerBridge.DataSOs.TryGetValue(typeof(T), out Dictionary<int, DataSO> dataSOs) ? dataSOs.Count : 0;

		/// <summary>
		/// 주어진 타입의 DataSO 스크립터블 오브젝트를 가져옵니다
		/// </summary>
		public static T Get<T>(int id) where T : DataSO => SOManagerBridge.DataSOs[typeof(T)][id] as T;

		// 아래 코드는 불가능
		// 왜 WHY : 제네릭 타입의 변환에 제한, C#의 타입 안전성을 보장하기 위한.
		// i.e. Dic<int, DataSO>를 Dic<int, ItemData>로 캐스팅하고, DataSO를 Add하려고 하면, 이는 ItemData 타입이 아니므로 문제가 발생
		// public static Dictionary<int, T> GetDictionary<T>() where T : DataSO => SOManagerBridge.DataSOs[typeof(T)] as Dictionary<int, T>;
	}
}