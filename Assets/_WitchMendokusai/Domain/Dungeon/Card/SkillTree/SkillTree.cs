using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	public enum SkillTreeNodeState
	{
		Locked = 0,
		Open = 1,
		Owned = 2,
		Maxed = 3,
	}

	public class SkillTreeRequirement
	{
		public SkillTreeNode Node { get; }
		public CardData Card { get; }
		public int Stack { get; }

		public bool IsExternal => Node == null;

		public SkillTreeRequirement(SkillTreeNode node, CardData card, int stack)
		{
			Node = node;
			Card = card;
			Stack = stack;
		}

		public int Current(IReadOnlyList<CardData> selectedCards) => SkillTree.Count(Card, selectedCards);
		public bool IsMet(IReadOnlyList<CardData> selectedCards) => Current(selectedCards) >= Stack;
	}

	public class SkillTreeNode
	{
		public string Id { get; }
		public CardData Card { get; }
		public bool IsInline { get; }
		public List<SkillTreeRequirement> Requirements { get; } = new();

		public SkillTreeNode(string id, CardData card, bool isInline)
		{
			Id = id;
			Card = card;
			IsInline = isInline;
		}

		public bool IsUnlocked(IReadOnlyList<CardData> selectedCards) => Requirements.All(requirement => requirement.IsMet(selectedCards));

		public SkillTreeNodeState State(IReadOnlyList<CardData> selectedCards)
		{
			int count = SkillTree.Count(Card, selectedCards);
			if (count >= Card.MaxStack)
				return SkillTreeNodeState.Maxed;
			if (count > 0)
				return SkillTreeNodeState.Owned;
			return IsUnlocked(selectedCards) ? SkillTreeNodeState.Open : SkillTreeNodeState.Locked;
		}
	}

	public class SkillTree
	{
		private readonly Dictionary<int, SkillTreeNode> nodesByCard = new();

		public int EquipmentID { get; }
		public List<SkillTreeNode> Nodes { get; } = new();

		public SkillTree(int equipmentId) => EquipmentID = equipmentId;

		public IEnumerable<CardData> InlineCards => Nodes.Where(node => node.IsInline).Select(node => node.Card);

		public void Add(SkillTreeNode node)
		{
			Nodes.Add(node);
			nodesByCard.Add(node.Card.ID, node);
		}

		public SkillTreeNode NodeOf(CardData card) => card != null && nodesByCard.TryGetValue(card.ID, out SkillTreeNode node) ? node : null;

		// 트리 밖 카드는 제한 없음
		public bool IsUnlocked(CardData card, IReadOnlyList<CardData> selectedCards)
		{
			SkillTreeNode node = NodeOf(card);
			return node == null || node.IsUnlocked(selectedCards);
		}

		public static int Count(CardData card, IReadOnlyList<CardData> selectedCards)
		{
			int count = 0;
			foreach (CardData selected in selectedCards)
				if (selected != null && selected.ID == card.ID)
					count++;
			return count;
		}
	}
}
