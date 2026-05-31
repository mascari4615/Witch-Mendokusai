using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-169 Phase 1 INC-A — 보관 상자 데이터 substrate 잠금.
	///
	/// 핵심 3가지:
	/// (1) ChestInventory(POCO IInventory) 의 Add/Remove/Find 의미가 메인 Inventory 와 동일,
	/// (2) ChestSaveData JSON round-trip 이 capacity + 슬롯(인덱스/ID/수량/Guid) 보존,
	/// (3) BuildingInstanceData.RuntimeData(string) 슬롯 ↔ GridData.Save/Load 와 함께 영속 — 즉
	///     상자 배치 → 아이템 입출 → save → load → 내용물 유지 (behavior-verify 의 EditMode 등가).
	///
	/// SOManager bootstrap 없이 돌아가도록 TestItemData fake 사용(WorldStageCitySaveTest 동일
	/// 정책 — 구조적 round-trip 만 검증, SO 해석 분리). Item.ToItem 의 SOHelper 호출 검증은 INC-B
	/// 의 실 ItemData asset 통합에서.
	/// </summary>
	public sealed class ChestInventoryTest
	{
		private sealed class TestItemData : IItemData
		{
			public int ID { get; }
			public int MaxAmount { get; }
			public ItemType Type => ItemType.None;
			public ItemGrade Grade => default;
			public bool IsCountable => MaxAmount != 1;

			public TestItemData(int id, int maxAmount)
			{
				ID = id;
				MaxAmount = maxAmount;
			}

			public Item CreateItem() => new(Guid.NewGuid(), this, 1);
		}

		[Test]
		public void Add_Countable_FillsExistingSlotThenOverflowsToEmpty()
		{
			ChestInventory chest = new(capacity: 4);
			TestItemData seed = new(id: 101, maxAmount: 10);

			int remaining = chest.Add(seed, 7);
			Assert.That(remaining, Is.Zero, "용량 충분 — 잉여 0");
			Assert.That(chest.GetItemAmount(101), Is.EqualTo(7));

			remaining = chest.Add(seed, 5);
			Assert.That(remaining, Is.Zero, "기존 슬롯 max=10 까지 채우고 빈 슬롯에 2 — 잉여 0");
			Assert.That(chest.GetItemAmount(101), Is.EqualTo(12));
			Assert.That(chest.GetItem(0).Amount, Is.EqualTo(10), "첫 슬롯 max");
			Assert.That(chest.GetItem(1).Amount, Is.EqualTo(2), "둘째 슬롯 잉여분");
		}

		[Test]
		public void Add_ExceedsCapacity_ReturnsRemainder()
		{
			ChestInventory chest = new(capacity: 2);
			TestItemData seed = new(id: 200, maxAmount: 5);

			int remaining = chest.Add(seed, 12);
			Assert.That(remaining, Is.EqualTo(2), "용량 2 슬롯 × max 5 = 10 수용 → 12 중 2 잉여");
			Assert.That(chest.GetItemAmount(200), Is.EqualTo(10));
		}

		[Test]
		public void Remove_DecrementsThenClearsEmptySlot()
		{
			ChestInventory chest = new(capacity: 4);
			TestItemData seed = new(id: 300, maxAmount: 10);
			chest.Add(seed, 5);

			chest.Remove(0, 2);
			Assert.That(chest.GetItem(0).Amount, Is.EqualTo(3));

			chest.Remove(0, 3);
			Assert.That(chest.GetItem(0), Is.Null, "0 도달 시 슬롯 클리어");
		}

		[Test]
		public void SaveData_RoundTrip_PreservesCapacityAndSlots()
		{
			ChestInventory original = new(capacity: 5);
			TestItemData seedA = new(id: 401, maxAmount: 99);
			TestItemData seedB = new(id: 402, maxAmount: 99);
			original.Add(seedA, 12);
			original.Add(seedB, 7);

			ChestSaveData saved = ChestSaveData.FromChest(original);
			string json = saved.ToJson();
			ChestSaveData reloaded = ChestSaveData.FromJson(json);

			Assert.That(reloaded.Capacity, Is.EqualTo(5), "용량 보존");
			Assert.That(reloaded.Slots, Has.Count.EqualTo(2), "비는 슬롯 제외 2 개만");

			ChestSlotSaveData slot0 = reloaded.Slots.First(s => s.SlotIndex == 0);
			Assert.That(slot0.ItemID, Is.EqualTo(401));
			Assert.That(slot0.Amount, Is.EqualTo(12));
			Assert.That(slot0.GuidString, Is.Not.Empty, "Guid 보존(string 평탄화)");

			ChestSlotSaveData slot1 = reloaded.Slots.First(s => s.SlotIndex == 1);
			Assert.That(slot1.ItemID, Is.EqualTo(402));
			Assert.That(slot1.Amount, Is.EqualTo(7));
		}

		[Test]
		public void SaveData_FromJson_EmptyOrNull_ReturnsEmptySafely()
		{
			ChestSaveData fromEmpty = ChestSaveData.FromJson(string.Empty);
			Assert.That(fromEmpty.Slots, Is.Not.Null, "빈 JSON → 빈 슬롯 리스트(NRE 금지)");
			Assert.That(fromEmpty.Capacity, Is.Zero);

			ChestSaveData fromNull = ChestSaveData.FromJson(null);
			Assert.That(fromNull.Slots, Is.Not.Null);
		}

		[Test]
		public void GridDataRoundTrip_PreservesChestRuntimeData()
		{
			// 핵심 영속 시나리오 — 상자 배치 → 인벤 채움 → GridData Save → Load → 내용물 유지.
			// BuildingInstanceData.RuntimeData(string) 가 GridData 와 함께 직렬화되는 단일 채널.
			//
			// 복원 측은 ChestSaveData 구조만 검증한다. Item 으로의 재구성(ApplyTo)은 SOHelper 를
			// 거쳐 실 ItemData asset 을 해석하므로 SOManager bootstrap 이 필요 — 그 부분은 PlayMode/
			// integration 영역. 여기서는 RuntimeData 가 그대로 살아남아 같은 슬롯/ID/수량으로 디코딩
			// 됨을 보장하면 충분(WorldStageCitySaveTest 가 BuildingID 만 보는 정책과 정합).
			ChestInventory chestA = new(capacity: 3);
			TestItemData seed = new(id: 500, maxAmount: 99);
			chestA.Add(seed, 8);
			string chestJsonA = ChestSaveData.FromChest(chestA).ToJson();

			GridData original = new();
			Vector3Int pivot = new(2, 3, 0);
			original.AddBuildingAt(pivot, new BuildingInstanceData(
				buildingID: 4005,
				state: BuildingState.Placed,
				level: 1,
				runtimeData: chestJsonA));

			List<KeyValuePair<Vector3Int, BuildingInstanceData>> serialized = original.Save();

			GridData restored = new();
			restored.Load(serialized);

			Assert.That(restored.HasBuildingAt(pivot), Is.True, "건물 복원");
			Assert.That(restored.TryGetBuildingAt(pivot, out BuildingInstanceData data), Is.True);
			Assert.That(data.RuntimeData, Is.Not.Empty, "RuntimeData 유지");

			ChestSaveData decoded = ChestSaveData.FromJson(data.RuntimeData);
			Assert.That(decoded.Capacity, Is.EqualTo(3));
			Assert.That(decoded.Slots, Has.Count.EqualTo(1));
			Assert.That(decoded.Slots[0].ItemID, Is.EqualTo(500), "상자 내용물(아이템 500)");
			Assert.That(decoded.Slots[0].Amount, Is.EqualTo(8), "수량 8 유지");
		}

		[Test]
		public void OnDataChanged_FiresOnAddAndRemove()
		{
			ChestInventory chest = new(capacity: 4);
			TestItemData seed = new(id: 700, maxAmount: 10);
			int callCount = 0;
			chest.OnDataChanged += () => callCount++;

			chest.Add(seed, 3);
			Assert.That(callCount, Is.EqualTo(1), "Add 시 발화");

			chest.Remove(0, 1);
			Assert.That(callCount, Is.EqualTo(2), "Remove 시 발화");
		}
	}
}
