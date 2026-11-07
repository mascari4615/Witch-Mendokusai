using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// 형식 검증: 잘못된 ID, 연결, 순환, 없는 효과
	public static class SkillTreeValidator
	{
		public static void Validate(string sourceName, SkillTreeFile file, ISkillTreeDataSource dataSource, HashSet<int> usedEquipments, HashSet<int> usedInlineCards, List<string> errors)
		{
			void Report(string message) => errors.Add($"[{sourceName}] {message}");

			IReadOnlyList<CardData> equipmentCards = null;
			if (file.Equipment.HasValue == false)
				Report("equipment 없음");
			else if (dataSource.TryGetEquipmentCards(file.Equipment.Value, out equipmentCards) == false)
				Report($"없는 장비 {file.Equipment.Value}");
			else if (usedEquipments.Add(file.Equipment.Value) == false)
				Report($"장비 {file.Equipment.Value} 의 트리가 둘 이상");

			if (file.Nodes == null || file.Nodes.Count == 0)
			{
				Report("nodes 없음");
				return;
			}

			Dictionary<string, SkillTreeNodeFile> nodes = new();
			foreach (SkillTreeNodeFile node in file.Nodes)
			{
				if (string.IsNullOrEmpty(node.Id))
					Report("노드 id 없음");
				else if (nodes.TryAdd(node.Id, node) == false)
					Report($"중복 노드 id {node.Id}");
			}

			foreach (SkillTreeNodeFile node in file.Nodes)
			{
				if (node.Card.HasValue == node.CardId.HasValue)
					Report($"노드 {node.Id}: card 와 cardId 중 하나만");
				else if (node.Card.HasValue)
					ValidateReference(node, nodes, dataSource, Report);
				else
					ValidateInline(node, dataSource, usedInlineCards, Report);

				ValidateRequires(node, nodes, dataSource, Report);
			}

			List<int> referencedCards = file.Nodes.Where(node => node.Card.HasValue).Select(node => node.Card.Value).ToList();
			foreach (int duplicate in referencedCards.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))
				Report($"카드 {duplicate} 를 두 노드가 참조");

			if (equipmentCards != null)
				foreach (CardData card in equipmentCards.Where(card => card != null && referencedCards.Contains(card.ID) == false))
					Report($"장비 카드 {card.ID} 가 트리에 없음");

			foreach (string cycle in FindCycles(file.Nodes, nodes))
				Report($"순환 연결 {cycle}");
		}

		private static void ValidateReference(SkillTreeNodeFile node, Dictionary<string, SkillTreeNodeFile> nodes, ISkillTreeDataSource dataSource, Action<string> report)
		{
			if (node.Effects != null || node.Name != null || node.MaxStack.HasValue || node.Kind != null || node.Rarity != null)
				report($"노드 {node.Id}: card 참조 노드는 효과와 이름을 카드 자산에서 읽음");

			if (dataSource.TryGetCard(node.Card.Value, out CardData card) == false)
			{
				report($"노드 {node.Id}: 없는 카드 {node.Card.Value}");
				return;
			}

			foreach (CardData.Requirement requirement in card.Requirements)
			{
				bool isListed = node.Requires != null && node.Requires.Any(require => require.Stack >= requirement.Stack && RequiredCardId(require, nodes) == requirement.Card.ID);
				if (isListed == false)
					report($"노드 {node.Id}: 카드 조건 {requirement.Card.ID}x{requirement.Stack} 이 requires 에 없음");
			}
		}

		private static int? RequiredCardId(SkillTreeRequireFile require, Dictionary<string, SkillTreeNodeFile> nodes)
		{
			if (require.Card.HasValue)
				return require.Card;
			return require.Node != null && nodes.TryGetValue(require.Node, out SkillTreeNodeFile target) ? target.Card : null;
		}

		private static void ValidateInline(SkillTreeNodeFile node, ISkillTreeDataSource dataSource, HashSet<int> usedInlineCards, Action<string> report)
		{
			int cardId = node.CardId.Value;
			if (dataSource.TryGetCard(cardId, out _))
				report($"노드 {node.Id}: cardId {cardId} 는 기존 카드와 겹침");
			else if (usedInlineCards.Add(cardId) == false)
				report($"노드 {node.Id}: cardId {cardId} 중복");

			if (string.IsNullOrEmpty(node.Name))
				report($"노드 {node.Id}: name 없음");
			if (string.IsNullOrEmpty(node.Description))
				report($"노드 {node.Id}: description 없음");
			if (node.MaxStack.HasValue == false || node.MaxStack.Value < 1)
				report($"노드 {node.Id}: maxStack 은 1 이상");
			if (node.Kind != null && Enum.TryParse(node.Kind, out SurvivalUIKind _) == false)
				report($"노드 {node.Id}: 없는 kind {node.Kind}");
			if (node.Rarity != null && Enum.TryParse(node.Rarity, out CardRarity _) == false)
				report($"노드 {node.Id}: 없는 rarity {node.Rarity}");
			if (node.IconCard.HasValue && dataSource.TryGetCard(node.IconCard.Value, out _) == false)
				report($"노드 {node.Id}: 없는 iconCard {node.IconCard.Value}");

			if (node.Effects == null || node.Effects.Count == 0)
			{
				report($"노드 {node.Id}: effects 없음");
				return;
			}

			foreach (SkillTreeEffectFile effect in node.Effects)
				ValidateEffect(node, effect, dataSource, report);
		}

		private static void ValidateEffect(SkillTreeNodeFile node, SkillTreeEffectFile effect, ISkillTreeDataSource dataSource, Action<string> report)
		{
			if (Enum.TryParse(effect.Type, out EffectType type) == false)
				report($"노드 {node.Id}: 없는 효과 종류 {effect.Type}");
			else if (dataSource.TryGetEffectTarget(type, effect.Target, out _) == false)
				report($"노드 {node.Id}: 없는 효과 대상 {effect.Type}/{effect.Target}");

			if (Enum.TryParse(effect.Operator, out ArithmeticOperator _) == false)
				report($"노드 {node.Id}: 없는 연산 {effect.Operator}");
			if (effect.Value.HasValue == false)
				report($"노드 {node.Id}: 효과 value 없음");
		}

		private static void ValidateRequires(SkillTreeNodeFile node, Dictionary<string, SkillTreeNodeFile> nodes, ISkillTreeDataSource dataSource, Action<string> report)
		{
			if (node.Requires == null)
				return;

			foreach (SkillTreeRequireFile require in node.Requires)
			{
				if (require.Stack < 1)
					report($"노드 {node.Id}: 조건 stack 은 1 이상");

				if ((require.Node != null) == require.Card.HasValue)
				{
					report($"노드 {node.Id}: 조건은 node 와 card 중 하나만");
					continue;
				}

				if (require.Node != null)
				{
					if (nodes.TryGetValue(require.Node, out SkillTreeNodeFile target) == false)
						report($"노드 {node.Id}: 없는 노드 {require.Node} 를 조건으로 씀");
					else if (MaxStackOf(target, dataSource) is int maxStack && require.Stack > maxStack)
						report($"노드 {node.Id}: {require.Node} 조건 {require.Stack}회가 최대 {maxStack}회를 넘음");
				}
				else if (dataSource.TryGetCard(require.Card.Value, out CardData card) == false)
				{
					report($"노드 {node.Id}: 조건의 없는 카드 {require.Card.Value}");
				}
				else if (require.Stack > card.MaxStack)
				{
					report($"노드 {node.Id}: 카드 {card.ID} 조건 {require.Stack}회가 최대 {card.MaxStack}회를 넘음");
				}
			}
		}

		private static int? MaxStackOf(SkillTreeNodeFile node, ISkillTreeDataSource dataSource)
		{
			if (node.MaxStack.HasValue)
				return node.MaxStack;
			if (node.Card.HasValue && dataSource.TryGetCard(node.Card.Value, out CardData card))
				return card.MaxStack;
			return null;
		}

		private static List<string> FindCycles(List<SkillTreeNodeFile> nodeList, Dictionary<string, SkillTreeNodeFile> nodes)
		{
			List<string> cycles = new();
			HashSet<string> finished = new();
			List<string> path = new();

			void Visit(string id)
			{
				int pathIndex = path.IndexOf(id);
				if (pathIndex >= 0)
				{
					cycles.Add(string.Join(" -> ", path.Skip(pathIndex).Append(id)));
					return;
				}
				if (finished.Contains(id) || nodes.TryGetValue(id, out SkillTreeNodeFile node) == false)
					return;

				path.Add(id);
				if (node.Requires != null)
					foreach (SkillTreeRequireFile require in node.Requires.Where(require => require.Node != null))
						Visit(require.Node);
				path.RemoveAt(path.Count - 1);
				finished.Add(id);
			}

			foreach (SkillTreeNodeFile node in nodeList.Where(node => string.IsNullOrEmpty(node.Id) == false))
				Visit(node.Id);
			return cycles;
		}
	}
}
