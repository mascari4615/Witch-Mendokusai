using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class FakeSkillTreeDataSource : ISkillTreeDataSource
	{
		public readonly Dictionary<int, CardData> Cards = new();
		public readonly Dictionary<int, List<CardData>> Equipments = new();
		public readonly Dictionary<string, DataSO> Targets = new();

		public bool TryGetCard(int id, out CardData card) => Cards.TryGetValue(id, out card);

		public bool TryGetEquipmentCards(int equipmentId, out IReadOnlyList<CardData> cards)
		{
			bool isFound = Equipments.TryGetValue(equipmentId, out List<CardData> list);
			cards = list;
			return isFound;
		}

		public bool TryGetEquipmentSprite(int equipmentId, out Sprite sprite)
		{
			sprite = null;
			return Equipments.ContainsKey(equipmentId);
		}

		public bool TryGetEffectTarget(EffectType type, string target, out DataSO data) => Targets.TryGetValue($"{type}/{target}", out data);
	}

	public class SkillTreeFormatTest
	{
		private const int EQUIPMENT_ID = 77;
		private const int ROOT_CARD_ID = 1000;
		private const int SECOND_CARD_ID = 1001;

		private readonly List<UnityEngine.Object> created = new();
		private FakeSkillTreeDataSource dataSource;
		private CardData rootCard;
		private CardData secondCard;

		[SetUp]
		public void SetUp()
		{
			dataSource = new FakeSkillTreeDataSource();
			rootCard = NewCard(ROOT_CARD_ID, 5);
			secondCard = NewCard(SECOND_CARD_ID, 5);
			dataSource.Equipments[EQUIPMENT_ID] = new List<CardData> { rootCard, secondCard };
			UnitStatData stat = ScriptableObject.CreateInstance<UnitStatData>();
			created.Add(stat);
			dataSource.Targets["UnitStat/SWORD_COUNT"] = stat;
		}

		[TearDown]
		public void TearDown()
		{
			foreach (UnityEngine.Object target in created)
				UnityEngine.Object.DestroyImmediate(target);
			created.Clear();
		}

		private CardData NewCard(int id, int maxStack)
		{
			CardData card = CardData.CreateRuntime(id, $"card{id}", "text", null, new List<EffectInfo>(), maxStack, CardRarity.Common, SurvivalUIKind.Count);
			created.Add(card);
			dataSource.Cards[id] = card;
			return card;
		}

		private List<string> Errors(string json) => new SkillTreeCatalog(new[] { new SkillTreeSource("test", json) }, dataSource).Validate();

		private static string Tree(string nodes, int equipment = EQUIPMENT_ID) => $"{{\"equipment\":{equipment},\"nodes\":[{nodes}]}}";

		private const string VALID_NODES = "{\"id\":\"a\",\"card\":1000},{\"id\":\"b\",\"card\":1001,\"requires\":[{\"node\":\"a\",\"stack\":2}]}";
		private const string INLINE_NODE = "{\"id\":\"c\",\"cardId\":2000,\"name\":\"n\",\"description\":\"d\",\"maxStack\":3,\"kind\":\"Damage\",\"rarity\":\"Rare\",\"effects\":[{\"type\":\"UnitStat\",\"target\":\"SWORD_COUNT\",\"op\":\"Add\",\"value\":4}],\"requires\":[{\"node\":\"a\"}]}";

		private static void AssertHasError(List<string> errors, string part) =>
			Assert.That(errors.Any(error => error.Contains(part)), $"'{part}' 오류 없음: {string.Join(" | ", errors)}");

		[Test]
		public void IsRoot_OnlyStartNodeWithDependents()
		{
			CardData loneCard = NewCard(SECOND_CARD_ID + 1, 5);
			SkillTree tree = new(EQUIPMENT_ID);
			SkillTreeNode root = new("root", rootCard, false);
			SkillTreeNode child = new("child", secondCard, false);
			child.Requirements.Add(new SkillTreeRequirement(root, rootCard, 1));
			tree.Add(root);
			tree.Add(child);
			tree.Add(new SkillTreeNode("lone", loneCard, false));

			Assert.That(tree.IsRoot(rootCard), Is.True);
			Assert.That(tree.IsRoot(secondCard), Is.False);
			Assert.That(tree.IsRoot(loneCard), Is.False);
		}

		[Test]
		public void ValidTree_HasNoErrors() => Assert.That(Errors(Tree(VALID_NODES + "," + INLINE_NODE)), Is.Empty);

		[Test]
		public void StartingBasicBranches_OfferThreeDistinctUpgradesAndAgreeWithTreeState()
		{
			CardData thirdCard = NewCard(SECOND_CARD_ID + 1, 5);
			SkillTreeNode root = new("root", rootCard, false);
			SkillTreeNode second = new("second", secondCard, false);
			SkillTreeNode third = new("third", thirdCard, true);
			second.Requirements.Add(new SkillTreeRequirement(root, rootCard, 1));
			third.Requirements.Add(new SkillTreeRequirement(second, secondCard, 1));
			SkillTree tree = new(EQUIPMENT_ID);
			foreach (SkillTreeNode node in new[] { root, second, third }) tree.Add(node);
			List<CardData> selected = new();

			Assert.That(tree.Nodes.Count(node => tree.IsUnlocked(node.Card, selected)), Is.EqualTo(3));
			Assert.That(tree.Nodes.Select(node => node.Card.ID).Distinct().Count(), Is.EqualTo(3));
			foreach (SkillTreeNode node in tree.Nodes)
			{
				Assert.That(node.State(selected), Is.EqualTo(SkillTreeNodeState.Open));
				Assert.That(node.ChoiceRequirements, Is.Empty);
			}
			Assert.That(third.Requirements, Has.Count.EqualTo(1), "관계 원자료는 보존");
		}

		[Test]
		public void AdvancedRareMultiStackAndExternalRequirements_StayLocked()
		{
			SkillTreeNode root = new("root", rootCard, false);
			CardData rareCard = CardData.CreateRuntime(3000, "rare", "", null, new List<EffectInfo>(), 3, CardRarity.Rare, SurvivalUIKind.Damage);
			CardData advancedCard = CardData.CreateRuntime(3001, "advanced", "", null, new List<EffectInfo>(), 1, CardRarity.Common, SurvivalUIKind.Advanced);
			created.Add(rareCard);
			created.Add(advancedCard);
			SkillTreeNode rare = new("rare", rareCard, true);
			SkillTreeNode advanced = new("advanced", advancedCard, false);
			SkillTreeNode multi = new("multi", secondCard, false);
			SkillTreeNode external = new("external", NewCard(3002, 5), false);
			rare.Requirements.Add(new SkillTreeRequirement(root, rootCard, 1));
			advanced.Requirements.Add(new SkillTreeRequirement(root, rootCard, 1));
			multi.Requirements.Add(new SkillTreeRequirement(root, rootCard, 2));
			external.Requirements.Add(new SkillTreeRequirement(null, rootCard, 1));
			foreach (SkillTreeNode node in new[] { rare, advanced, multi, external })
			{
				Assert.That(node.IsStartingUpgrade, Is.False);
				Assert.That(node.State(new List<CardData>()), Is.EqualTo(SkillTreeNodeState.Locked));
				Assert.That(node.ChoiceRequirements, Has.Count.EqualTo(1));
			}
			Assert.That(multi.IsUnlocked(new List<CardData> { rootCard, rootCard }), Is.True);
		}

		[Test]
		public void CardAssetRequirement_IsNotBypassedByStartingChoice()
		{
			secondCard.Requirements.Add(new CardData.Requirement { Card = rootCard, Stack = 1 });
			SkillTreeNode node = new("asset-gated", secondCard, false);
			Assert.That(node.IsStartingUpgrade, Is.False);
			Assert.That(node.IsUnlocked(new List<CardData>()), Is.False);
			Assert.That(node.IsUnlocked(new List<CardData> { rootCard }), Is.True);
		}

		[Test]
		public void BrokenJson_IsReported() => AssertHasError(Errors("{ not json"), "JSON");

		[Test]
		public void UnknownField_IsReported() => AssertHasError(Errors("{\"equipment\":77,\"nodez\":[]}"), "JSON");

		[Test]
		public void UnknownEquipment_IsReported() => AssertHasError(Errors(Tree(VALID_NODES, 5)), "없는 장비");

		[Test]
		public void UnknownCard_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":999}")), "없는 카드");

		[Test]
		public void DuplicateNodeId_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000},{\"id\":\"a\",\"card\":1001}")), "중복 노드");

		[Test]
		public void UnknownRequiredNode_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000,\"requires\":[{\"node\":\"z\"}]},{\"id\":\"b\",\"card\":1001}")), "없는 노드");

		[Test]
		public void Cycle_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000,\"requires\":[{\"node\":\"b\"}]},{\"id\":\"b\",\"card\":1001,\"requires\":[{\"node\":\"a\"}]}")), "순환");

		[Test]
		public void SelfCycle_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000,\"requires\":[{\"node\":\"a\"}]},{\"id\":\"b\",\"card\":1001}")), "순환");

		[Test]
		public void UnknownEffectType_IsReported() => AssertHasError(Errors(Tree(VALID_NODES + "," + INLINE_NODE.Replace("\"UnitStat\"", "\"Teleport\""))), "없는 효과 종류");

		[Test]
		public void UnknownEffectTarget_IsReported() => AssertHasError(Errors(Tree(VALID_NODES + "," + INLINE_NODE.Replace("SWORD_COUNT", "NO_STAT"))), "없는 효과 대상");

		[Test]
		public void UnknownOperator_IsReported() => AssertHasError(Errors(Tree(VALID_NODES + "," + INLINE_NODE.Replace("\"Add\"", "\"Plus\""))), "없는 연산");

		[Test]
		public void ReferenceAndInlineTogether_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000,\"cardId\":2000},{\"id\":\"b\",\"card\":1001}")), "하나만");

		[Test]
		public void NoCardAtAll_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\"},{\"id\":\"b\",\"card\":1000},{\"id\":\"c\",\"card\":1001}")), "하나만");

		[Test]
		public void InlineCardIdClash_IsReported() => AssertHasError(Errors(Tree(VALID_NODES + "," + INLINE_NODE.Replace("2000", "1000"))), "겹침");

		[Test]
		public void ZeroStack_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000},{\"id\":\"b\",\"card\":1001,\"requires\":[{\"node\":\"a\",\"stack\":0}]}")), "stack");

		[Test]
		public void StackOverMax_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000},{\"id\":\"b\",\"card\":1001,\"requires\":[{\"node\":\"a\",\"stack\":6}]}")), "넘음");

		[Test]
		public void MissingEquipmentCard_IsReported() => AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000}")), "트리에 없음");

		[Test]
		public void CardRequirementMissingInFile_IsReported()
		{
			secondCard.Requirements.Add(new CardData.Requirement { Card = rootCard, Stack = 3 });
			AssertHasError(Errors(Tree("{\"id\":\"a\",\"card\":1000},{\"id\":\"b\",\"card\":1001}")), "requires 에 없음");
			Assert.That(Errors(Tree("{\"id\":\"a\",\"card\":1000},{\"id\":\"b\",\"card\":1001,\"requires\":[{\"node\":\"a\",\"stack\":3}]}")), Is.Empty);
		}

		[Test]
		public void BrokenFile_FailsFastOnUse()
		{
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree("{\"id\":\"a\",\"card\":999}")) }, dataSource);
			Assert.Throws<System.IO.InvalidDataException>(() => catalog.Find(EQUIPMENT_ID));
		}

		[Test]
		public void InlineNode_CompilesToCardWithSameEffectData()
		{
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree(VALID_NODES + "," + INLINE_NODE)) }, dataSource);
			SkillTreeNode node = catalog.Find(EQUIPMENT_ID).Nodes.First(item => item.Id == "c");
			created.Add(node.Card);

			Assert.That(node.IsInline);
			Assert.That(node.Card.ID, Is.EqualTo(2000));
			Assert.That(node.Card.MaxStack, Is.EqualTo(3));
			Assert.That(node.Card.Rarity, Is.EqualTo(CardRarity.Rare));
			Assert.That(node.Card.UIKind, Is.EqualTo(SurvivalUIKind.Damage));
			Assert.That(node.Card.Effects.Count, Is.EqualTo(1));
			Assert.That(node.Card.Effects[0].Type, Is.EqualTo(EffectType.UnitStat));
			Assert.That(node.Card.Effects[0].Data, Is.SameAs(dataSource.Targets["UnitStat/SWORD_COUNT"]));
			Assert.That(node.Card.Effects[0].ArithmeticOperator, Is.EqualTo(ArithmeticOperator.Add));
			Assert.That(node.Card.Effects[0].Value, Is.EqualTo(4));
		}

		[Test]
		public void NodeState_FollowsSelectedCards()
		{
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree(VALID_NODES)) }, dataSource);
			SkillTree tree = catalog.Find(EQUIPMENT_ID);
			SkillTreeNode root = tree.NodeOf(rootCard);
			SkillTreeNode child = tree.NodeOf(secondCard);
			List<CardData> selected = new();

			Assert.That(root.State(selected), Is.EqualTo(SkillTreeNodeState.Open));
			Assert.That(child.State(selected), Is.EqualTo(SkillTreeNodeState.Locked));
			Assert.That(tree.IsUnlocked(secondCard, selected), Is.False);

			selected.Add(rootCard);
			Assert.That(root.State(selected), Is.EqualTo(SkillTreeNodeState.Owned));
			Assert.That(child.State(selected), Is.EqualTo(SkillTreeNodeState.Locked));

			selected.Add(rootCard);
			Assert.That(child.State(selected), Is.EqualTo(SkillTreeNodeState.Open));
			Assert.That(tree.IsUnlocked(secondCard, selected), Is.True);

			selected.AddRange(Enumerable.Repeat(rootCard, 3));
			Assert.That(root.State(selected), Is.EqualTo(SkillTreeNodeState.Maxed));
		}

		[Test]
		public void CardOutsideTree_IsNotGated()
		{
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree(VALID_NODES)) }, dataSource);
			CardData other = NewCard(3000, 1);
			Assert.That(catalog.Find(EQUIPMENT_ID).IsUnlocked(other, new List<CardData>()), Is.True);
		}

		[Test]
		public void ExternalRequirement_CountsOtherEquipmentCard()
		{
			CardData external = NewCard(3000, 5);
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree("{\"id\":\"a\",\"card\":1000,\"requires\":[{\"card\":3000,\"stack\":1}]},{\"id\":\"b\",\"card\":1001}")) }, dataSource);
			SkillTreeNode node = catalog.Find(EQUIPMENT_ID).NodeOf(rootCard);

			Assert.That(node.Requirements[0].IsExternal);
			Assert.That(node.IsUnlocked(new List<CardData>()), Is.False);
			Assert.That(node.IsUnlocked(new List<CardData> { external }), Is.True);
		}

		[Test]
		public void TwoFilesForOneEquipment_IsReported()
		{
			SkillTreeSource source = new("test", Tree(VALID_NODES));
			AssertHasError(new SkillTreeCatalog(new[] { source, source }, dataSource).Validate(), "둘 이상");
		}

		[Test]
		public void Layout_ChildIsBelowParent_AndCellsAreUnique()
		{
			SkillTreeCatalog catalog = new(new[] { new SkillTreeSource("test", Tree(VALID_NODES + "," + INLINE_NODE)) }, dataSource);
			SkillTree tree = catalog.Find(EQUIPMENT_ID);
			created.AddRange(tree.InlineCards);
			Dictionary<string, SkillTreeCell> cells = SkillTreeLayout.Compute(tree, 4);

			Assert.That(cells["b"].Row, Is.GreaterThan(cells["a"].Row));
			Assert.That(cells["c"].Row, Is.GreaterThan(cells["a"].Row));
			Assert.That(cells.Values.Select(cell => (cell.Column, cell.Row)).Distinct().Count(), Is.EqualTo(cells.Count));
		}

		[Test]
		public void Layout_WrapsWideLayer()
		{
			List<CardData> cards = new();
			List<string> nodes = new();
			for (int i = 0; i < 9; i++)
			{
				cards.Add(NewCard(4000 + i, 1));
				nodes.Add($"{{\"id\":\"n{i}\",\"card\":{4000 + i}}}");
			}
			dataSource.Equipments[EQUIPMENT_ID] = cards;
			SkillTree tree = new SkillTreeCatalog(new[] { new SkillTreeSource("test", Tree(string.Join(",", nodes))) }, dataSource).Find(EQUIPMENT_ID);
			Dictionary<string, SkillTreeCell> cells = SkillTreeLayout.Compute(tree, 4);

			Assert.That(SkillTreeLayout.RowCount(cells), Is.EqualTo(3));
			Assert.That(cells.Values.GroupBy(cell => cell.Row).Max(group => group.Count()), Is.EqualTo(4));
		}

		[Test]
		public void Next_MovesByDirection_AndStopsAtEdge()
		{
			Dictionary<string, SkillTreeCell> cells = new()
			{
				["top"] = new SkillTreeCell(0f, 0),
				["left"] = new SkillTreeCell(-0.5f, 1),
				["right"] = new SkillTreeCell(0.5f, 1),
			};

			Assert.That(SkillTreeLayout.Next(cells, "top", Vector2Int.up), Is.Null);
			Assert.That(SkillTreeLayout.Next(cells, "left", Vector2Int.right), Is.EqualTo("right"));
			Assert.That(SkillTreeLayout.Next(cells, "right", Vector2Int.left), Is.EqualTo("left"));
			Assert.That(SkillTreeLayout.Next(cells, "left", Vector2Int.up), Is.EqualTo("top"));
			Assert.That(SkillTreeLayout.Next(cells, "top", Vector2Int.down), Is.Not.Null);
			Assert.That(SkillTreeLayout.Next(cells, "left", Vector2Int.down), Is.Null);
		}
	}
}
