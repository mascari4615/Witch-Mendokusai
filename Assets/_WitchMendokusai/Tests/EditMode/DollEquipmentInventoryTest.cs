using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>인형 장비 칸. 인형이 없을 때도 칸 수만큼 읽을 수 있어야 함 (세이브를 다시 읽는 동안 목록이 잠깐 빔)</summary>
	public sealed class DollEquipmentInventoryTest
	{
		private DollEquipmentInventory inventory;

		[SetUp]
		public void SetUp() => inventory = ScriptableObject.CreateInstance<DollEquipmentInventory>();

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(inventory);

		[Test]
		public void Unbound_slots_read_as_empty()
		{
			inventory.BindDoll(null);

			Assert.That(inventory.Data.Count, Is.EqualTo(inventory.Capacity));
			for (int i = 0; i < inventory.Capacity; i++)
			{
				Assert.That(inventory.GetItem(i), Is.Null);
			}
		}

		[Test]
		public void Grid_survives_unbinding_the_doll()
		{
			Doll doll = ScriptableObject.CreateInstance<Doll>();
			inventory.BindDoll(doll);
			ItemGrid grid = new();
			grid.Bind(inventory);
			Assert.That(grid.childCount, Is.EqualTo(Doll.EQUIPMENT_SLOT_COUNT));

			Assert.DoesNotThrow(() => inventory.BindDoll(null));

			grid.Unbind();
			Object.DestroyImmediate(doll);
		}
	}
}
