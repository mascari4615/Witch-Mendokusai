using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;
using static WitchMendokusai.GameDefine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public static class GameLogic
	{
		public static Vector3 GetRandomSpawnPosOffset(Vector3 position, float offset = LootItemSpawnPosOffsetXZ)
		{
			Vector3 randomOffset = new(Random.Range(-offset, offset), LootItemSpawnPosOffsetY, Random.Range(-offset, offset));
			return position + randomOffset;
		}

		public static void SpawnExpOrb(Vector3 position)
		{
			GameObject exp = ObjectPoolManager.Instance.Spawn(
				ResourceManager.Instance.EXPPrefab,
				GetRandomSpawnPosOffset(position)
			);
			exp.SetActive(true);
		}

		public static void SpawnLootItem(List<DataSOWithPercentage> lootTable, Vector3 position)
		{
			Probability<ItemData> probability = new(shouldFill100Percent: true);
			foreach (DataSOWithPercentage item in lootTable)
			{
				if (item.DataSO == null)
				{
					Debug.LogError("DataSO is null");
					continue;
				}
				probability.Add(item.DataSO as ItemData, item.Percentage);
			}

			ItemData dropItem = probability.Get();

			if (dropItem == default)
			{
				return;
			}

			GameObject lootItem = ObjectPoolManager.Instance.Spawn(
				ResourceManager.Instance.LootItemPrefab,
				GetRandomSpawnPosOffset(position)
			);
			lootItem.SetActive(true);
			lootItem.GetComponent<ItemObject>().Init(dropItem);
		}

		public static void SpawnGameItem(Vector3 position)
		{
			Probability<GameItemObject> gameItemProbability = new(shouldFill100Percent: true);
			gameItemProbability.Add(ResourceManager.Instance.HealObjectPrefab, HealPercentage);
			gameItemProbability.Add(ResourceManager.Instance.MagnetObjectPrefab, MagnetPercentage);

			GameItemObject gameItem = gameItemProbability.Get();

			if (gameItem == null)
			{
				return;
			}

			GameObject gameItemObject = ObjectPoolManager.Instance.Spawn(
				gameItem.gameObject,
				GetRandomSpawnPosOffset(position)
			);
			gameItemObject.SetActive(true);
		}
	}
}