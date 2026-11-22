using System;
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
		// 묶음과 갈림길. 판정은 SkillTreeBundles (DomainSDK)
		public string Bundle { get; }
		public string ExclusiveGroup { get; }
		// 마무리돼야 열리는 묶음 (최종 카드)
		public List<string> RequiredBundles { get; } = new();
		// 차야 열리는 숙련 막대 (갈래 확정 카드). 없으면 DollMastery.NO_TRACK
		public int RequiredMasteryTrack { get; set; } = DollMastery.NO_TRACK;
		// 받는 순간 바뀌는 인물 형태 키. 없으면 null
		public string Form { get; set; }
		public SkillTree Tree { get; private set; }

		public SkillTreeNode(string id, CardData card, bool isInline, string bundle = null, string exclusiveGroup = null)
		{
			Id = id;
			Card = card;
			IsInline = isInline;
			Bundle = bundle;
			ExclusiveGroup = exclusiveGroup;
		}

		public void AttachTo(SkillTree tree) => Tree = tree;

		private bool HasProgression => Bundle != null || ExclusiveGroup != null || RequiredBundles.Count > 0 || RequiredMasteryTrack != DollMastery.NO_TRACK;

		// 기본 강화의 같은 무기 1회 연결은 시작부터 선택, 진화와 상위 및 외부 조건 유지
		// 묶음, 갈림길, 숙련이 걸린 노드는 순서가 뜻이라 제외
		public bool IsStartingUpgrade => Card != null && HasProgression == false && Card.Rarity == CardRarity.Common && Card.UIKind != SurvivalUIKind.Advanced
			&& Card.Requirements.Count == 0 && Requirements.All(requirement => requirement.IsExternal == false
				&& requirement.Stack == 1 && requirement.Node.IsStartingUpgrade);

		public IReadOnlyList<SkillTreeRequirement> ChoiceRequirements => IsStartingUpgrade ? Array.Empty<SkillTreeRequirement>() : Requirements;

		public bool IsUnlocked(IReadOnlyList<CardData> selectedCards) => Card != null && Card.MeetsRequirements(selectedCards)
			&& ChoiceRequirements.All(requirement => requirement.IsMet(selectedCards))
			&& (ExclusiveGroup == null || Tree.IsExcluded(this, selectedCards) == false)
			&& RequiredBundles.All(bundle => Tree.IsBundleFinished(bundle, selectedCards))
			&& (RequiredMasteryTrack == DollMastery.NO_TRACK || DollMasteryRun.IsTrackReady(RequiredMasteryTrack));

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
		private readonly List<SkillTreeBundleNode> bundleNodes = new();

		public int EquipmentID { get; }
		public List<SkillTreeNode> Nodes { get; } = new();

		public SkillTree(int equipmentId) => EquipmentID = equipmentId;

		public IEnumerable<CardData> InlineCards => Nodes.Where(node => node.IsInline).Select(node => node.Card);

		public void Add(SkillTreeNode node)
		{
			Nodes.Add(node);
			nodesByCard.Add(node.Card.ID, node);
			bundleNodes.Add(new SkillTreeBundleNode(node.Id, node.Bundle, node.ExclusiveGroup));
			node.AttachTo(this);
		}

		// 같은 갈림길의 다른 노드를 받았으면 잠김
		public bool IsExcluded(SkillTreeNode node, IReadOnlyList<CardData> selectedCards)
			=> node.ExclusiveGroup != null && SkillTreeBundles.IsExcluded(bundleNodes, node.Id, OwnedCount(selectedCards));

		public bool IsBundleFinished(string bundle, IReadOnlyList<CardData> selectedCards)
			=> SkillTreeBundles.IsFinished(bundleNodes, bundle, OwnedCount(selectedCards));

		public (int owned, int required) BundleProgress(string bundle, IReadOnlyList<CardData> selectedCards)
			=> SkillTreeBundles.Progress(bundleNodes, bundle, OwnedCount(selectedCards));

		private Func<string, int> OwnedCount(IReadOnlyList<CardData> selectedCards)
			=> nodeId => Count(Nodes.First(node => node.Id == nodeId).Card, selectedCards);

		public SkillTreeNode NodeOf(CardData card) => card != null && nodesByCard.TryGetValue(card.ID, out SkillTreeNode node) ? node : null;

		// 트리 밖 카드는 제한 없음
		public bool IsUnlocked(CardData card, IReadOnlyList<CardData> selectedCards)
		{
			SkillTreeNode node = NodeOf(card);
			return node == null || node.IsUnlocked(selectedCards);
		}

		// 다른 노드가 기대는 시작 노드. 빼면 하위 노드가 그 판에서 잠김
		public bool IsRoot(CardData card)
		{
			SkillTreeNode node = NodeOf(card);
			if (node == null || node.Requirements.Count > 0)
				return false;
			return Nodes.Any(other => other.Requirements.Any(requirement => requirement.Card.ID == card.ID));
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
