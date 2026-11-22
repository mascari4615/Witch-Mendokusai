using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 스킬 트리 노드 하나의 묶음 정보. 묶음과 갈림길이 없으면 null
	public readonly struct SkillTreeBundleNode
	{
		public readonly string Id;
		public readonly string Bundle;
		public readonly string ExclusiveGroup;

		public SkillTreeBundleNode(string id, string bundle, string exclusiveGroup)
		{
			Id = id;
			Bundle = bundle;
			ExclusiveGroup = exclusiveGroup;
		}
	}

	// 트리 묶음과 갈림길 판정의 정본
	// - 갈림길: 같은 exclusiveGroup 에서 하나를 받으면 나머지는 그 판에서 잠김
	// - 묶음 마무리: 묶음 안 노드를 전부 받음. 갈림길에서 잠긴 쪽은 빼고 셈
	// ownedCount: 노드 id 별 이번 판에 받은 장 수
	public static class SkillTreeBundles
	{
		public static bool IsExcluded(IReadOnlyList<SkillTreeBundleNode> nodes, string nodeId, Func<string, int> ownedCount)
		{
			SkillTreeBundleNode target = Find(nodes, nodeId);
			if (string.IsNullOrEmpty(target.ExclusiveGroup) || ownedCount(nodeId) > 0)
				return false;
			foreach (SkillTreeBundleNode node in nodes)
			{
				if (node.Id != nodeId && node.ExclusiveGroup == target.ExclusiveGroup && ownedCount(node.Id) > 0)
					return true;
			}
			return false;
		}

		// 노드가 없는 묶음은 마무리될 수 없음 (오타가 조용히 참이 되지 않게)
		public static bool IsFinished(IReadOnlyList<SkillTreeBundleNode> nodes, string bundle, Func<string, int> ownedCount)
		{
			bool hasNode = false;
			foreach (SkillTreeBundleNode node in nodes)
			{
				if (node.Bundle != bundle)
					continue;
				hasNode = true;
				if (ownedCount(node.Id) > 0 || IsExcluded(nodes, node.Id, ownedCount))
					continue;
				return false;
			}
			return hasNode;
		}

		// 묶음 안에서 받은 노드 수와 마무리까지 필요한 노드 수. 갈림길에서 잠긴 쪽은 분모에서 뺌
		public static (int owned, int required) Progress(IReadOnlyList<SkillTreeBundleNode> nodes, string bundle, Func<string, int> ownedCount)
		{
			int owned = 0;
			int required = 0;
			HashSet<string> countedGroups = new HashSet<string>();
			foreach (SkillTreeBundleNode node in nodes)
			{
				if (node.Bundle != bundle || IsExcluded(nodes, node.Id, ownedCount))
					continue;
				// 아직 아무것도 안 고른 갈림길은 한 칸으로 셈
				bool isOpenChoice = string.IsNullOrEmpty(node.ExclusiveGroup) == false && ownedCount(node.Id) == 0;
				if (isOpenChoice && countedGroups.Add(node.ExclusiveGroup) == false)
					continue;
				required++;
				if (ownedCount(node.Id) > 0)
					owned++;
			}
			return (owned, required);
		}

		private static SkillTreeBundleNode Find(IReadOnlyList<SkillTreeBundleNode> nodes, string nodeId)
		{
			foreach (SkillTreeBundleNode node in nodes)
			{
				if (node.Id == nodeId)
					return node;
			}
			throw new ArgumentException($"트리에 없는 노드 {nodeId}");
		}
	}
}
