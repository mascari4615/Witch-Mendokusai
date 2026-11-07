using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 상자 진화 카드와 장비 카드 풀 대조
	public class SurvivalEvolutionDataTest
	{
		private const string RULES_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Survival/SurvivalRunRules.asset";
		private const int BROOM = 20000017;
		private const int THIMBLE = 20000032;
		private const int CHORE = 20000033;
		private const int PULL = 20000034;
		private static readonly int[] BASIC_EQUIPMENTS = { BROOM, THIMBLE, CHORE, PULL };

		private SurvivalRunRules rules;
		private SkillTreeCatalog catalog;
		private Dictionary<int, EquipmentData> equipments;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			rules = AssetDatabase.LoadAssetAtPath<SurvivalRunRules>(RULES_PATH);
			catalog = new SkillTreeCatalog(SkillTreeCatalog.LoadResources(), new AssetSkillTreeDataSource());
			equipments = new Dictionary<int, EquipmentData>();
			foreach (string guid in AssetDatabase.FindAssets("t:EquipmentData"))
			{
				EquipmentData equipment = AssetDatabase.LoadAssetAtPath<EquipmentData>(AssetDatabase.GUIDToAssetPath(guid));
				equipments[equipment.ID] = equipment;
			}
		}

		[Test]
		public void EvolutionCards_AreUniqueAndChestOnly()
		{
			Assert.That(rules.EvolutionCards, Has.None.Null);
			Assert.That(rules.EvolutionCards.Select(card => card.ID), Is.Unique);
			foreach (CardData evolution in rules.EvolutionCards)
			{
				Assert.That(evolution.MaxStack, Is.EqualTo(1), evolution.ID.ToString());
				Assert.That(evolution.Requirements, Is.Not.Empty, evolution.ID.ToString());
				Assert.That(TreeEquipments().Any(equipment => catalog.CardsFor(equipment).Any(card => card.ID == evolution.ID)), Is.False, $"{evolution.ID} 가 레벨업 카드 풀에 있음");
			}
		}

		// 요구 카드가 한 장비 풀 안에 있어야 진화 진행 표시와 지급이 열림
		[Test]
		public void EveryEvolution_RequirementsFitOneEquipment()
		{
			foreach (CardData evolution in rules.EvolutionCards)
				Assert.That(OwnersOf(evolution), Is.Not.Empty, evolution.ID.ToString());
		}

		[Test]
		public void BasicEquipments_EachHaveOneEvolution()
		{
			foreach (int equipmentId in BASIC_EQUIPMENTS)
			{
				int count = rules.EvolutionCards.Count(evolution => OwnersOf(evolution).Contains(equipmentId));
				Assert.That(count, Is.EqualTo(1), equipmentId.ToString());
			}
		}

		[Test]
		public void Requirements_StayWithinCardMaxStack()
		{
			foreach (CardData evolution in rules.EvolutionCards)
				foreach (CardData.Requirement requirement in evolution.Requirements)
					Assert.That(requirement.Stack, Is.InRange(1, requirement.Card.MaxStack), $"{evolution.ID} -> {requirement.Card.ID}");
		}

		private List<int> OwnersOf(CardData evolution)
		{
			List<int> owners = new();
			foreach (EquipmentData equipment in TreeEquipments())
			{
				HashSet<int> pool = new(catalog.CardsFor(equipment).Where(card => card != null).Select(card => card.ID));
				if (evolution.Requirements.All(requirement => pool.Contains(requirement.Card.ID)))
					owners.Add(equipment.ID);
			}
			return owners;
		}

		private IEnumerable<EquipmentData> TreeEquipments() => catalog.Trees.Select(tree => equipments[tree.EquipmentID]);
	}
}
