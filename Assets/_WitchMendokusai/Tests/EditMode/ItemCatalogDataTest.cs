using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	public class ItemCatalogDataTest
	{
		private static readonly int[] TOOL_EQUIPMENT_IDS = { 20000000, 20000004, 20000006, 20000007, 20000008, 20000034 };
		private static readonly string[] TYPO_FRAGMENTS = { "괝", "잎파리" };

		private static List<T> AllAssets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
			.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
			.Where(asset => asset != null)
			.ToList();

		[Test]
		public void Purchasable_items_sell_below_purchase_price()
		{
			List<string> resaleProfit = AllAssets<ItemData>()
				.Where(item => item.PurchasePrice > 0 && item.SalePrice >= item.PurchasePrice)
				.Select(item => $"{item.ID} {item.Name} {item.PurchasePrice}/{item.SalePrice}")
				.ToList();

			Assert.IsEmpty(resaleProfit);
		}

		[Test]
		public void Charging_shops_only_stock_priced_items()
		{
			foreach (NPC npc in AllAssets<NPC>().Where(npc => npc.IsShopFree == false))
			{
				List<string> freeStock = npc.ItemDataBuffers
					.SelectMany(buffer => buffer.InitItems)
					.Where(item => item != null && item.PurchasePrice <= 0)
					.Select(item => item.Name)
					.ToList();

				Assert.IsEmpty(freeStock, npc.Name);
			}
		}

		[Test]
		public void Item_names_have_no_known_typos()
		{
			List<string> typos = AllAssets<ItemData>()
				.Where(item => TYPO_FRAGMENTS.Any(fragment => item.Name.Contains(fragment) || (item.Description != null && item.Description.Contains(fragment))))
				.Select(item => item.Name)
				.ToList();

			Assert.IsEmpty(typos);
		}

		[Test]
		public void Support_equipment_is_tool_and_stays_offerable()
		{
			Dictionary<int, EquipmentData> equipments = AllAssets<EquipmentData>().ToDictionary(equipment => equipment.ID);

			foreach (int id in TOOL_EQUIPMENT_IDS)
			{
				Assert.AreEqual(EquipmentType.Tool, equipments[id].EquipmentType, equipments[id].Name);
				Assert.IsTrue(equipments[id].IsRunOfferType, equipments[id].Name);
			}
		}

		[Test]
		public void Default_type_equipment_has_attack_object()
		{
			List<string> notWeapons = AllAssets<EquipmentData>()
				.Where(equipment => equipment.Type == ItemType.Equipment && equipment.EquipmentType == EquipmentType.Default && equipment.Object == null)
				.Where(equipment => equipment.EffectCards != null && equipment.EffectCards.Any(card => card != null))
				.Select(equipment => equipment.Name)
				.ToList();

			Assert.IsEmpty(notWeapons);
		}

		[Test]
		public void Gathering_tools_are_not_offered_in_runs()
		{
			foreach (EquipmentData equipment in AllAssets<EquipmentData>().Where(equipment => equipment.EquipmentType is EquipmentType.Pickaxe or EquipmentType.Axe or EquipmentType.FishingRod or EquipmentType.Hoe))
				Assert.IsFalse(equipment.IsRunOfferType, equipment.Name);
		}
	}
}
