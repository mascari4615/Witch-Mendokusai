using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 실제 자산과 트리 파일 대조
	public class AssetSkillTreeDataSource : ISkillTreeDataSource
	{
		private readonly Dictionary<int, CardData> cards = new();
		private readonly Dictionary<int, EquipmentData> equipments = new();
		private readonly Dictionary<int, UnitStatData> stats = new();

		public AssetSkillTreeDataSource()
		{
			foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
			{
				CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
				cards[card.ID] = card;
			}
			foreach (string guid in AssetDatabase.FindAssets("t:EquipmentData"))
			{
				EquipmentData equipment = AssetDatabase.LoadAssetAtPath<EquipmentData>(AssetDatabase.GUIDToAssetPath(guid));
				equipments[equipment.ID] = equipment;
			}
			foreach (string guid in AssetDatabase.FindAssets("t:UnitStatData"))
			{
				UnitStatData stat = AssetDatabase.LoadAssetAtPath<UnitStatData>(AssetDatabase.GUIDToAssetPath(guid));
				stats[stat.ID] = stat;
			}
		}

		public bool TryGetCard(int id, out CardData card) => cards.TryGetValue(id, out card);

		public bool TryGetEquipmentCards(int equipmentId, out IReadOnlyList<CardData> equipmentCards)
		{
			bool isFound = equipments.TryGetValue(equipmentId, out EquipmentData equipment);
			equipmentCards = isFound ? equipment.EffectCards : null;
			return isFound;
		}

		public bool TryGetEquipmentSprite(int equipmentId, out Sprite sprite)
		{
			bool isFound = equipments.TryGetValue(equipmentId, out EquipmentData equipment);
			sprite = isFound ? equipment.Sprite : null;
			return isFound;
		}

		public bool TryGetEffectTarget(EffectType type, string target, out DataSO data)
		{
			data = null;
			if (type != EffectType.UnitStat || System.Enum.TryParse(target, out UnitStatType stat) == false)
				return false;
			bool isFound = stats.TryGetValue((int)stat, out UnitStatData statData);
			data = statData;
			return isFound;
		}
	}

	public class SkillTreeDataTest
	{
		private const int FIRST_NEW_EQUIPMENT = 20000013;
		private const int LAST_NEW_EQUIPMENT = 20000034;
		private const int SWORD = 20000009;
		private const int AXE = 20000003;

		// 커밋 f06bc53ff 의 UIDeck 프리팹 5개에 놓였던 카드
		private static readonly Dictionary<int, int[]> OLD_PREFAB_SLOTS = new()
		{
			[20000000] = new int[0],
			[20000001] = new[] { 100, 101, 102, 103 },
			[20000002] = new[] { 200, 201, 202 },
			[AXE] = new[] { 0 },
			[20000004] = new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
		};

		// 같은 커밋의 카드 자산 효과. (스탯, Add 값)
		private static readonly Dictionary<int, (UnitStatType stat, int value)[]> OLD_EFFECTS = new()
		{
			[0] = new[] { (UnitStatType.ATTACK_SPEED_BONUS, 1) },
			[1] = new[] { (UnitStatType.MOVEMENT_SPEED_BONUS, 1) },
			[2] = new[] { (UnitStatType.COOLTIME_BONUS, 20) },
			[3] = new[] { (UnitStatType.HP_MAX, 5), (UnitStatType.HP_CUR, 5) },
			[4] = new[] { (UnitStatType.DAMAGE_BONUS, 5) },
			[5] = new[] { (UnitStatType.EXP_COLLIDER_SCALE, 1) },
			[6] = new[] { (UnitStatType.CRITICAL_CHANCE, 20) },
			[7] = new[] { (UnitStatType.CRITICAL_DAMAGE, 20) },
			[100] = new[] { (UnitStatType.SATELLITE_COUNT, 1) },
			[101] = new[] { (UnitStatType.SATELLITE_ROTATE_SPEED_BONUS, 1) },
			[102] = new[] { (UnitStatType.SATELLITE_DAMAGE_BONUS, 2) },
			[103] = new[] { (UnitStatType.SATELLITE_SCALE_BONUS, 1) },
			[200] = new[] { (UnitStatType.FAIRY_COUNT, 1) },
			[201] = new[] { (UnitStatType.FAIRY_DAMAGE_BONUS, 1) },
			[202] = new[] { (UnitStatType.FAIRY_ATTACK_SPEED_BONUS, 1) },
		};

		private AssetSkillTreeDataSource dataSource;
		private SkillTreeCatalog catalog;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			dataSource = new AssetSkillTreeDataSource();
			catalog = new SkillTreeCatalog(SkillTreeCatalog.LoadResources(), dataSource);
		}

		[Test]
		public void ShippedFiles_AreValid() => Assert.That(catalog.Validate(), Is.Empty);

		[Test]
		public void ShippedFiles_AreLoaded() => Assert.That(SkillTreeCatalog.LoadResources().Count, Is.EqualTo(35));

		[Test]
		public void OldPrefabs_AreMigrated_WithSameEffects()
		{
			foreach (KeyValuePair<int, int[]> slots in OLD_PREFAB_SLOTS)
			{
				SkillTree tree = catalog.Find(slots.Key);
				Assert.That(tree, Is.Not.Null, $"{slots.Key} 트리 없음");

				foreach (int cardId in slots.Value.Where(cardId => slots.Key != AXE))
				{
					SkillTreeNode node = tree.Nodes.FirstOrDefault(item => item.Card.ID == cardId);
					Assert.That(node, Is.Not.Null, $"{slots.Key} 에 카드 {cardId} 없음");
					AssertSameEffects(node.Card);
				}
			}
		}

		// 도끼 프리팹의 카드 0 은 도끼 후보가 아니었음. 트리는 실제 후보를 따름
		[Test]
		public void OldAxePrefabCard_WasNotInPool()
		{
			Assert.That(dataSource.TryGetEquipmentCards(AXE, out IReadOnlyList<CardData> cards));
			Assert.That(cards.Any(card => card.ID == 0), Is.False);
			Assert.That(catalog.Find(AXE).Nodes.Select(node => node.Card.ID), Is.EquivalentTo(cards.Select(card => card.ID)));
		}

		[Test]
		public void OldPrefabTrees_AddNoNewGate()
		{
			foreach (int equipmentId in OLD_PREFAB_SLOTS.Keys)
			{
				foreach (SkillTreeNode node in catalog.Find(equipmentId).Nodes)
				{
					Assert.That(node.IsInline, Is.False, node.Id);
					Assert.That(node.Requirements.Count, Is.EqualTo(node.Card.Requirements.Count), node.Id);
				}
			}
		}

		[Test]
		public void EveryTree_ContainsWholeCardPool()
		{
			foreach (SkillTree tree in catalog.Trees)
			{
				Assert.That(dataSource.TryGetEquipmentCards(tree.EquipmentID, out IReadOnlyList<CardData> cards));
				Assert.That(tree.Nodes.Where(node => node.IsInline == false).Select(node => node.Card.ID), Is.EquivalentTo(cards.Select(card => card.ID)), tree.EquipmentID.ToString());
			}
		}

		[Test]
		public void SwordTree_HasBranchAndInlineNode()
		{
			SkillTree tree = catalog.Find(SWORD);
			Assert.That(tree, Is.Not.Null);
			Assert.That(tree.Nodes.Any(node => node.IsInline));
			Assert.That(SkillTreeLayout.RowCount(SkillTreeLayout.Compute(tree, 4)), Is.GreaterThan(1));
		}

		[Test]
		public void NewEquipmentTrees_HaveThreeToFiveNodes()
		{
			for (int id = FIRST_NEW_EQUIPMENT; id <= LAST_NEW_EQUIPMENT; id++)
			{
				SkillTree tree = catalog.Find(id);
				Assert.That(tree, Is.Not.Null, $"{id} 트리 없음");
				Assert.That(tree.Nodes.Count, Is.InRange(3, 5), id.ToString());
			}
		}

		[Test]
		public void EveryTree_HasOpenRoot_AndEveryNodeIsReachableByKeys()
		{
			foreach (SkillTree tree in catalog.Trees)
			{
				Assert.That(tree.Nodes.Any(node => node.IsUnlocked(new List<CardData>())), tree.EquipmentID.ToString());

				Dictionary<string, SkillTreeCell> cells = SkillTreeLayout.Compute(tree, 4);
				HashSet<string> visited = new();
				Stack<string> pending = new();
				pending.Push(tree.Nodes[0].Id);
				while (pending.Count > 0)
				{
					string current = pending.Pop();
					if (visited.Add(current) == false)
						continue;
					foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
					{
						string next = SkillTreeLayout.Next(cells, current, direction);
						if (next != null)
							pending.Push(next);
					}
				}
				Assert.That(visited.Count, Is.EqualTo(tree.Nodes.Count), tree.EquipmentID.ToString());
			}
		}

		private static void AssertSameEffects(CardData card)
		{
			(UnitStatType stat, int value)[] expected = OLD_EFFECTS[card.ID];
			Assert.That(card.Effects.Count, Is.EqualTo(expected.Length), card.ID.ToString());
			for (int i = 0; i < expected.Length; i++)
			{
				Assert.That(card.Effects[i].Type, Is.EqualTo(EffectType.UnitStat), card.ID.ToString());
				Assert.That(card.Effects[i].ArithmeticOperator, Is.EqualTo(ArithmeticOperator.Add), card.ID.ToString());
				Assert.That((card.Effects[i].Data as UnitStatData).Type, Is.EqualTo(expected[i].stat), card.ID.ToString());
				Assert.That(card.Effects[i].Value, Is.EqualTo(expected[i].value), card.ID.ToString());
			}
		}
	}
}
