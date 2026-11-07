using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class DevShopNpcTest
	{
		private const string DEV_SHOP_PATH = "Assets/_WitchMendokusai/Domain/Trade/Shop/Object/NPC_11_개발용상점.asset";
		private const string MERCHANT_PATH = "Assets/_WitchMendokusai/Domain/Trade/Shop/Object/NPC_0_떠돌이상인.asset";
		private const string STAGE_MANAGER_PATH = "Assets/_WitchMendokusai/Core/Assets/Singletons/StageManager.prefab";

		private static NPC DevShop => AssetDatabase.LoadAssetAtPath<NPC>(DEV_SHOP_PATH);

		private static List<EquipmentData> AllEquipment() => AssetDatabase.FindAssets("t:" + nameof(EquipmentData))
			.Select(guid => AssetDatabase.LoadAssetAtPath<EquipmentData>(AssetDatabase.GUIDToAssetPath(guid)))
			.ToList();

		[Test]
		public void Dev_shop_sells_every_equipment()
		{
			List<ItemData> stock = DevShop.ItemDataBuffers[0].InitItems;
			List<EquipmentData> missing = AllEquipment().Where(equipment => stock.Contains(equipment) == false).ToList();

			Assert.IsEmpty(missing.Select(equipment => equipment.name));
		}

		[Test]
		public void Dev_shop_is_free_and_keeps_item_price()
		{
			EquipmentData priced = AllEquipment().First(equipment => equipment.PurchasePrice > 0);

			Assert.AreEqual("개발용 상점", DevShop.Name);
			Assert.AreEqual(0, DevShop.GetShopPrice(priced));
			Assert.Greater(priced.PurchasePrice, 0);
		}

		[Test]
		public void Other_shop_charges_item_price()
		{
			NPC merchant = AssetDatabase.LoadAssetAtPath<NPC>(MERCHANT_PATH);
			EquipmentData priced = AllEquipment().First(equipment => equipment.PurchasePrice > 0);

			Assert.AreEqual(priced.PurchasePrice, merchant.GetShopPrice(priced));
		}

		[Test]
		public void Dev_shop_stands_in_start_stage()
		{
			StageManager stageManager = AssetDatabase.LoadAssetAtPath<GameObject>(STAGE_MANAGER_PATH).GetComponent<StageManager>();
			NPCObject[] npcs = stageManager.StartStage.Prefab.GetComponentsInChildren<NPCObject>(true);

			Assert.IsTrue(npcs.Any(npc => npc.Data == DevShop));
		}
	}
}
