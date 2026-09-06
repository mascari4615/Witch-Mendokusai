using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;
using static WitchMendokusai.GameDefine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// TASK-WM-120 γ — static class → 주입 서비스. ObjectPoolManager(DI 매니저)
	// 의 static `.Instance` reach 제거 = Composition Root graph-derived. POCO
	// 서비스로 RootLifetimeScope 등록(EffectRunner 패턴), DI-거주 caller 4종이
	// [Inject]. ResourceManager 는 :ScriptableObject = SO 데이터 컨테이너
	// (SOManager/RegisterInstance 카테고리, γ 대상 매니저 아님) → SO 패턴 유지.
	public class GameLogic
	{
		private readonly ObjectPoolManager objectPoolManager;
		private readonly ResourceManager resourceManager;

		[Inject]
		public GameLogic(ObjectPoolManager objectPoolManager, ResourceManager resourceManager)
		{
			this.objectPoolManager = objectPoolManager;
			this.resourceManager = resourceManager;
		}

		public static Vector3 GetRandomSpawnPosOffset(Vector3 position, float offset = LOOT_ITEM_SPAWN_POS_OFFSET_XZ)
		{
			Vector3 randomOffset = new(Random.Range(-offset, offset), LOOT_ITEM_SPAWN_POS_OFFSET_Y, Random.Range(-offset, offset));
			return position + randomOffset;
		}

		public void SpawnExpOrb(Vector3 position)
		{
			GameObject exp = objectPoolManager.Spawn(
				resourceManager.EXPPrefab,
				GetRandomSpawnPosOffset(position)
			);
			exp.SetActive(true);
		}

		public void SpawnLootItem(List<DataSOWithPercentage> lootTable, Vector3 position)
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

			GameObject lootItem = objectPoolManager.Spawn(
				resourceManager.LootItemPrefab,
				GetRandomSpawnPosOffset(position)
			);
			lootItem.SetActive(true);
			lootItem.GetComponent<ItemObject>().Init(dropItem);
		}

		public void SpawnGameItem(Vector3 position)
		{
			Probability<GameItemObject> gameItemProbability = new(shouldFill100Percent: true);
			gameItemProbability.Add(resourceManager.HealObjectPrefab, HEAL_PERCENTAGE);
			gameItemProbability.Add(resourceManager.MagnetObjectPrefab, MAGNET_PERCENTAGE);

			GameItemObject gameItem = gameItemProbability.Get();

			if (gameItem == null)
			{
				return;
			}

			GameObject gameItemObject = objectPoolManager.Spawn(
				gameItem.gameObject,
				GetRandomSpawnPosOffset(position)
			);
			gameItemObject.SetActive(true);
		}
	}
}