using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 자료의 모든 이름에 그림이 있고, 자산의 칸이 모두 이어져 있는지
	public sealed class HomesteadArtTest
	{
		private static readonly string[] MARKS = { "nyang", "townhall", "house", "board", "relic_dirty", "fish_shadow", "bobber", "sprout_0", "sprout_1", "sprout_2", "sprout_3" };

		[Test]
		public void Every_id_has_an_icon()
		{
			HomesteadArt art = Resources.Load<HomesteadArt>(nameof(HomesteadArt));
			HomesteadCatalog catalog = HomesteadManager.LoadCatalog();
			List<string> missing = new();
			void Need(string key)
			{
				if (art.HasIcon(key) == false)
					missing.Add(key);
			}
			foreach (string mark in MARKS)
				Need(mark);
			foreach (GoodsDef goods in catalog.Goods)
			{
				if (goods.ItemId < 0)
					Need(goods.Id);
			}
			catalog.Buildings.ForEach(building => Need(building.Id));
			catalog.Residents.ForEach(resident => Need(resident.Id));
			catalog.Animals.ForEach(animal => Need(animal.Id));
			catalog.Furniture.ForEach(furniture => Need(furniture.Id));
			catalog.Costumes.ForEach(costume => Need(costume.Id));
			catalog.Placeables.FindAll(placeable => placeable.Kind != PlaceableKind.Building).ForEach(placeable => Need(placeable.Id));
			Assert.IsEmpty(missing, string.Join(", ", missing));
		}

		[Test]
		public void Every_crop_and_rock_has_a_model()
		{
			HomesteadCatalog catalog = HomesteadManager.LoadCatalog();
			HashSet<string> keys = new();
			foreach (VillageModels.Model model in VillageModels.Instance.Models)
				keys.Add(model.Key);
			List<string> missing = new();
			void Need(string key)
			{
				if (keys.Contains(key) == false)
					missing.Add(key);
			}
			foreach (string key in new[] { "CropSprout", "FieldEdge", "Scarecrow", "MineShell", "MineLift", "MineHole", "MineGlint", "Museum_hall" })
				Need(key);
			foreach (WingDef wing in catalog.Wings)
			{
				Need("Museum_" + wing.Id);
				Need("Stand_" + wing.Id);
			}
			foreach (string stage in new[] { "_a", "_b", "_c" })
				catalog.Crops.ForEach(crop => Need("Crop_" + crop.Id + stage));
			catalog.MineNodes.ForEach(node => Need("Mine_" + node.Id));
			Assert.IsEmpty(missing, string.Join(", ", missing));
		}

		// 걸음은 선 모습 한 장과 걸음 한 장 이상
		[Test]
		public void Every_animal_has_walking_frames()
		{
			HomesteadArt art = Resources.Load<HomesteadArt>(nameof(HomesteadArt));
			foreach (AnimalDef animal in HomesteadManager.LoadCatalog().Animals)
				Assert.GreaterOrEqual(art.FramesOf(animal.Id, 0).Length, 2, animal.Id);
		}

		[Test]
		public void Every_slot_of_the_art_asset_is_linked()
		{
			SerializedObject asset = new(Resources.Load<HomesteadArt>(nameof(HomesteadArt)));
			SerializedProperty property = asset.GetIterator();
			List<string> empty = new();
			while (property.NextVisible(true))
			{
				if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null)
					empty.Add(property.name);
			}
			Assert.IsEmpty(empty, string.Join(", ", empty));
		}

		[Test]
		public void Village_stage_is_reachable_by_id()
		{
			WorldStage stage = AssetDatabase.LoadAssetAtPath<WorldStage>("Assets/_WitchMendokusai/Domain/World/Stage/0006_Village/WS_6_Village.asset");
			Assert.AreEqual(6, stage.ID);
			Assert.IsNotNull(stage.Prefab.GetComponent<VillageStage>());
			Assert.AreEqual(1, stage.Prefab.Portals.Length);
		}
	}
}
