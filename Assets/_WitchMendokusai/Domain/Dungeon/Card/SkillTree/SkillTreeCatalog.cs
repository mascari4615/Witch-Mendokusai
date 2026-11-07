using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// Resources/SkillTrees 의 파일을 읽어 장비별 트리로 변환
	public class SkillTreeCatalog
	{
		public const string RESOURCE_FOLDER = "SkillTrees";

		private readonly IReadOnlyList<SkillTreeSource> sources;
		private readonly ISkillTreeDataSource dataSource;
		private Dictionary<int, SkillTree> trees;

		[Inject]
		public SkillTreeCatalog() : this(LoadResources(), new RuntimeSkillTreeDataSource())
		{
		}

		public SkillTreeCatalog(IReadOnlyList<SkillTreeSource> sources, ISkillTreeDataSource dataSource)
		{
			this.sources = sources;
			this.dataSource = dataSource;
		}

		public static IReadOnlyList<SkillTreeSource> LoadResources() =>
			Resources.LoadAll<TextAsset>(RESOURCE_FOLDER).Select(asset => new SkillTreeSource(asset.name, asset.text)).ToList();

		public IReadOnlyList<SkillTree> Trees
		{
			get
			{
				EnsureBuilt();
				return trees.Values.ToList();
			}
		}

		public SkillTree Find(int equipmentId)
		{
			EnsureBuilt();
			return trees.TryGetValue(equipmentId, out SkillTree tree) ? tree : null;
		}

		// 장비 카드와 트리 파일이 정의한 새 카드
		public IReadOnlyList<CardData> CardsFor(EquipmentData equipment)
		{
			SkillTree tree = Find(equipment.ID);
			if (tree == null)
				return equipment.EffectCards;
			return equipment.EffectCards.Concat(tree.InlineCards).ToList();
		}

		public List<string> Validate()
		{
			List<string> errors = new();
			ParseAll(errors);
			return errors;
		}

		private void EnsureBuilt()
		{
			if (trees != null)
				return;

			List<string> errors = new();
			List<(SkillTreeSource source, SkillTreeFile file)> files = ParseAll(errors);
			if (errors.Count > 0)
				throw new InvalidDataException("스킬 트리 파일 오류\n" + string.Join("\n", errors));

			trees = new Dictionary<int, SkillTree>();
			foreach ((SkillTreeSource _, SkillTreeFile file) in files)
				trees.Add(file.Equipment.Value, Compile(file));
		}

		private List<(SkillTreeSource source, SkillTreeFile file)> ParseAll(List<string> errors)
		{
			HashSet<int> usedEquipments = new();
			HashSet<int> usedInlineCards = new();
			List<(SkillTreeSource, SkillTreeFile)> files = new();
			foreach (SkillTreeSource source in sources)
			{
				SkillTreeFile file = SkillTreeParser.Parse(source, errors);
				if (file == null)
					continue;
				SkillTreeValidator.Validate(source.Name, file, dataSource, usedEquipments, usedInlineCards, errors);
				files.Add((source, file));
			}
			return files;
		}

		private SkillTree Compile(SkillTreeFile file)
		{
			SkillTree tree = new(file.Equipment.Value);
			Dictionary<string, SkillTreeNode> nodes = new();
			foreach (SkillTreeNodeFile nodeFile in file.Nodes)
			{
				SkillTreeNode node = nodeFile.Card.HasValue
					? new SkillTreeNode(nodeFile.Id, RequireCard(nodeFile.Card.Value), false)
					: new SkillTreeNode(nodeFile.Id, CreateInlineCard(file, nodeFile), true);
				nodes.Add(nodeFile.Id, node);
				tree.Add(node);
			}

			foreach (SkillTreeNodeFile nodeFile in file.Nodes.Where(nodeFile => nodeFile.Requires != null))
			{
				foreach (SkillTreeRequireFile require in nodeFile.Requires)
				{
					nodes[nodeFile.Id].Requirements.Add(require.Node != null
						? new SkillTreeRequirement(nodes[require.Node], nodes[require.Node].Card, require.Stack)
						: new SkillTreeRequirement(null, RequireCard(require.Card.Value), require.Stack));
				}
			}
			return tree;
		}

		private CardData RequireCard(int id)
		{
			dataSource.TryGetCard(id, out CardData card);
			return card;
		}

		private CardData CreateInlineCard(SkillTreeFile file, SkillTreeNodeFile node)
		{
			List<EffectInfo> effects = new();
			foreach (SkillTreeEffectFile effect in node.Effects)
			{
				EffectType type = (EffectType)Enum.Parse(typeof(EffectType), effect.Type);
				dataSource.TryGetEffectTarget(type, effect.Target, out DataSO target);
				effects.Add(new EffectInfo
				{
					Type = type,
					Data = target,
					ArithmeticOperator = (ArithmeticOperator)Enum.Parse(typeof(ArithmeticOperator), effect.Operator),
					Value = effect.Value.Value,
				});
			}

			Sprite sprite;
			if (node.IconCard.HasValue)
				sprite = RequireCard(node.IconCard.Value).Sprite;
			else
				dataSource.TryGetEquipmentSprite(file.Equipment.Value, out sprite);

			return CardData.CreateRuntime(
				node.CardId.Value,
				node.Name,
				node.Description,
				sprite,
				effects,
				node.MaxStack.Value,
				node.Rarity != null ? (CardRarity)Enum.Parse(typeof(CardRarity), node.Rarity) : CardRarity.Common,
				node.Kind != null ? (SurvivalUIKind)Enum.Parse(typeof(SurvivalUIKind), node.Kind) : SurvivalUIKind.Count);
		}
	}
}
