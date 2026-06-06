using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-182 트랙③ 사냥 — 마수 멧돼지(MOB_18220) 전리품 테이블 회귀 락.
	///
	/// 사냥 도살 시 떨어지는 재료(마수 고기 18211 / 거친 가죽 18212 / 마수 뼈 18213)가 Monster SO 에
	/// 정확히 wired + 각 가중치 > 0 인지. <see cref="WildBeastObject"/> 가 <see cref="MonsterObject.DropLoot"/>
	/// (IsDungeon 게이트 무시)로 이 테이블을 gameLogic.SpawnLootItem 에 넘김 — 데이터 끊기면 야외 사냥 드랍 0.
	///
	/// 가중치 수치(5/3/2)는 designer-tweakable(수치노출 룰)이라 락 X — 3종 wiring + weight>0 만 회귀 잠금.
	/// flee 방향은 <see cref="WildBeastFleeTest"/>. 야수 prefab/아트 = 사용자 Grey Box(미생성).
	/// </summary>
	public sealed class WildBeastLootTableTest
	{
		private const int MOB_MASU_BOAR = 18220;
		private static readonly int[] EXPECTED_LOOT_IDS = { 18211, 18212, 18213 };

		[Test]
		public void MasuBoar_LootTable_WiresThreeBeastMaterials()
		{
			Monster boar = LoadMonsterById(MOB_MASU_BOAR);
			Assert.That(boar, Is.Not.Null, $"MOB_{MOB_MASU_BOAR} 마수멧돼지 Monster SO 를 못 찾음");

			List<DataSOWithPercentage> loots = boar.Loots;
			Assert.That(loots, Is.Not.Null, "Loots 가 null");
			Assert.That(loots.Count, Is.EqualTo(3), "마수멧돼지 전리품 = 3종(고기/가죽/뼈)이어야 함");

			List<int> lootIds = new();
			foreach (DataSOWithPercentage entry in loots)
			{
				Assert.That(entry.DataSO, Is.Not.Null, "전리품 DataSO null — 참조 끊김");
				Assert.That(entry.DataSO, Is.InstanceOf<ItemData>(), $"{entry.DataSO.name} 은 ItemData 여야 함");
				Assert.That(entry.Percentage, Is.GreaterThan(0f), $"{entry.DataSO.name} 가중치 0 = 절대 안 떨어짐");
				lootIds.Add(((ItemData)entry.DataSO).ID);
			}

			foreach (int expectedId in EXPECTED_LOOT_IDS)
				Assert.That(lootIds, Contains.Item(expectedId), $"전리품 ID {expectedId} 누락 (테이블 wiring 끊김)");
		}

		private static Monster LoadMonsterById(int id)
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Monster"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(path);
				if (monster != null && monster.ID == id)
					return monster;
			}
			return null;
		}
	}
}
